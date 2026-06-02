// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Verifies the protocol assembles multi-frame stream payloads correctly before dispatching
    /// — guards against the truncation regression where a request body larger than one wire chunk
    /// (~4096 bytes per the Bot Framework streaming convention) would be deserialized after only
    /// the first chunk had arrived.
    /// </summary>
    public class NamedPipeProtocolStreamingTests
    {
        private const int FrameChunkSize = 4096;

        [Theory]
        [InlineData(100)]            // single small frame
        [InlineData(FrameChunkSize)] // exactly one frame
        [InlineData(FrameChunkSize + 1)]   // two frames (boundary)
        [InlineData(10_000)]         // three frames
        [InlineData(50_000)]         // many frames
        public async Task LargeRequestBody_AssembledFromMultipleStreamFrames(int bodyLength)
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(bodyLength);

            await harness.WriteRequestAsync(requestId, streamId, body.Length);
            await harness.WriteChunkedStreamAsync(streamId, body, FrameChunkSize);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body.Length, received.Length);
            Assert.Equal(body, received);
        }

        [Fact]
        public async Task Handler_NotInvoked_UntilEndOfStreamFlagSeen()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(10_000);

            await harness.WriteRequestAsync(requestId, streamId, body.Length);

            // Send all but the final frame — the handler MUST NOT fire yet.
            await harness.WriteChunkedStreamAsync(streamId, body[..(body.Length - 100)], FrameChunkSize, sendFinalEndFlag: false);

            var prematureDispatch = await Task.WhenAny(bodyReceived.Task, Task.Delay(300));
            Assert.NotSame(bodyReceived.Task, prematureDispatch);

            // Send the final chunk with End=true — handler should fire and see the full body.
            await harness.WriteFrameAsync(PayloadTypes.Stream, streamId, body[^100..], end: true);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body, received);
        }

        [Fact]
        public async Task StreamFrames_ArrivingBeforeRequestFrame_AreBuffered()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req.Body);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = MakePayload(10_000);

            // Stream frames first, then the request frame.
            await harness.WriteChunkedStreamAsync(streamId, body, FrameChunkSize);
            await harness.WriteRequestAsync(requestId, streamId, body.Length);

            var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(body, received);
        }

        [Fact]
        public async Task MultiStreamRequest_DispatchesPrimaryBody_AndDeliversAttachmentStreams()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var bodyReceived = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                bodyReceived.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            // Drive enough rounds that any per-request leak of attachment buffers would
            // trip the MaxStreamBuffers=100 force-disconnect.
            for (int round = 0; round < 50; round++)
            {
                bodyReceived = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
                harness.Protocol.OnRequestReceived = (req, _) =>
                {
                    bodyReceived.TrySetResult(req);
                    return Task.FromResult(NamedPipeResponse.OK());
                };

                var requestId = Guid.NewGuid();
                var primaryStreamId = Guid.NewGuid();
                var attachmentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
                var primaryBody = Encoding.UTF8.GetBytes($"activity-{round}");
                var attachmentBody = MakePayload(2048);

                await harness.WriteRequestAsync(requestId, primaryStreamId, primaryBody.Length, attachmentIds, attachmentBody.Length);

                // Send the attachment streams first, then the primary, to exercise the
                // "buffer attachments while waiting for primary completion" path.
                foreach (var attachmentId in attachmentIds)
                {
                    await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentId, attachmentBody, end: true);
                }

                await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId, primaryBody, end: true);

                var received = await bodyReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(primaryBody, received.Body);
                Assert.Equal(attachmentIds.Length, received.Attachments.Count);
                for (int i = 0; i < attachmentIds.Length; i++)
                {
                    Assert.Equal(attachmentIds[i].ToString("D"), received.Attachments[i].Id);
                    Assert.Equal(attachmentBody, received.Attachments[i].Body);
                }
            }
        }

        [Fact]
        public async Task MultiStreamRequest_AttachmentBytes_AreDeliveredIntact()
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var received = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var primaryStreamId = Guid.NewGuid();
            var attachmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var primaryBody = Encoding.UTF8.GetBytes("primary-activity");

            // Heterogeneous attachment payloads of different sizes
            var att0 = MakePayload(7000);
            var att1 = MakePayload(123);

            await harness.WriteRequestAsync(requestId, primaryStreamId, primaryBody.Length, attachmentIds, attachmentLength: 0);
            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId, primaryBody, end: true);
            await harness.WriteChunkedStreamAsync(attachmentIds[0], att0, FrameChunkSize);
            await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentIds[1], att1, end: true);

            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(primaryBody, req.Body);
            Assert.Equal(2, req.Attachments.Count);
            Assert.Equal(att0, req.Attachments[0].Body);
            Assert.Equal(att1, req.Attachments[1].Body);
        }

        /// <summary>
        /// Reproduces the Bot.Streaming / DirectLineFlex boundary bug where the sender
        /// declares an attachment stream's descriptor length > 4096 but only sends a
        /// single frame (len=4096, End=true) followed by the remaining bytes RAW (no framing).
        /// The drain logic must read those trailing bytes so the next request parses correctly.
        /// </summary>
        [Theory]
        [InlineData(4097)]     // just over the boundary — 1 trailing byte
        [InlineData(5000)]     // moderate trailing data
        [InlineData(8192)]     // exactly 2x the chunk size
        [InlineData(12000)]    // large overflow
        public async Task BotStreamingBoundaryBug_TrailingBytesAfterEndTrue_AreDrainedAndNextRequestWorks(int attachmentLength)
        {
            using var harness = await ProtocolHarness.CreateAsync();
            var tcs1 = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tcs2 = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            int callCount = 0;
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                var n = System.Threading.Interlocked.Increment(ref callCount);
                if (n == 1) tcs1.TrySetResult(req);
                else tcs2.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var attachmentData = MakePayload(attachmentLength);
            var primaryBody = Encoding.UTF8.GetBytes("{\"type\":\"message\"}");

            // --- First request: simulates the buggy sender behavior ---
            var req1Id = Guid.NewGuid();
            var primaryStreamId1 = Guid.NewGuid();
            var attachmentId1 = Guid.NewGuid();

            // Descriptor declares the REAL attachment length
            await harness.WriteRequestAsync(req1Id, primaryStreamId1, primaryBody.Length,
                new[] { attachmentId1 }, attachmentLength);

            // Primary body stream (small, single frame, fine)
            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId1, primaryBody, end: true);

            // Buggy attachment stream: header says len=4096, End=true, but then
            // raw trailing bytes follow without framing (simulates the DirectLineFlex bug)
            var framedPart = attachmentData[..FrameChunkSize];
            var trailingPart = attachmentData[FrameChunkSize..];
            await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentId1, framedPart, end: true);
            // Write trailing bytes RAW (no header) — this is what the sender bug does
            await harness.WriteRawBytesAsync(trailingPart);

            // --- Second request: must parse correctly despite the trailing bytes ---
            var req2Id = Guid.NewGuid();
            var primaryStreamId2 = Guid.NewGuid();
            var body2 = Encoding.UTF8.GetBytes("{\"type\":\"message\",\"text\":\"hello\"}");
            await harness.WriteRequestAsync(req2Id, primaryStreamId2, body2.Length);
            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId2, body2, end: true);

            // Both requests should dispatch successfully
            var r1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var r2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // First request: attachment contains the FULL data (framed + trailing)
            Assert.Single(r1.Attachments);
            Assert.Equal(attachmentLength, r1.Attachments[0].Body.Length);
            Assert.Equal(attachmentData, r1.Attachments[0].Body);

            // Second request: body parsed correctly (proves framing wasn't desynchronized)
            Assert.Equal(body2, r2.Body);
        }

        private static byte[] MakePayload(int length)
        {
            var data = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data[i] = (byte)('a' + (i % 26));
            }

            return data;
        }

        private sealed class ProtocolHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;
            private readonly Task _outboundDrain;
            private readonly CancellationTokenSource _drainCts;

            public NamedPipeProtocol Protocol { get; }

            private ProtocolHarness(
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

                // Drain the outbound pipe so the protocol's response writes never block.
                _drainCts = new CancellationTokenSource();
                _outboundDrain = Task.Run(async () =>
                {
                    var buf = new byte[4096];
                    try
                    {
                        while (await outboundClient.ReadAsync(buf, _drainCts.Token).ConfigureAwait(false) > 0)
                        {
                        }
                    }
                    catch
                    {
                        // pipe closed
                    }
                });
            }

            public static Task<ProtocolHarness> CreateAsync()
            {
                var inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                var inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, inboundServer.GetClientHandleAsString());
                var outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                var outboundClient = new AnonymousPipeClientStream(PipeDirection.In, outboundServer.GetClientHandleAsString());

                return Task.FromResult(new ProtocolHarness(inboundServer, inboundClient, outboundServer, outboundClient));
            }

            public void Start() => Protocol.Start();

            public Task WriteRequestAsync(Guid requestId, Guid streamId, int bodyLength)
                => WriteRequestAsync(requestId, streamId, bodyLength, attachmentStreamIds: null, attachmentLength: 0);

            public Task WriteRequestAsync(Guid requestId, Guid streamId, int bodyLength, Guid[] attachmentStreamIds, int attachmentLength)
            {
                var streams = new List<PayloadDescription>
                {
                    new()
                    {
                        Id = streamId.ToString("D"),
                        ContentType = "application/json",
                        Length = bodyLength,
                    }
                };

                if (attachmentStreamIds != null)
                {
                    foreach (var attachmentId in attachmentStreamIds)
                    {
                        streams.Add(new PayloadDescription
                        {
                            Id = attachmentId.ToString("D"),
                            ContentType = "application/octet-stream",
                            Length = attachmentLength,
                        });
                    }
                }

                var payload = new RequestPayload
                {
                    Verb = "POST",
                    Path = "/v3/conversations/abc/activities",
                    Streams = streams,
                };

                var json = JsonSerializer.SerializeToUtf8Bytes(payload);
                return WriteFrameAsync(PayloadTypes.Request, requestId, json, end: true);
            }

            public async Task WriteChunkedStreamAsync(Guid streamId, byte[] body, int chunkSize, bool sendFinalEndFlag = true)
            {
                for (int offset = 0; offset < body.Length; offset += chunkSize)
                {
                    int len = Math.Min(chunkSize, body.Length - offset);
                    bool isLast = offset + len == body.Length;
                    bool end = isLast && sendFinalEndFlag;
                    var chunk = new byte[len];
                    Buffer.BlockCopy(body, offset, chunk, 0, len);
                    await WriteFrameAsync(PayloadTypes.Stream, streamId, chunk, end).ConfigureAwait(false);
                }
            }

            public async Task WriteFrameAsync(char type, Guid id, byte[] payload, bool end)
            {
                var header = new Header
                {
                    Type = type,
                    Id = id,
                    PayloadLength = payload?.Length ?? 0,
                    End = end
                };

                var headerBytes = HeaderSerializer.Serialize(header);
                await _inboundClient.WriteAsync(headerBytes).ConfigureAwait(false);
                if (payload != null && payload.Length > 0)
                {
                    await _inboundClient.WriteAsync(payload).ConfigureAwait(false);
                }

                await _inboundClient.FlushAsync().ConfigureAwait(false);
            }

            /// <summary>
            /// Writes raw bytes to the inbound pipe without any framing header.
            /// Used to simulate the Bot.Streaming bug where trailing attachment bytes
            /// are written beyond what the frame header declared.
            /// </summary>
            public async Task WriteRawBytesAsync(byte[] data)
            {
                await _inboundClient.WriteAsync(data).ConfigureAwait(false);
                await _inboundClient.FlushAsync().ConfigureAwait(false);
            }

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _drainCts.Cancel(); } catch { }
                try { _inboundClient.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
                _drainCts.Dispose();
            }
        }
    }
}
