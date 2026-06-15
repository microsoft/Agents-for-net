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
        /// A 0-byte attachment stream with descriptor length=0 should dispatch immediately.
        /// No trailing-byte drain fires because expectedLen=0.
        /// </summary>
        [Fact]
        public async Task ZeroByteAttachmentStream_DoesNotBlockDispatch()
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
            var attachmentId = Guid.NewGuid();
            var primaryBody = Encoding.UTF8.GetBytes("{\"type\":\"message\"}");

            // Descriptor declares attachment with length=0 (truly empty attachment).
            await harness.WriteRequestAsync(requestId, primaryStreamId, primaryBody.Length, [attachmentId], attachmentLength: 0);
            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId, primaryBody, end: true);
            await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentId, [], end: true);

            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(primaryBody, req.Body);
            Assert.Single(req.Attachments);
            Assert.Empty(req.Attachments[0].Body);
        }

        /// <summary>
        /// When a stream descriptor declares a length larger than the actual framed data
        /// (e.g., DirectLineFlex reporting 262144 for a 9KB image), the protocol trusts the
        /// framing (End=true flag) rather than the descriptor length. Subsequent requests
        /// must parse correctly — no blocking, no drain.
        /// </summary>
        [Theory]
        [InlineData(4096, 262144)]   // 4KB actual, 256KB declared
        [InlineData(100, 9542)]      // 100B actual, ~9KB declared
        [InlineData(8192, 65536)]    // 8KB actual (multi-chunk), 64KB declared
        public async Task DescriptorLengthMismatch_DoesNotBlockSubsequentRequests(int actualLength, int declaredLength)
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

            var attachmentData = MakePayload(actualLength);
            var primaryBody = Encoding.UTF8.GetBytes("{\"type\":\"message\"}");

            // --- First request: descriptor declares more than actual ---
            var req1Id = Guid.NewGuid();
            var primaryStreamId1 = Guid.NewGuid();
            var attachmentId1 = Guid.NewGuid();

            // Descriptor says declaredLength, but we'll only send actualLength bytes
            await harness.WriteRequestAsync(req1Id, primaryStreamId1, primaryBody.Length,
                [attachmentId1], declaredLength);

            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId1, primaryBody, end: true);

            // Send attachment in proper framed chunks (4096 max each), last with End=true
            await harness.WriteChunkedStreamAsync(attachmentId1, attachmentData, FrameChunkSize, sendFinalEndFlag: true);

            // Wait for the probe timeout (20ms) to expire before sending the next request.
            // In production, DLFlex has a meaningful gap between operations; in tests with
            // anonymous pipes, data arrives instantly so without this delay the probe would
            // incorrectly consume the next frame's header byte.
            // Use 200ms (not 50ms) to account for CancelAfter timer granularity (~15.6ms on
            // Windows) and thread scheduling jitter on loaded CI machines.
            await Task.Delay(200);

            // --- Second request: must parse correctly immediately ---
            var req2Id = Guid.NewGuid();
            var primaryStreamId2 = Guid.NewGuid();
            var body2 = Encoding.UTF8.GetBytes("{\"type\":\"message\",\"text\":\"hello\"}");
            await harness.WriteRequestAsync(req2Id, primaryStreamId2, body2.Length);
            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryStreamId2, body2, end: true);

            // Both requests should dispatch after the probe timeout (~20ms for mismatch detection)
            var r1 = await tcs1.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var r2 = await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // First request: attachment has the ACTUAL data (not padded to declared length)
            Assert.Single(r1.Attachments);
            Assert.Equal(actualLength, r1.Attachments[0].Body.Length);
            Assert.Equal(attachmentData, r1.Attachments[0].Body);

            // Second request: body parsed correctly (framing is intact)
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

        /// <summary>
        /// Verifies that when a peer advertises streams but never sends them, the pending dispatch
        /// timeout fires and the request is dispatched with partial data (empty attachment).
        /// This prevents the pipe from permanently freezing when DirectLineFlex drops a stream.
        /// </summary>
        [Fact]
        public async Task PendingDispatch_TimesOut_WhenStreamNeverArrives()
        {
            var receivedRequests = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var harness = await ProtocolHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, ct) =>
            {
                receivedRequests.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.Accepted());
            };
            harness.Start();

            // Send a request with 2 streams: body + attachment
            var requestId = Guid.NewGuid();
            var bodyStreamId = Guid.NewGuid();
            var attachmentStreamId = Guid.NewGuid();
            var body = Encoding.UTF8.GetBytes("{\"type\":\"message\",\"text\":\"hello\"}");

            await harness.WriteRequestAsync(requestId, bodyStreamId, body.Length,
                [attachmentStreamId], 1024);

            // Send the body stream (arrives fine)
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId, body, end: true);

            // DO NOT send the attachment stream — simulates DirectLineFlex dropping it

            // The request should still be dispatched after the pending dispatch timeout (15s)
            // plus the 5s read-timeout sweep interval
            var result = await receivedRequests.Task.WaitAsync(TimeSpan.FromSeconds(NamedPipeProtocol.PendingDispatchTimeoutSeconds + 10));

            Assert.NotNull(result);
            Assert.Equal(body, result.Body);
            // The missing attachment should surface as a zero-byte entry
            Assert.Single(result.Attachments);
            Assert.Empty(result.Attachments[0].Body);
        }

        /// <summary>
        /// Verifies that after a pending dispatch timeout, subsequent requests are
        /// still processed correctly (the pipe is not permanently broken).
        /// </summary>
        [Fact]
        public async Task PendingDispatch_Timeout_DoesNotBlockSubsequentRequests()
        {
            var requests = new List<NamedPipeRequest>();
            var firstRequest = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondRequest = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            int requestCount = 0;
            using var harness = await ProtocolHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, ct) =>
            {
                lock (requests) { requests.Add(req); }
                var n = Interlocked.Increment(ref requestCount);
                if (n == 1) firstRequest.TrySetResult(req);
                if (n == 2) secondRequest.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.Accepted());
            };
            harness.Start();

            // First request: has a stream that will never arrive (triggers timeout)
            var req1Id = Guid.NewGuid();
            var bodyStreamId1 = Guid.NewGuid();
            var attachmentStreamId = Guid.NewGuid();
            var body1 = Encoding.UTF8.GetBytes("{\"type\":\"message\",\"text\":\"first\"}");

            await harness.WriteRequestAsync(req1Id, bodyStreamId1, body1.Length,
                [attachmentStreamId], 500);
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId1, body1, end: true);
            // Attachment stream deliberately NOT sent

            // Wait for the first request to be force-dispatched by timeout sweep
            await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(NamedPipeProtocol.PendingDispatchTimeoutSeconds + 10));

            // Second request: should dispatch immediately (no missing streams)
            var req2Id = Guid.NewGuid();
            var bodyStreamId2 = Guid.NewGuid();
            var body2 = Encoding.UTF8.GetBytes("{\"type\":\"message\",\"text\":\"second\"}");

            await harness.WriteRequestAsync(req2Id, bodyStreamId2, body2.Length);
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId2, body2, end: true);

            var result = await secondRequest.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(result);
            Assert.Equal(body2, result.Body);
        }

        /// <summary>
        /// Verifies the trailing-byte drain: when DLFlex sends a single stream frame with
        /// End=true and PayloadLength=0, followed by unframed trailing bytes, the protocol
        /// reads those bytes and delivers the complete stream to the handler.
        /// </summary>
        [Fact]
        public async Task TrailingByteDrain_ReadsUnframedBytesAfterSingleFrameStream()
        {
            var receivedRequests = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var harness = await ProtocolHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, ct) =>
            {
                receivedRequests.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.Accepted());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var bodyStreamId = Guid.NewGuid();
            var trailingData = MakePayload(9542); // Simulates a real attachment

            // Send request with stream descriptor declaring 9542 bytes
            await harness.WriteRequestAsync(requestId, bodyStreamId, trailingData.Length);

            // Send a stream frame with PayloadLength=0, End=true (DLFlex bug: stream.Length returned 0)
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId, [], end: true);

            // Write trailing unframed bytes (this is what DLFlex's CopyToAsync actually writes)
            await harness.WriteRawBytesAsync(trailingData);

            // The drain should consume the trailing bytes and deliver them to the handler
            var result = await receivedRequests.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotNull(result);
            Assert.Equal(trailingData, result.Body);
        }

        /// <summary>
        /// Verifies that for multi-frame streams where total received is less than descriptor,
        /// the probe correctly times out (no trailing bytes) and the request still dispatches
        /// with the actual framed data.
        /// </summary>
        [Fact]
        public async Task TrailingByteDrain_ProbeTimesOutForMultiFrameStream_DispatchesActualData()
        {
            var receivedRequests = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var harness = await ProtocolHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, ct) =>
            {
                receivedRequests.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.Accepted());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var bodyStreamId = Guid.NewGuid();
            var body = MakePayload(8000);

            // Descriptor declares 100000 bytes (much more than actual)
            await harness.WriteRequestAsync(requestId, bodyStreamId, 100000);

            // Send in two frames (multi-frame) — no trailing bytes will follow
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId, body[..4000], end: false);
            await harness.WriteFrameAsync(PayloadTypes.Stream, bodyStreamId, body[4000..], end: true);

            // Probe will timeout after 100ms, then dispatch with actual framed data
            var result = await receivedRequests.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.NotNull(result);
            Assert.Equal(body, result.Body);
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
