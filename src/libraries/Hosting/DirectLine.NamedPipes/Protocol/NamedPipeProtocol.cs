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
        /// Maximum payload size per frame. The wire header encodes length as 6 ASCII digits,
        /// so 999,999 is the protocol ceiling.
        /// </summary>
        private const int MaxPayloadSize = 999_999;

        /// <summary>
        /// Maximum time (seconds) a pending dispatch waits for streams before force-dispatching.
        /// Prevents a misbehaving peer from permanently blocking the pipe.
        /// </summary>
        internal const int PendingDispatchTimeoutSeconds = 15;

        /// <summary>
        /// Maximum number of concurrent stream buffers. Limits memory usage from
        /// unmatched stream frames.
        /// </summary>
        private const int MaxStreamBuffers = 100;

        /// <summary>
        /// Outbound stream chunk size (64 KB). Matches the JS SDK and reduces DLFlex
        /// write-lock contention vs the legacy 4 KB value.
        /// </summary>
        internal const int MaxSendStreamChunkSize = 65_536;

        /// <summary>
        /// Maximum time (seconds) for draining trailing unframed bytes. If exceeded, the
        /// connection is torn down (partial drain corrupts framing).
        /// </summary>
        private const int TrailingByteDrainTimeoutSeconds = 10;

        /// <summary>
        /// Maximum cumulative bytes buffered per stream id. Bounds memory from a misbehaving
        /// peer that never sets End. 100 MiB covers expected payloads; worst-case total is
        /// MaxStreamBuffers × MaxStreamSize ≈ 10 GiB before disconnect.
        /// </summary>
        private const int MaxStreamSize = 100 * 1024 * 1024;

        private readonly NamedPipeTransport _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        private readonly NamedPipeTransport _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly Dictionary<Guid, TaskCompletionSource<ReceiveResponse>> _pendingRequests = [];
        private readonly object _dispatchedTasksLock = new();
        private readonly List<Task> _dispatchedTasks = [];

        // ConcurrentDictionary: dispatch-completion continuations remove entries from the
        // thread pool while the read loop may read/cancel from the main loop.
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _inflightDispatches = new();

        private CancellationTokenSource _cts;
        private volatile Task _readLoop;
        private int _disposed;
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
        /// Send a request with optional multi-stream attachments and wait for a response.
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

                // The Request frame carries only the JSON envelope; body bytes travel
                // under their own stream id.
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
                    // OCE from the TCS itself (e.g., inbound CancelAll); propagate as-is.
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
            var pendingDispatches = new Dictionary<Guid, (Header header, RequestPayload payload, long createdTicks)>();
            var pendingResponses = new Dictionary<Guid, ResponsePayload>();


            try
            {
                var connected = true;
                while (!cancellationToken.IsCancellationRequested && _reader.IsConnected && connected)
                {
                    // Periodic read timeout sweeps stale pending dispatches.
                    bool gotFrame;
                    if (pendingDispatches.Count > 0)
                    {
                        using var readTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        readTimeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
                        try
                        {
                            gotFrame = await _reader.ReadExactAsync(headerBuffer, HeaderSerializer.HeaderSize, readTimeoutCts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        {
                            // Read timeout — no frame arrived in 5s. Sweep stale pending dispatches.
                            await TryDispatchPendingRequestsAsync(streamBuffers, completedStreams, expectedStreamLengths, pendingDispatches, cancellationToken).ConfigureAwait(false);
                            TryCompletePendingResponses(streamBuffers, completedStreams, expectedStreamLengths, pendingResponses);
                            continue;
                        }
                    }
                    else
                    {
                        gotFrame = await _reader.ReadExactAsync(headerBuffer, HeaderSerializer.HeaderSize, cancellationToken).ConfigureAwait(false);
                    }

                    if (!gotFrame)
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

                            // Trailing-byte drain (DirectLineFlex compat).
                            // DLFlex may mark End=true prematurely and write remaining bytes
                            // unframed. Probe with a short read: if bytes arrive immediately,
                            // drain them; if not, the descriptor was wrong.
                            if (connected && header.End
                                && expectedStreamLengths.TryGetValue(header.Id, out var expectedLen)
                                && streamBuffers.TryGetValue(header.Id, out var streamMs)
                                && streamMs.Length < expectedLen)
                            {
                                var missing = (int)(expectedLen - streamMs.Length);
                                if (expectedLen <= MaxStreamSize)
                                {
                                    // Probe: read 1 byte with 20ms timeout. DLFlex trailing bytes
                                    // are synchronously available if they exist.
                                    var probeBuf = new byte[1];
                                    int probeRead;
                                    using (var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                                    {
                                        probeCts.CancelAfter(TimeSpan.FromMilliseconds(20));
                                        try
                                        {
                                            probeRead = await _reader.ReadSingleAsync(probeBuf, probeCts.Token).ConfigureAwait(false);
                                        }
                                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                                        {
                                            probeRead = -1; // Timeout — no trailing bytes
                                        }
                                    }

                                    if (probeRead > 0)
                                    {
                                        // Trailing bytes confirmed — drain the remainder.
                                        _logger.LogInformation(
                                            "NamedPipeProtocol: Stream {Id} ended with {Received} bytes but descriptor expects {Expected}; probe detected trailing bytes — draining {Missing} bytes (DirectLineFlex compat).",
                                            header.Id, streamMs.Length, expectedLen, missing);

                                        // Write probe byte into the stream buffer
                                        streamMs.Write(probeBuf, 0, 1);
                                        var remaining = missing - 1;

                                        if (remaining > 0)
                                        {
                                            // Drain in fixed-size chunks to avoid LOH pressure for large streams.
                                            var drainChunk = new byte[Math.Min(remaining, MaxSendStreamChunkSize)];
                                            bool drainFailed = false;
                                            using (var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                                            {
                                                drainCts.CancelAfter(TimeSpan.FromSeconds(TrailingByteDrainTimeoutSeconds));
                                                try
                                                {
                                                    int left = remaining;
                                                    while (left > 0)
                                                    {
                                                        int toRead = Math.Min(left, drainChunk.Length);
                                                        if (!await _reader.ReadExactAsync(drainChunk, toRead, drainCts.Token).ConfigureAwait(false))
                                                        {
                                                            _logger.LogWarning("NamedPipeProtocol: Pipe disconnected during trailing-byte drain for stream {Id}.", header.Id);
                                                            drainFailed = true;
                                                            break;
                                                        }
                                                        streamMs.Write(drainChunk, 0, toRead);
                                                        left -= toRead;
                                                    }
                                                }
                                                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                                                {
                                                    _logger.LogError(
                                                        "NamedPipeProtocol: Trailing-byte drain for stream {Id} timed out after {Timeout}s (remaining={Remaining}). Disconnecting.",
                                                        header.Id, TrailingByteDrainTimeoutSeconds, remaining);
                                                    drainFailed = true;
                                                }
                                            }

                                            if (drainFailed)
                                            {
                                                connected = false;
                                                break;
                                            }
                                        }

                                        _logger.LogInformation(
                                            "NamedPipeProtocol: Stream {Id} drained {Missing} trailing bytes — now {Total} bytes total.",
                                            header.Id, missing, streamMs.Length);
                                    }
                                    else if (probeRead == 0)
                                    {
                                        _logger.LogWarning("NamedPipeProtocol: Pipe disconnected during probe read for stream {Id}.", header.Id);
                                        connected = false;
                                    }
                                    else
                                    {
                                        // Probe timed out — no trailing bytes; descriptor was wrong
                                        // or DLFlex's semaphore timeout dropped remaining frames.
                                        _logger.LogDebug(
                                            "NamedPipeProtocol: Stream {Id} ended with {Received}/{Expected} bytes; no trailing bytes detected (descriptor mismatch, frames may have been dropped).",
                                            header.Id, streamMs.Length, expectedLen);
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning(
                                        "NamedPipeProtocol: Stream {Id} expected length {Expected} exceeds MaxStreamSize; skipping drain.",
                                        header.Id, expectedLen);
                                }
                            }

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
                FailPendingRequests(new IOException("Named pipe disconnected."));

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
            Dictionary<Guid, (Header header, RequestPayload payload, long createdTicks)> pendingDispatches,
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
                    pendingDispatches[header.Id] = (header, requestPayload, Environment.TickCount64);
                    return;
                }
            }

            // Extract body and attachments on the read-loop thread (no concurrent access to streamBuffers).
            var (body, contentType, attachments) = ExtractRequestPayload(header, requestPayload, streamBuffers, completedStreams, expectedStreamLengths);
            StartDispatch(header, requestPayload, body, contentType, attachments, cancellationToken);
        }

        /// <summary>
        /// Returns true when every stream referenced by the Streams[] descriptors has End=true.
        /// Malformed ids are treated as complete to avoid blocking dispatch forever.
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
        /// Extracts the primary body and attachment streams for a request. Streams[0] is the
        /// body; Streams[1..N] are attachments. Must only be called on the read-loop thread.
        /// </summary>
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
        /// Removes and returns the assembled bytes for a stream descriptor, or null if not found.
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
        /// Removes and returns <see cref="NamedPipeAttachment"/> instances for Streams[1..N].
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
                else
                {
                    _logger.LogWarning(
                        "NamedPipeProtocol: TakeAttachmentStreams — no buffer found for stream {Id} (descriptor[{Index}], type={ContentType}, declaredLen={Len}). completedStreams.Contains={InCompleted}, activeBufferCount={Count}.",
                        attachmentId, i, descriptor.ContentType, descriptor.Length,
                        completedStreams.Contains(attachmentId),
                        streamBuffers.Count);
                }

                completedStreams.Remove(attachmentId);
                expectedStreamLengths.Remove(attachmentId);

                attachments.Add(new NamedPipeAttachment
                {
                    Id = descriptor.Id,
                    ContentType = descriptor.ContentType ?? string.Empty,
                    Body = body,
                });

                _logger.LogDebug(
                    "NamedPipeProtocol: TakeAttachmentStreams — extracted stream {Id} with {Len} bytes (descriptor declared {DeclaredLen}).",
                    attachmentId, body.Length, descriptor.Length);
            }

            return attachments;
        }

        private void TrackExpectedStreamLengths(
            List<PayloadDescription> streams,
            Guid ownerId,
            Dictionary<Guid, int> expectedStreamLengths)
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
            Dictionary<Guid, (Header header, RequestPayload payload, long createdTicks)> pendingDispatches,
            CancellationToken cancellationToken)
        {
            var dispatched = new List<Guid>();
            var now = Environment.TickCount64;

            foreach (var (requestId, pending) in pendingDispatches)
            {
                if (pending.payload.Streams is { Count: > 0 }
                    && AllStreamsComplete(pending.payload.Streams, requestId, completedStreams))
                {
                    dispatched.Add(requestId);
                    var (body, contentType, attachments) = ExtractRequestPayload(pending.header, pending.payload, streamBuffers, completedStreams, expectedStreamLengths);
                    StartDispatch(pending.header, pending.payload, body, contentType, attachments, cancellationToken);
                }
                else if (now - pending.createdTicks > PendingDispatchTimeoutSeconds * 1000L)
                {
                    // Streams never arrived; force-dispatch with partial data.
                    _logger.LogWarning(
                        "NamedPipeProtocol: Pending dispatch {Id} timed out after {Seconds}s waiting for streams. Force-dispatching with partial data.",
                        requestId, PendingDispatchTimeoutSeconds);
                    dispatched.Add(requestId);

                    // Mark all streams as complete so extraction succeeds
                    if (pending.payload.Streams != null)
                    {
                        foreach (var stream in pending.payload.Streams)
                        {
                            if (TryGetStreamId(stream?.Id, requestId, out var sid))
                            {
                                completedStreams.Add(sid);
                                // Ensure a buffer exists so TakeStreamBody can extract
                                if (!streamBuffers.ContainsKey(sid))
                                {
                                    streamBuffers[sid] = new MemoryStream();
                                }
                            }
                        }
                    }

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

            var handler = OnRequestReceived;
            var response = handler != null
                ? await handler(request, cancellationToken).ConfigureAwait(false)
                : NamedPipeResponse.NotFound();

            await SendResponseAsync(header.Id, response, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a per-request CTS linked to the read-loop token and starts the dispatch task.
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

            // Remove tracking after handler completes. We don't dispose requestCts here
            // because HandleCancelAllFrame may call Cancel() on it concurrently.
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
        /// Handles a CancelStream frame: drops buffered data, marks stream complete, and
        /// cancels the in-flight dispatch if the id matches a request.
        /// </summary>
        private void HandleCancelStreamFrame(
            Guid streamId,
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload, long createdTicks)> pendingDispatches)
        {
            if (streamBuffers.Remove(streamId, out var buf))
            {
                try { buf.Dispose(); } catch { /* benign */ }
            }

            // Mark as complete so pending dispatches can proceed with empty data.
            completedStreams.Add(streamId);
            expectedStreamLengths.Remove(streamId);

            // Cancel in-flight dispatch for this id.
            if (_inflightDispatches.TryGetValue(streamId, out var requestCts))
            {
                try { requestCts.Cancel(); } catch { /* already disposed */ }
            }

            // If the id is an undispatched request, drop it.
            if (pendingDispatches.Remove(streamId))
            {
                _logger.LogInformation("NamedPipeProtocol: Dropped pending request {Id} due to CancelStream.", streamId);
            }
        }

        /// <summary>
        /// Handles a CancelAll frame: clears all state, cancels in-flight dispatches, and
        /// fails all pending outbound requests.
        /// </summary>
        private void HandleCancelAllFrame(
            Dictionary<Guid, MemoryStream> streamBuffers,
            HashSet<Guid> completedStreams,
            Dictionary<Guid, int> expectedStreamLengths,
            Dictionary<Guid, (Header header, RequestPayload payload, long createdTicks)> pendingDispatches,
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

            // Fail all outbound pending requests.
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

            if (header.End)
            {
                completedStreams.Add(header.Id);
                logger.LogDebug(
                    "NamedPipeProtocol: Stream {Id} complete. TotalBytes={TotalBytes}, PayloadThisFrame={FrameLen}, End=true.",
                    header.Id, ms.Length, payload?.Length ?? 0);
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

            // Response frame carries only the JSON envelope; body travels under its own stream id.
            await SendSingleFrameAsync(PayloadTypes.Response, requestId, payloadJson, end: true, cancellationToken).ConfigureAwait(false);

            if (response.Body != null)
            {
                await SendStreamFramesAsync(bodyStreamId.Value, response.Body, end: true, cancellationToken).ConfigureAwait(false);
            }

            await SendAttachmentFramesAsync(response.Attachments, attachmentIds, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates (or reuses) a wire id for each attachment.
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
        /// Builds the Streams[] descriptor list for the wire envelope. Returns null when empty
        /// so the JSON property is omitted (matches Bot.Streaming behavior).
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

        // Legacy 4 KB chunk size (retained for test interop validation).
        internal const int MaxPayloadLength = 4096;

        private async Task SendSingleFrameAsync(char type, Guid id, byte[] payload, bool end, CancellationToken cancellationToken)
        {
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
                // Write failure → pipe broken; cancel read loop to trigger reconnect.
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
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // 64 KB chunks; empty payloads emit one zero-length frame.
                int offset = 0;
                do
                {
                    int chunkLen = Math.Min(MaxSendStreamChunkSize, payload.Length - offset);
                    bool isLast = offset + chunkLen >= payload.Length;
                    await SendFrameCoreAsync(PayloadTypes.Stream, id, payload, chunkLen, offset, isLast && end, cancellationToken).ConfigureAwait(false);
                    offset += chunkLen;
                }
                while (offset < payload.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Write failure → pipe broken; cancel read loop to trigger reconnect.
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
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            // Best-effort shutdown notification with a real token so a wedged pipe releases _writeLock.
            try
            {
                using var cancelAllCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await SendCancelAllAsync(cancelAllCts.Token).ConfigureAwait(false);
            }
            catch
            {
                // Pipe may already be gone or write timed out.
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

            // If Start() was never called, release any pending callers.
            FailPendingRequests(new ObjectDisposedException(nameof(NamedPipeProtocol)));

            // Wait for in-flight dispatched handlers before tearing down.
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

            // _writeLock is intentionally not disposed: in-flight SendRequestAsync callers
            // captured before SetProtocol(null) may still be inside WaitAsync/Release.
            // SemaphoreSlim only needs explicit disposal when AvailableWaitHandle is used.
        }
    }
}
