// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol
{
    /// <summary>
    /// The named pipe protocol engine. Reads/writes framed messages over the transport,
    /// correlates request/response pairs, and dispatches to a handler.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="NamedPipeProtocol"/> class.
    /// </remarks>
    /// <param name="reader">The transport for reading incoming frames.</param>
    /// <param name="writer">The transport for writing outgoing frames.</param>
    /// <param name="logger">The logger instance.</param>
    internal sealed class NamedPipeProtocol(NamedPipeTransport reader, NamedPipeTransport writer, ILogger logger) : IAsyncDisposable
    {
        /// <summary>
        /// Maximum allowed payload size per frame. Mirrors
        /// <c>Microsoft.Bot.Streaming.Transport.TransportConstants.MaxLength</c>:
        /// the on-wire <c>Header.PayloadLength</c> field is encoded as 6 ASCII digits,
        /// so 999,999 is the hard ceiling enforced by the protocol itself.
        /// </summary>
        private const int MaxPayloadSize = 999_999;

        /// <summary>
        /// Maximum number of concurrent stream buffers. Limits memory usage from
        /// unmatched stream frames.
        /// </summary>
        private const int MaxStreamBuffers = 100;

        /// <summary>
        /// Maximum cumulative size (in bytes) buffered for a single stream id. Prevents a
        /// misbehaving or hostile peer from driving unbounded memory growth by sending an
        /// arbitrary number of <see cref="PayloadTypes.Stream"/> frames against the same id
        /// without ever setting the End flag. 100 MiB comfortably covers expected activity
        /// payloads including embedded attachments while bounding worst-case usage at
        /// MaxStreamBuffers × MaxStreamSize ≈ 10 GiB total before a disconnect is forced.
        /// </summary>
        private const int MaxStreamSize = 100 * 1024 * 1024;

        private readonly NamedPipeTransport _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        private readonly NamedPipeTransport _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly Dictionary<Guid, TaskCompletionSource<ReceiveResponse>> _pendingRequests = [];
        private readonly object _dispatchedTasksLock = new();
        private readonly List<Task> _dispatchedTasks = [];

        // In-flight inbound dispatches keyed by request id, so a peer CancelStream/CancelAll
        // can cancel the handler. Uses ConcurrentDictionary because the dispatch-completion
        // continuation removes entries from the thread pool while the read loop reads/cancels.
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inflightDispatches = new();

        private CancellationTokenSource _cts;
        private volatile Task _readLoop;
        private int _started;

        /// <summary>
        /// Gets or sets the handler invoked when an inbound request is received
        /// (e.g., POST /api/messages from DirectLineFlex).
        /// </summary>
        internal Func<NamedPipeRequest, CancellationToken, Task<NamedPipeResponse>> OnRequestReceived { get; set; }

        /// <summary>
        /// Gets a task that completes when the read loop exits, either due to
        /// pipe disconnection, a protocol error, or disposal. Callers can await
        /// this to be signalled of disconnect without polling.
        /// </summary>
        internal Task Completion => _readLoop ?? Task.CompletedTask;

        /// <summary>
        /// Start the background read loop that processes incoming frames.
        /// Must be called at most once per instance.
        /// </summary>
        internal void Start()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                throw new InvalidOperationException("NamedPipeProtocol has already been started.");
            }

            _cts = new CancellationTokenSource();
            _readLoop = ReadLoopAsync(_cts.Token);
        }

        /// <summary>
        /// Send a request to the remote end and wait for its response.
        /// Used for outbound activities (agent → DirectLineFlex).
        /// </summary>
        /// <param name="verb">The HTTP verb.</param>
        /// <param name="path">The request path.</param>
        /// <param name="body">The request body, or null.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>The response from the remote end.</returns>
        internal Task<ReceiveResponse> SendRequestAsync(string verb, string path, byte[] body, CancellationToken cancellationToken)
            => SendRequestAsync(verb, path, body, attachments: null, contentType: null, cancellationToken);

        /// <summary>
        /// Send a request — optionally with multi-stream attachments — to the remote end and
        /// wait for its response. Each attachment is advertised as an entry in the request's
        /// Streams[] and delivered as its own <see cref="PayloadTypes.Stream"/> frame sequence,
        /// matching the Bot Framework streaming spec used by DirectLineFlex.
        /// </summary>
        /// <param name="verb">The HTTP verb (e.g., POST, GET). Required.</param>
        /// <param name="path">The request path (e.g., /api/messages). Required.</param>
        /// <param name="body">The request body bytes for the primary stream, or null for an empty body.</param>
        /// <param name="attachments">Optional additional streams to deliver alongside the primary body, or null.</param>
        /// <param name="contentType">Content type for the primary body stream; defaults to <c>application/json</c> when null or empty.</param>
        /// <param name="cancellationToken">A cancellation token observed for the duration of the call.</param>
        /// <returns>The response from the remote end.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="verb"/> or <paramref name="path"/> is null, empty, or whitespace.</exception>
        internal async Task<ReceiveResponse> SendRequestAsync(string verb, string path, byte[] body,
            IList<NamedPipeAttachment> attachments, string contentType, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(verb))
            {
                throw new ArgumentException("Verb must be a non-empty, non-whitespace string.", nameof(verb));
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path must be a non-empty, non-whitespace string.", nameof(path));
            }

            var requestId = Guid.NewGuid();
            var tcs = new TaskCompletionSource<ReceiveResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_pendingRequests)
            {
                _pendingRequests[requestId] = tcs;
            }

            try
            {
                var bodyStreamId = body != null ? Guid.NewGuid() : (Guid?)null;
                var attachmentIds = MaterializeAttachmentIds(attachments);

                var requestPayload = new RequestPayload
                {
                    Verb = verb,
                    Path = path,
                    Streams = BuildStreamDescriptors(bodyStreamId, body?.Length, attachments, attachmentIds, contentType),
                };

                var payloadJson = JsonSerializer.SerializeToUtf8Bytes(requestPayload);

                // End is per-payload-id (Bot.Streaming framing). The Request payload-id
                // carries only the JSON envelope — its body bytes travel under their own
                // stream id. The JSON is complete in this single frame, so End=true.
                await SendSingleFrameAsync(PayloadTypes.Request, requestId, payloadJson, end: true, cancellationToken).ConfigureAwait(false);

                if (body != null)
                {
                    await SendStreamFramesAsync(bodyStreamId.Value, body, end: true, cancellationToken).ConfigureAwait(false);
                }

                await SendAttachmentFramesAsync(attachments, attachmentIds, cancellationToken).ConfigureAwait(false);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

                try
                {
                    return await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Caller-initiated cancellation — propagate as-is.
                    throw;
                }
                catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested)
                {
                    // OCE originated from the TCS itself (e.g., inbound CancelAll faulted the
                    // pending request); propagate without rewriting to TimeoutException.
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("Named pipe request timed out waiting for a response.");
                }
            }
            finally
            {
                lock (_pendingRequests)
                {
                    _pendingRequests.Remove(requestId);
                }
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var headerBuffer = new byte[HeaderSerializer.HeaderSize];
            var streamBuffers = new Dictionary<Guid, MemoryStream>();
            var completedStreams = new HashSet<Guid>();
            var expectedStreamLengths = new Dictionary<Guid, int>();
            var pendingDispatches = new Dictionary<Guid, (Header header, RequestPayload payload)>();
            var pendingResponses = new Dictionary<Guid, ResponsePayload>();

            try
            {
                var connected = true;
                while (!cancellationToken.IsCancellationRequested && _reader.IsConnected && connected)
                {
                    if (!await _reader.ReadExactAsync(headerBuffer, HeaderSerializer.HeaderSize, cancellationToken).ConfigureAwait(false))
                    {
                        _logger.LogInformation("NamedPipeProtocol: Pipe disconnected (read returned 0).");
                        break;
                    }

                    Header header;
                    try
                    {
                        header = HeaderSerializer.Deserialize(headerBuffer);
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogError(
                            "NamedPipeProtocol: Header parse failed. Raw bytes (hex): {Hex}. Exception: {Msg}",
                            Convert.ToHexString(headerBuffer),
                            ex.Message);
                        throw;
                    }

                    _logger.LogDebug("NamedPipeProtocol: Frame type={Type} id={Id} len={Len} end={End}",
                        header.Type, header.Id, header.PayloadLength, header.End);

                    byte[] payload = null;
                    if (header.PayloadLength > 0)
                    {
                        if (header.PayloadLength > MaxPayloadSize)
                        {
                            _logger.LogError("NamedPipeProtocol: Payload size {Size} exceeds maximum {Max}. Disconnecting.",
                                header.PayloadLength, MaxPayloadSize);
                            break;
                        }

                        payload = new byte[header.PayloadLength];
                        if (!await _reader.ReadExactAsync(payload, header.PayloadLength, cancellationToken).ConfigureAwait(false))
                        {
                            _logger.LogWarning("NamedPipeProtocol: Pipe disconnected mid-payload.");
                            break;
                        }
                    }

                    if (header.Type == PayloadTypes.Stream)
                    {
                        payload = await ReadTrailingSingleFrameStreamBytesIfNeededAsync(
                            header,
                            payload,
                            streamBuffers,
                            expectedStreamLengths,
                            cancellationToken).ConfigureAwait(false);
                    }

                    switch (header.Type)
                    {
                        case PayloadTypes.Request:
                            HandleRequestFrame(header, payload, streamBuffers, completedStreams, expectedStreamLengths, pendingDispatches, cancellationToken);
                            break;

                        case PayloadTypes.Response:
                            HandleResponseFrame(header, payload, streamBuffers, completedStreams, expectedStreamLengths, pendingResponses);
                            break;

                        case PayloadTypes.Stream:
                            connected = HandleStreamFrame(header, payload, streamBuffers, completedStreams, _logger);
                            break;

                        case PayloadTypes.CancelAll:
                            _logger.LogWarning("NamedPipeProtocol: Received CancelAll from peer.");
                            HandleCancelAllFrame(streamBuffers, completedStreams, expectedStreamLengths, pendingDispatches, pendingResponses);
                            break;

                        case PayloadTypes.CancelStream:
                            _logger.LogInformation("NamedPipeProtocol: Received CancelStream for {Id}.", header.Id);
                            HandleCancelStreamFrame(header.Id, streamBuffers, completedStreams, expectedStreamLengths, pendingDispatches);
                            break;

                        default:
                            _logger.LogWarning("NamedPipeProtocol: Unknown frame type '{Type}'.", header.Type);
                            break;
                    }

                    if (connected)
                    {
                        await TryDispatchPendingRequestsAsync(streamBuffers, completedStreams, expectedStreamLengths, pendingDispatches, cancellationToken).ConfigureAwait(false);
                        TryCompletePendingResponses(streamBuffers, completedStreams, expectedStreamLengths, pendingResponses);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeProtocol: Read loop error.");
            }
            finally
            {
                // Fail any pending outbound requests so callers don't wait the full timeout.
                FailPendingRequests(new IOException("Named pipe disconnected."));

                // Dispose any orphaned stream buffers to prevent memory leaks
                foreach (var ms in streamBuffers.Values)
                {
                    ms.Dispose();
                }

                streamBuffers.Clear();
                completedStreams.Clear();
                expectedStreamLengths.Clear();
                pendingDispatches.Clear();
                pendingResponses.Clear();
            }
        }

        private void FailPendingRequests(Exception exception)
        {
            List<TaskCompletionSource<ReceiveResponse>> pending;
            lock (_pendingRequests)
            {
                if (_pendingRequests.Count == 0)
                {
                    return;
                }

                pending = [.. _pendingRequests.Values];
                _pendingRequests.Clear();
            }

            foreach (var tcs in pending)
            {
                tcs.TrySetException(exception);
            }
        }

        private void HandleRequestFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            if (payload == null)
            {
                return;
            }

            var requestPayload = JsonSerializer.Deserialize<RequestPayload>(payload);
            if (requestPayload == null)
            {
                return;
            }

            _logger.LogDebug(
                "NamedPipeProtocol: Inbound Request id={Id} verb={Verb} path={Path} streamCount={StreamCount} payloadBytes={PayloadLen}",
                header.Id, requestPayload.Verb, requestPayload.Path,
                requestPayload.Streams?.Count ?? 0, payload.Length);

            TrackExpectedStreamLengths(requestPayload.Streams, header.Id, expectedStreamLengths);

            if (requestPayload.Streams is { Count: > 0 })
            {
                if (!AllStreamsComplete(requestPayload.Streams, header.Id, completedStreams))
                {
                    pendingDispatches[header.Id] = (header, requestPayload);
                    return;
                }
            }

            // Extract the body and any attachments synchronously on the read-loop thread so
            // the dispatch task never touches streamBuffers concurrently with the read loop.
            var (body, contentType, attachments) = ExtractRequestPayload(header, requestPayload, streamBuffers, completedStreams, expectedStreamLengths);
            StartDispatch(header, requestPayload, body, contentType, attachments, cancellationToken);
        }

        private async Task<byte[]> ReadTrailingSingleFrameStreamBytesIfNeededAsync(
            Header header,
            byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            Dictionary<Guid, int> expectedStreamLengths,
            CancellationToken cancellationToken)
        {
            payload ??= [];

            // Bot.Streaming / Bot.Connector.Streaming can forward a stream with
            // Header.End=true but write MORE bytes than Header.PayloadLength when
            // the sender's ContentLength is wrong (common with attachments relayed
            // through DirectLineFlex). Use the advertised stream descriptor length
            // to drain the trailing unframed bytes so they don't desync the reader.
            if (!header.End
                || !expectedStreamLengths.TryGetValue(header.Id, out var expectedLength))
            {
                return payload;
            }

            var bufferedLength = streamBuffers.TryGetValue(header.Id, out var existing)
                ? existing.Length
                : 0;
            var receivedLength = bufferedLength + payload.Length;
            if (receivedLength >= expectedLength)
            {
                return payload;
            }

            var missing = (int)(expectedLength - receivedLength);
            _logger.LogWarning(
                "NamedPipeProtocol: Stream {Id} ended after {Received} bytes but descriptor length is {Expected}; reading {Missing} trailing bytes for Bot.Streaming boundary compatibility.",
                header.Id,
                receivedLength,
                expectedLength,
                missing);

            var repairedPayload = new byte[payload.Length + missing];
            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, repairedPayload, 0, payload.Length);
            }

            if (!await _reader.ReadExactAsync(repairedPayload.AsMemory(payload.Length), missing, cancellationToken).ConfigureAwait(false))
            {
                throw new IOException("Pipe disconnected while reading trailing stream bytes.");
            }

            return repairedPayload;
        }

        /// <summary>
        /// Returns true when every stream id referenced by the given Streams[] descriptors
        /// (primary + attachments) has been observed with End=true. Required so that
        /// multi-stream requests are not dispatched before all attachment bytes have arrived.
        /// Malformed/unparseable ids are treated as "complete" (extraction will surface the
        /// resulting empty payload to the agent rather than blocking dispatch forever).
        /// </summary>
        private bool AllStreamsComplete(List<PayloadDescription> streams, Guid ownerId, HashSet<Guid> completedStreams)
        {
            if (streams == null)
            {
                return true;
            }

            for (int i = 0; i < streams.Count; i++)
            {
                if (!TryGetStreamId(streams[i]?.Id, ownerId, out var sid))
                {
                    continue;
                }

                if (!completedStreams.Contains(sid))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Extracts the assembled primary body and any attachment streams for a request.
        /// Removes the corresponding entries from <paramref name="streamBuffers"/> and
        /// <paramref name="completedStreams"/>. Must only be called on the read-loop thread.
        /// </summary>
        /// <remarks>
        /// Streams[0] is the request body (Activity JSON for /api/messages traffic).
        /// Streams[1..N] are attachment payloads — DirectLineFlex sends one such stream per
        /// file uploaded by a user (see
        /// <c>Intercom/Microsoft.DirectLineFlex/Services/BotConnection.SendActivityAsync</c>).
        /// </remarks>
        private (byte[] Body, string ContentType, List<NamedPipeAttachment> Attachments) ExtractRequestPayload(
            Header header,
            RequestPayload requestPayload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths)
        {
            if (requestPayload.Streams is not { Count: > 0 })
            {
                return (null, "application/json", []);
            }

            var primary = requestPayload.Streams[0];
            var body = TakeStreamBody(primary, header.Id, streamBuffers, completedStreams, expectedStreamLengths);
            var attachments = TakeAttachmentStreams(requestPayload.Streams, header.Id, streamBuffers, completedStreams, expectedStreamLengths);
            var contentType = string.IsNullOrEmpty(primary?.ContentType) ? "application/json" : primary.ContentType;
            return (body, contentType, attachments);
        }

        /// <summary>
        /// Removes and returns the assembled bytes for a stream descriptor, or <c>null</c> when
        /// the descriptor is missing/malformed or its frames never arrived.
        /// </summary>
        private byte[] TakeStreamBody(
            PayloadDescription descriptor,
            Guid ownerId,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths)
        {
            if (!TryGetStreamId(descriptor?.Id, ownerId, out var streamId)
                || !streamBuffers.TryGetValue(streamId, out var ms))
            {
                return null;
            }

            var body = ms.ToArray();
            ms.Dispose();
            streamBuffers.Remove(streamId);
            completedStreams.Remove(streamId);
            expectedStreamLengths.Remove(streamId);
            return body;
        }

        /// <summary>
        /// Removes and returns assembled <see cref="NamedPipeAttachment"/> instances for all
        /// <c>Streams[1..N]</c> descriptors. Always returns a list (possibly empty) so callers
        /// can pass it straight to <see cref="NamedPipeRequest.Attachments"/> /
        /// <see cref="ReceiveResponse.Attachments"/>.
        /// </summary>
        private List<NamedPipeAttachment> TakeAttachmentStreams(
            List<PayloadDescription> streams,
            Guid ownerId,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths)
        {
            if (streams == null || streams.Count <= 1)
            {
                return [];
            }

            var attachments = new List<NamedPipeAttachment>(streams.Count - 1);
            for (int i = 1; i < streams.Count; i++)
            {
                var descriptor = streams[i];
                if (descriptor == null)
                {
                    continue;
                }

                if (!TryGetStreamId(descriptor.Id, ownerId, out var attachmentId))
                {
                    continue;
                }

                byte[] body = [];
                if (streamBuffers.TryGetValue(attachmentId, out var ms))
                {
                    body = ms.ToArray();
                    ms.Dispose();
                    streamBuffers.Remove(attachmentId);
                }

                completedStreams.Remove(attachmentId);
                expectedStreamLengths.Remove(attachmentId);

                attachments.Add(new NamedPipeAttachment
                {
                    Id = descriptor.Id,
                    ContentType = descriptor.ContentType ?? string.Empty,
                    Body = body,
                });
            }

            return attachments;
        }

        private void TrackExpectedStreamLengths(List<PayloadDescription> streams, Guid ownerId, Dictionary<Guid, int> expectedStreamLengths)
        {
            if (streams == null)
            {
                return;
            }

            foreach (var descriptor in streams)
            {
                if (descriptor?.Length is not { } length || length < 0)
                {
                    continue;
                }

                if (TryGetStreamId(descriptor.Id, ownerId, out var streamId))
                {
                    expectedStreamLengths[streamId] = length;
                    _logger.LogDebug(
                        "NamedPipeProtocol: Tracking stream {StreamId} expectedLength={Length} type={Type}.",
                        streamId, length, descriptor.ContentType);
                }
            }
        }

        private bool TryGetStreamId(string id, Guid requestId, out Guid streamId)
        {
            if (!Guid.TryParse(id, out streamId))
            {
                _logger.LogWarning("NamedPipeProtocol: Invalid stream id '{Id}' for request {RequestId}; ignoring frame.", id, requestId);
                return false;
            }

            return true;
        }

        private void TrackDispatch(Task task)
        {
            lock (_dispatchedTasksLock)
            {
                _dispatchedTasks.Add(task);
            }

            _ = task.ContinueWith(
                t =>
                {
                    lock (_dispatchedTasksLock)
                    {
                        _dispatchedTasks.Remove(t);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private Task TryDispatchPendingRequestsAsync(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            var dispatched = new List<Guid>();
            foreach (var (requestId, pending) in pendingDispatches)
            {
                if (pending.payload.Streams is { Count: > 0 }
                    && AllStreamsComplete(pending.payload.Streams, requestId, completedStreams))
                {
                    dispatched.Add(requestId);
                    var (body, contentType, attachments) = ExtractRequestPayload(pending.header, pending.payload, streamBuffers, completedStreams, expectedStreamLengths);
                    StartDispatch(pending.header, pending.payload, body, contentType, attachments, cancellationToken);
                }
            }

            foreach (var id in dispatched)
            {
                pendingDispatches.Remove(id);
            }

            return Task.CompletedTask;
        }

        private async Task DispatchRequestSafeAsync(Header header, RequestPayload requestPayload,
            byte[] body, string contentType, List<NamedPipeAttachment> attachments, CancellationToken cancellationToken)
        {
            try
            {
                await DispatchRequestAsync(header, requestPayload, body, contentType, attachments, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown — suppress
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeProtocol: Unhandled exception dispatching request {Id}.", header.Id);
            }
        }

        private async Task DispatchRequestAsync(Header header, RequestPayload requestPayload,
            byte[] body, string contentType, List<NamedPipeAttachment> attachments, CancellationToken cancellationToken)
        {
            var request = new NamedPipeRequest
            {
                Id = header.Id,
                Verb = requestPayload.Verb,
                Path = requestPayload.Path,
                Body = body,
                ContentType = string.IsNullOrEmpty(contentType) ? "application/json" : contentType,
                Attachments = attachments ?? [],
            };

            _logger.LogDebug("NamedPipeProtocol: Dispatching {Verb} {Path} (BodyLen={Len}, ContentType={ContentType}, Attachments={AttachmentCount}).",
                request.Verb, request.Path, body?.Length ?? 0, request.ContentType, request.Attachments.Count);

            // Capture the delegate once to avoid a torn read between the null check and invocation.
            var handler = OnRequestReceived;
            var response = handler != null
                ? await handler(request, cancellationToken).ConfigureAwait(false)
                : NamedPipeResponse.NotFound();

            await SendResponseAsync(header.Id, response, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Registers a per-request <see cref="CancellationTokenSource"/> linked to the read-loop's
        /// token so that an inbound <c>CancelStream</c>/<c>CancelAll</c> can cancel the running
        /// handler, then starts the dispatch task. Called only from the read-loop thread.
        /// </summary>
        private void StartDispatch(
            Header header,
            RequestPayload requestPayload,
            byte[] body,
            string contentType,
            List<NamedPipeAttachment> attachments,
            CancellationToken cancellationToken)
        {
            var requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _inflightDispatches[header.Id] = requestCts;

            var dispatchTask = DispatchRequestSafeAsync(header, requestPayload, body, contentType, attachments, requestCts.Token);

            // Remove the in-flight tracking entry once the handler returns. We intentionally do
            // NOT call requestCts.Dispose() here: HandleCancelAllFrame may be iterating
            // _inflightDispatches.Values on the read-loop thread and calling Cancel() on
            // snapshotted references. Disposing here would race with that and surface as
            // ObjectDisposedException. The CTS is request-scoped and short-lived; GC will
            // reclaim it. This matches the rationale documented for _writeLock at DisposeAsync.
            // We also drop TaskContinuationOptions.ExecuteSynchronously so a synchronously
            // completing handler can't reenter the read-loop thread to mutate _inflightDispatches.
            _ = dispatchTask.ContinueWith(
                static (completedTask, state) =>
                {
                    var s = ((NamedPipeProtocol Self, Guid Id))state;
                    s.Self._inflightDispatches.TryRemove(s.Id, out _);
                },
                (this, header.Id),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            TrackDispatch(dispatchTask);
        }

        /// <summary>
        /// Handles an inbound <see cref="PayloadTypes.CancelStream"/> frame for a single payload id.
        /// Drops the buffered stream bytes (if any), marks the stream complete so a pending dispatch
        /// can move forward with empty data, and cancels the in-flight dispatch if the id is also
        /// a request id. Mutates read-loop state — called only from the read loop.
        /// </summary>
        private void HandleCancelStreamFrame(
            Guid streamId,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches)
        {
            if (streamBuffers.Remove(streamId, out var buf))
            {
                try { buf.Dispose(); } catch { /* benign */ }
            }

            // Treat cancellation as "no more bytes coming" so any pending dispatch
            // whose primary/attachment streams are otherwise satisfied can complete.
            completedStreams.Add(streamId);
            expectedStreamLengths.Remove(streamId);

            // If the cancelled id is the request id of an in-flight dispatch, cancel it.
            if (_inflightDispatches.TryGetValue(streamId, out var requestCts))
            {
                try { requestCts.Cancel(); } catch { /* already disposed */ }
            }

            // If the cancelled id matches an as-yet-undispatched pending request's primary
            // stream id (header.Id == streamId for the request), drop it — the peer told us
            // they're giving up.
            if (pendingDispatches.Remove(streamId))
            {
                _logger.LogInformation("NamedPipeProtocol: Dropped pending request {Id} due to CancelStream.", streamId);
            }
        }

        /// <summary>
        /// Handles an inbound <see cref="PayloadTypes.CancelAll"/> frame: clears all buffered
        /// state, cancels every in-flight dispatch, and fails every outbound pending request
        /// with <see cref="OperationCanceledException"/>. Mutates read-loop state — called only
        /// from the read loop.
        /// </summary>
        private void HandleCancelAllFrame(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload)> pendingDispatches,
            Dictionary<Guid, ResponsePayload> pendingResponses)
        {
            foreach (var buf in streamBuffers.Values)
            {
                try { buf.Dispose(); } catch { /* benign */ }
            }

            streamBuffers.Clear();
            completedStreams.Clear();
            expectedStreamLengths.Clear();
            pendingDispatches.Clear();
            pendingResponses.Clear();

            // Cancel every in-flight inbound dispatch.
            foreach (var cts in _inflightDispatches.Values)
            {
                try { cts.Cancel(); } catch { /* already disposed */ }
            }

            // Fail every outbound pending request — the peer just told us nothing will be
            // responded to.
            KeyValuePair<Guid, TaskCompletionSource<ReceiveResponse>>[] outbound;
            lock (_pendingRequests)
            {
                outbound = new KeyValuePair<Guid, TaskCompletionSource<ReceiveResponse>>[_pendingRequests.Count];
                int i = 0;
                foreach (var kvp in _pendingRequests)
                {
                    outbound[i++] = kvp;
                }

                _pendingRequests.Clear();
            }

            foreach (var kvp in outbound)
            {
                kvp.Value.TrySetException(new OperationCanceledException($"Peer cancelled all in-flight requests (CancelAll) — request {kvp.Key}."));
            }
        }

        /// <summary>
        /// Sends an outbound <see cref="PayloadTypes.CancelStream"/> frame for the given stream
        /// id. The frame carries a zero-length payload with End=true.
        /// </summary>
        /// <param name="streamId">The stream identifier to cancel.</param>
        /// <param name="cancellationToken">A cancellation token observed while writing the frame.</param>
        /// <returns>A task that completes when the cancel frame has been written.</returns>
        internal Task SendCancelStreamAsync(Guid streamId, CancellationToken cancellationToken = default)
            => SendSingleFrameAsync(PayloadTypes.CancelStream, streamId, [], end: true, cancellationToken);

        /// <summary>
        /// Sends an outbound <see cref="PayloadTypes.CancelAll"/> frame. The frame carries a
        /// zero-length payload with End=true; the id field is unused per the wire format.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token observed while writing the frame.</param>
        /// <returns>A task that completes when the cancel-all frame has been written.</returns>
        internal Task SendCancelAllAsync(CancellationToken cancellationToken = default)
            => SendSingleFrameAsync(PayloadTypes.CancelAll, Guid.Empty, [], end: true, cancellationToken);

        private void HandleResponseFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, ResponsePayload> pendingResponses)
        {
            if (payload == null)
            {
                return;
            }

            var responsePayload = JsonSerializer.Deserialize<ResponsePayload>(payload);
            if (responsePayload == null)
            {
                return;
            }

            _logger.LogDebug(
                "NamedPipeProtocol: Inbound Response id={Id} status={Status} streamCount={StreamCount} payloadBytes={PayloadLen}",
                header.Id, responsePayload.StatusCode,
                responsePayload.Streams?.Count ?? 0, payload.Length);

            TrackExpectedStreamLengths(responsePayload.Streams, header.Id, expectedStreamLengths);

            if (responsePayload.Streams is { Count: > 0 }
                && !AllStreamsComplete(responsePayload.Streams, header.Id, completedStreams))
            {
                pendingResponses[header.Id] = responsePayload;
                return;
            }

            var (assembledBody, contentType, attachments) = ExtractResponsePayload(header.Id, responsePayload, streamBuffers, completedStreams, expectedStreamLengths);
            CompleteResponse(header.Id, responsePayload, assembledBody, contentType, attachments);
        }

        private void TryCompletePendingResponses(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, ResponsePayload> pendingResponses)
        {
            if (pendingResponses.Count == 0)
            {
                return;
            }

            var completed = new List<Guid>();
            foreach (var (requestId, responsePayload) in pendingResponses)
            {
                if (responsePayload.Streams is { Count: > 0 }
                    && AllStreamsComplete(responsePayload.Streams, requestId, completedStreams))
                {
                    completed.Add(requestId);
                    var (body, contentType, attachments) = ExtractResponsePayload(requestId, responsePayload, streamBuffers, completedStreams, expectedStreamLengths);
                    CompleteResponse(requestId, responsePayload, body, contentType, attachments);
                }
            }

            foreach (var id in completed)
            {
                pendingResponses.Remove(id);
            }
        }

        private (byte[] Body, string ContentType, List<NamedPipeAttachment> Attachments) ExtractResponsePayload(
            Guid requestId,
            ResponsePayload responsePayload,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths)
        {
            if (responsePayload.Streams is not { Count: > 0 })
            {
                return (null, "application/json", []);
            }

            var primary = responsePayload.Streams[0];
            var body = TakeStreamBody(primary, requestId, streamBuffers, completedStreams, expectedStreamLengths);
            var attachments = TakeAttachmentStreams(responsePayload.Streams, requestId, streamBuffers, completedStreams, expectedStreamLengths);
            var contentType = string.IsNullOrEmpty(primary?.ContentType) ? "application/json" : primary.ContentType;
            return (body, contentType, attachments);
        }

        private void CompleteResponse(Guid requestId, ResponsePayload responsePayload, byte[] body, string contentType, List<NamedPipeAttachment> attachments)
        {
            var receiveResponse = new ReceiveResponse
            {
                StatusCode = responsePayload.StatusCode,
                Body = body,
                ContentType = string.IsNullOrEmpty(contentType) ? "application/json" : contentType,
                Attachments = attachments ?? [],
            };

            lock (_pendingRequests)
            {
                if (_pendingRequests.TryGetValue(requestId, out var tcs))
                {
                    tcs.TrySetResult(receiveResponse);
                    _pendingRequests.Remove(requestId);
                }
                else
                {
                    _logger.LogWarning("NamedPipeProtocol: Received response for unknown request {Id}.", requestId);
                }
            }
        }

        private static bool HandleStreamFrame(Header header, byte[] payload,
            Dictionary<Guid, MemoryStream> streamBuffers, HashSet<Guid> completedStreams, ILogger logger)
        {
            if (!streamBuffers.TryGetValue(header.Id, out var ms))
            {
                if (streamBuffers.Count >= MaxStreamBuffers)
                {
                    logger.LogError("NamedPipeProtocol: Maximum stream buffer count ({Max}) exceeded. Disconnecting.", MaxStreamBuffers);
                    return false;
                }

                ms = new MemoryStream();
                streamBuffers[header.Id] = ms;
            }

            if (payload != null && payload.Length > 0)
            {
                // Bound cumulative per-stream memory; a hostile/misbehaving peer could otherwise
                // send Stream frames against the same id forever (each frame within MaxPayloadSize)
                // and grow this MemoryStream without limit.
                if (ms.Length + payload.Length > MaxStreamSize)
                {
                    logger.LogError(
                        "NamedPipeProtocol: Stream {Id} exceeded maximum size ({Max} bytes). Disconnecting.",
                        header.Id,
                        MaxStreamSize);
                    return false;
                }

                ms.Write(payload);
            }

            // The End flag signals that all chunks for this stream have been received.
            // Until then the body is incomplete and must not be dispatched.
            if (header.End)
            {
                completedStreams.Add(header.Id);
            }

            return true;
        }

        private async Task SendResponseAsync(Guid requestId, NamedPipeResponse response, CancellationToken cancellationToken)
        {
            var bodyStreamId = response.Body != null ? Guid.NewGuid() : (Guid?)null;
            var attachmentIds = MaterializeAttachmentIds(response.Attachments);

            var responsePayload = new ResponsePayload
            {
                StatusCode = response.StatusCode,
                Streams = BuildStreamDescriptors(bodyStreamId, response.Body?.Length, response.Attachments, attachmentIds, response.ContentType),
            };

            var payloadJson = JsonSerializer.SerializeToUtf8Bytes(responsePayload);

            // End is per-payload-id (Bot.Streaming framing). The Response payload-id
            // carries only the JSON envelope — its body bytes travel under their own
            // stream id. The JSON is complete in this single frame, so End=true.
            await SendSingleFrameAsync(PayloadTypes.Response, requestId, payloadJson, end: true, cancellationToken).ConfigureAwait(false);

            if (response.Body != null)
            {
                await SendStreamFramesAsync(bodyStreamId.Value, response.Body, end: true, cancellationToken).ConfigureAwait(false);
            }

            await SendAttachmentFramesAsync(response.Attachments, attachmentIds, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates a wire identifier (or reuses an attachment-supplied one) for each
        /// attachment so the same id can be embedded in the <see cref="PayloadDescription"/>
        /// envelope and reused for the corresponding <see cref="PayloadTypes.Stream"/> frame.
        /// </summary>
        private static Guid[] MaterializeAttachmentIds(IList<NamedPipeAttachment> attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return [];
            }

            var ids = new Guid[attachments.Count];
            for (int i = 0; i < attachments.Count; i++)
            {
                var attachment = attachments[i];
                ids[i] = !string.IsNullOrEmpty(attachment?.Id) && Guid.TryParse(attachment.Id, out var parsed)
                    ? parsed
                    : Guid.NewGuid();
            }

            return ids;
        }

        /// <summary>
        /// Builds the Streams[] descriptor list that advertises the primary body and any
        /// attachments to the peer. Returns null when there are no streams to advertise so
        /// the JSON property is omitted on the wire (matches Bot.Streaming behavior).
        /// </summary>
        private static List<PayloadDescription> BuildStreamDescriptors(
            Guid? bodyStreamId,
            int? bodyLength,
            IList<NamedPipeAttachment> attachments,
            Guid[] attachmentIds,
            string primaryContentType)
        {
            int totalStreams = (bodyStreamId.HasValue ? 1 : 0) + (attachmentIds?.Length ?? 0);
            if (totalStreams == 0)
            {
                return null;
            }

            var descriptors = new List<PayloadDescription>(totalStreams);

            if (bodyStreamId.HasValue)
            {
                descriptors.Add(new PayloadDescription
                {
                    Id = bodyStreamId.Value.ToString("D"),
                    ContentType = string.IsNullOrEmpty(primaryContentType) ? "application/json" : primaryContentType,
                    Length = bodyLength,
                });
            }

            if (attachments != null)
            {
                for (int i = 0; i < attachments.Count; i++)
                {
                    var attachment = attachments[i];
                    descriptors.Add(new PayloadDescription
                    {
                        Id = attachmentIds[i].ToString("D"),
                        ContentType = string.IsNullOrEmpty(attachment?.ContentType) ? "application/octet-stream" : attachment.ContentType,
                        Length = attachment?.Body?.Length ?? 0,
                    });
                }
            }

            return descriptors;
        }

        private async Task SendAttachmentFramesAsync(
            IList<NamedPipeAttachment> attachments,
            Guid[] attachmentIds,
            CancellationToken cancellationToken)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            for (int i = 0; i < attachments.Count; i++)
            {
                var body = attachments[i]?.Body ?? [];
                await SendStreamFramesAsync(attachmentIds[i], body, end: true, cancellationToken).ConfigureAwait(false);
            }
        }

        // Matches Bot.Streaming TransportConstants.MaxPayloadLength. Frame payloads larger
        // than this are split into chunks for PayloadTypes.Stream only; request/response JSON
        // envelopes are single frames in Bot.Connector.Streaming.
        internal const int MaxPayloadLength = 4096;

        private async Task SendSingleFrameAsync(char type, Guid id, byte[] payload, bool end, CancellationToken cancellationToken)
        {
            payload ??= [];
            if (payload.Length > MaxPayloadSize)
            {
                throw new InvalidOperationException($"Payload length {payload.Length} exceeds the maximum frame size {MaxPayloadSize}.");
            }

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await SendFrameCoreAsync(type, id, payload, payload.Length, offset: 0, end, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A write failure means the outgoing pipe is broken. Cancel the read loop so
                // the hosted service observes Completion and reconnects (the old polling code
                // relied on connection.IsConnected to detect this for the outgoing pipe).
                _logger.LogWarning(ex, "NamedPipeProtocol: Write failed; tearing down protocol to trigger reconnect.");
                try { _cts?.Cancel(); } catch { /* already disposed */ }
                throw;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task SendStreamFramesAsync(Guid id, byte[] payload, bool end, CancellationToken cancellationToken)
        {
            payload ??= [];
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Split into MaxPayloadLength-sized frames. Empty payloads still emit one
                // frame (a zero-length header with End=end) so peers see payload completion.
                int offset = 0;
                do
                {
                    int chunkLen = Math.Min(MaxPayloadLength, payload.Length - offset);
                    bool isLast = offset + chunkLen >= payload.Length;
                    await SendFrameCoreAsync(PayloadTypes.Stream, id, payload, chunkLen, offset, isLast && end, cancellationToken).ConfigureAwait(false);
                    offset += chunkLen;
                }
                while (offset < payload.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A write failure means the outgoing pipe is broken. Cancel the read loop so
                // the hosted service observes Completion and reconnects (the old polling code
                // relied on connection.IsConnected to detect this for the outgoing pipe).
                _logger.LogWarning(ex, "NamedPipeProtocol: Write failed; tearing down protocol to trigger reconnect.");
                try { _cts?.Cancel(); } catch { /* already disposed */ }
                throw;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task SendFrameCoreAsync(
            char type,
            Guid id,
            byte[] payload,
            int payloadLength,
            int offset,
            bool end,
            CancellationToken cancellationToken)
        {
            if (!_writer.IsConnected)
            {
                throw new IOException("Named pipe writer is disconnected.");
            }

            var header = new Header
            {
                Type = type,
                PayloadLength = payloadLength,
                Id = id,
                End = end,
            };

            var headerBytes = HeaderSerializer.Serialize(header);

            _logger.LogDebug("NamedPipeProtocol: SEND frame type={Type} id={Id} len={Len} end={End}",
                type, id, payloadLength, end);

            await _writer.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            if (payloadLength > 0)
            {
                await _writer.WriteAsync(new ReadOnlyMemory<byte>(payload, offset, payloadLength), cancellationToken).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            // Best-effort: tell the peer we're shutting down so it can release any in-flight
            // dispatches on its side immediately rather than waiting for the pipe to break.
            // Cap the wait so a wedged peer (e.g., not reading the pipe) can't hold dispose
            // open indefinitely.
            try
            {
                await SendCancelAllAsync(CancellationToken.None)
                    .WaitAsync(TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
            }
            catch
            {
                // The pipe may already be gone, or the write may have timed out — that's fine;
                // the read-loop break will be observed once _cts is cancelled below.
            }

            _cts?.Cancel();

            if (_readLoop != null)
            {
                try
                {
                    await _readLoop.ConfigureAwait(false);
                }
                catch
                {
                    // Suppress exceptions during shutdown
                }
            }

            // Defensive: if Start() was never called, the read loop's finally never ran,
            // so make sure any pending caller is released.
            FailPendingRequests(new ObjectDisposedException(nameof(NamedPipeProtocol)));

            // Wait for any in-flight dispatched request handlers to finish before
            // tearing down the write lock they depend on.
            Task[] pendingDispatches;
            lock (_dispatchedTasksLock)
            {
                pendingDispatches = [.. _dispatchedTasks];
            }

            if (pendingDispatches.Length > 0)
            {
                try
                {
                    await Task.WhenAll(pendingDispatches).ConfigureAwait(false);
                }
                catch
                {
                    // Individual dispatch failures are already logged via DispatchRequestSafeAsync.
                }
            }

            _cts?.Dispose();

            // Note: _writeLock (SemaphoreSlim) is intentionally not disposed. An outbound
            // SendRequestAsync captured before SetProtocol(null) may still be inside a
            // send helper at WaitAsync/Release time when DisposeAsync runs. Disposing
            // the semaphore would race with that caller and surface as ObjectDisposedException
            // on every reconnect. SemaphoreSlim only requires explicit disposal when its
            // AvailableWaitHandle has been materialized (it lazily allocates a kernel
            // handle), which this class never does — so leaving it to GC is safe.
        }
    }
}
