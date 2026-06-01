// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Tests for per-stream <c>CancelStream</c> (PayloadType 'C') and <c>CancelAll</c> ('X')
    /// frames defined by Bot.Streaming TransportConstants. The protocol must:
    ///   - accept inbound CancelStream/CancelAll and clean up buffered state,
    ///   - cancel in-flight inbound dispatches whose request id matches CancelStream's id,
    ///   - fail outbound pending requests on CancelAll with <see cref="OperationCanceledException"/>,
    ///   - emit a CancelAll frame on dispose so peers release in-flight handlers immediately.
    /// </summary>
    public class NamedPipeProtocolCancelTests
    {
        // ----- Outbound emit semantics -----

        [Fact]
        public async Task OutboundSendCancelAllAsync_EmitsCancelAllFrame_EmptyEndTrue()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();

            await harness.Protocol.SendCancelAllAsync();

            var frame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.CancelAll, frame.Header.Type);
            Assert.Equal(0, frame.Header.PayloadLength);
            Assert.True(frame.Header.End);
        }

        [Fact]
        public async Task OutboundSendCancelStreamAsync_EmitsCancelStreamFrame_WithProvidedId()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var streamId = Guid.NewGuid();

            await harness.Protocol.SendCancelStreamAsync(streamId);

            var frame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.CancelStream, frame.Header.Type);
            Assert.Equal(streamId, frame.Header.Id);
            Assert.Equal(0, frame.Header.PayloadLength);
            Assert.True(frame.Header.End);
        }

        // ----- Inbound CancelStream semantics -----

        [Fact]
        public async Task InboundCancelStream_ForPendingPrimaryStream_DispatchesWithEmptyBody()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var received = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };

            var requestId = Guid.NewGuid();
            var primaryStreamId = Guid.NewGuid();
            // Declare a 1024-byte primary body, but cancel the stream before any Stream frame arrives.
            var requestJson = JsonSerializer.SerializeToUtf8Bytes(new RequestPayload
            {
                Verb = "POST",
                Path = "/api/messages",
                Streams = new List<PayloadDescription>
                {
                    new() { Id = primaryStreamId.ToString("D"), ContentType = "application/json", Length = 1024 },
                },
            });

            await harness.WriteInboundFrameAsync(PayloadTypes.Request, requestId, requestJson, end: true);
            await harness.WriteInboundFrameAsync(PayloadTypes.CancelStream, primaryStreamId, payload: null, end: true);

            // Cancel marks the stream "complete", which unblocks the pending dispatch with no body
            // bytes (TakeStreamBody returns null when nothing was ever buffered for the stream).
            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(req.Body == null || req.Body.Length == 0);

            // Drain the outbound 200 OK so dispose doesn't deadlock.
            var responseFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Response, responseFrame.Header.Type);
        }

        [Fact]
        public async Task InboundCancelStream_ForInflightRequestId_CancelsHandler()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            harness.Protocol.OnRequestReceived = async (req, ct) =>
            {
                handlerStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
                catch (OperationCanceledException)
                {
                    handlerCancelled.TrySetResult(true);
                    throw;
                }
                return NamedPipeResponse.OK();
            };

            // Send a request with no body so it dispatches immediately.
            var requestId = Guid.NewGuid();
            var requestJson = JsonSerializer.SerializeToUtf8Bytes(new RequestPayload
            {
                Verb = "POST",
                Path = "/api/messages",
                Streams = null,
            });
            await harness.WriteInboundFrameAsync(PayloadTypes.Request, requestId, requestJson, end: true);

            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // CancelStream against the request id must cancel the in-flight handler.
            await harness.WriteInboundFrameAsync(PayloadTypes.CancelStream, requestId, payload: null, end: true);

            var cancelled = await handlerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(cancelled);
        }

        // ----- Inbound CancelAll semantics -----

        [Fact]
        public async Task InboundCancelAll_FailsOutboundPendingRequest_WithOperationCanceled()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();

            // Outbound request that will never get a response — until CancelAll arrives.
            var requestTask = harness.Protocol.SendRequestAsync(
                "GET",
                "/v3/conversations",
                body: null,
                CancellationToken.None);

            // Consume the outbound Request frame so the test pipe doesn't fill.
            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);

            // Send CancelAll inbound.
            await harness.WriteInboundFrameAsync(PayloadTypes.CancelAll, Guid.Empty, payload: null, end: true);

            // The pending outbound request task should now fault with OperationCanceledException.
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await requestTask.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public async Task InboundCancelAll_CancelsInflightInboundDispatch()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            harness.Protocol.OnRequestReceived = async (req, ct) =>
            {
                handlerStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
                catch (OperationCanceledException)
                {
                    handlerCancelled.TrySetResult(true);
                    throw;
                }
                return NamedPipeResponse.OK();
            };

            var requestId = Guid.NewGuid();
            var requestJson = JsonSerializer.SerializeToUtf8Bytes(new RequestPayload
            {
                Verb = "POST",
                Path = "/api/messages",
                Streams = null,
            });
            await harness.WriteInboundFrameAsync(PayloadTypes.Request, requestId, requestJson, end: true);

            await handlerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await harness.WriteInboundFrameAsync(PayloadTypes.CancelAll, Guid.Empty, payload: null, end: true);

            var cancelled = await handlerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(cancelled);
        }

        // ----- Dispose emits CancelAll -----

        [Fact]
        public async Task DisposeAsync_EmitsCancelAllFrame_BestEffort()
        {
            var harness = await FrameInspectorHarness.CreateAsync();

            // Dispose runs synchronously in the harness's Dispose(); spin it off so we can read.
            var disposeTask = Task.Run(async () => await harness.Protocol.DisposeAsync());

            // First frame written by DisposeAsync should be CancelAll.
            var frame = await harness.ReadFrameAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(PayloadTypes.CancelAll, frame.Header.Type);

            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
            harness.DisposePipesOnly();
        }

        // ----- Harness -----

        private sealed class FrameInspectorHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;

            public NamedPipeProtocol Protocol { get; }

            private FrameInspectorHarness(
                AnonymousPipeServerStream inboundServer,
                AnonymousPipeClientStream inboundClient,
                AnonymousPipeServerStream outboundServer,
                AnonymousPipeClientStream outboundClient)
            {
                _inboundServer = inboundServer;
                _inboundClient = inboundClient;
                _outboundServer = outboundServer;
                _outboundClient = outboundClient;

                Protocol = new NamedPipeProtocol(
                    new NamedPipeTransport(inboundServer),
                    new NamedPipeTransport(outboundServer),
                    NullLogger.Instance);
            }

            public static Task<FrameInspectorHarness> CreateAsync()
            {
                var inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                var inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, inboundServer.GetClientHandleAsString());
                var outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                var outboundClient = new AnonymousPipeClientStream(PipeDirection.In, outboundServer.GetClientHandleAsString());

                var h = new FrameInspectorHarness(inboundServer, inboundClient, outboundServer, outboundClient);
                h.Protocol.Start();
                return Task.FromResult(h);
            }

            public async Task<(Header Header, byte[] Payload)> ReadFrameAsync()
            {
                var headerBuf = await ReadExactAsync(_outboundClient, HeaderSerializer.HeaderSize);
                var header = HeaderSerializer.Deserialize(headerBuf);
                var payload = header.PayloadLength > 0
                    ? await ReadExactAsync(_outboundClient, header.PayloadLength)
                    : Array.Empty<byte>();
                return (header, payload);
            }

            public Task WriteInboundFrameAsync(char type, Guid id, byte[] payload, bool end)
            {
                var header = new Header
                {
                    Type = type,
                    Id = id,
                    PayloadLength = payload?.Length ?? 0,
                    End = end,
                };
                var headerBuf = HeaderSerializer.Serialize(header);
                return WriteAllAsync(_inboundClient, headerBuf, payload);
            }

            private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
            {
                var buf = new byte[count];
                int read = 0;
                while (read < count)
                {
                    var n = await stream.ReadAsync(buf.AsMemory(read, count - read)).ConfigureAwait(false);
                    if (n == 0)
                    {
                        throw new EndOfStreamException($"Pipe closed before {count} bytes were read (got {read}).");
                    }
                    read += n;
                }
                return buf;
            }

            private static async Task WriteAllAsync(Stream stream, byte[] header, byte[] payload)
            {
                await stream.WriteAsync(header).ConfigureAwait(false);
                if (payload != null && payload.Length > 0)
                {
                    await stream.WriteAsync(payload).ConfigureAwait(false);
                }
                await stream.FlushAsync().ConfigureAwait(false);
            }

            public void DisposePipesOnly()
            {
                try { _inboundClient.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
            }

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                DisposePipesOnly();
            }
        }
    }
}
