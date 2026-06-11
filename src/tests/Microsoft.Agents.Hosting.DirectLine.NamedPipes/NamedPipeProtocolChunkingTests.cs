// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    /// <summary>
    /// Verifies the outbound 64 KB chunking invariant for stream payloads: stream bytes larger
    /// than <see cref="NamedPipeProtocol.MaxSendStreamChunkSize"/> (65536) are split across multiple
    /// frames sharing the same payload id, and only the last chunk carries End=true when the
    /// caller requested termination.
    /// </summary>
    public class NamedPipeProtocolChunkingTests
    {
        private const int Max = NamedPipeProtocol.MaxSendStreamChunkSize;

        [Theory]
        [InlineData(0)]            // empty -> 1 frame
        [InlineData(1)]            // 1 frame
        [InlineData(Max - 1)]      // 1 frame
        [InlineData(Max)]          // exactly 1 frame
        [InlineData(Max + 1)]      // 2 frames
        [InlineData(Max * 2)]      // 2 frames
        [InlineData(Max * 2 + 7)]  // 3 frames
        [InlineData(200_000)]      // several frames
        public async Task LargeBodyStream_IsSplitIntoMaxLengthFrames(int bodyLength)
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var body = MakePayload(bodyLength);

            var requestTask = harness.Protocol.SendRequestAsync(
                "POST",
                "/v3/conversations/abc/activities",
                body,
                CancellationToken.None);

            // Request frame (JSON envelope, never larger than Max here).
            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            Assert.True(requestFrame.Header.End, "Request envelope frame must end after its single JSON payload.");

            // Stream frames must total bodyLength, each <= Max, only last has End=true.
            var reassembled = new MemoryStream();
            bool sawEnd = false;
            int frameCount = 0;
            int expectedFrames = bodyLength == 0 ? 1 : (bodyLength + Max - 1) / Max;
            while (!sawEnd)
            {
                var frame = await harness.ReadFrameAsync();
                Assert.Equal(PayloadTypes.Stream, frame.Header.Type);
                Assert.True(frame.Header.PayloadLength <= Max, $"Frame {frameCount} exceeded MaxSendStreamChunkSize: {frame.Header.PayloadLength}.");
                reassembled.Write(frame.Payload, 0, frame.Payload.Length);
                sawEnd = frame.Header.End;
                frameCount++;
                Assert.True(frameCount <= expectedFrames, $"Saw {frameCount} frames, expected at most {expectedFrames}.");
            }

            Assert.Equal(expectedFrames, frameCount);
            Assert.Equal(body, reassembled.ToArray());

            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task EmptyBody_SendsExactlyOneFrame_WithEndTrue()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();

            var requestTask = harness.Protocol.SendRequestAsync(
                "GET",
                "/v3/conversations",
                body: null,
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            Assert.True(requestFrame.Header.End);

            // No stream frame expected when body is null.
            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task LargeRequestEnvelope_IsSentAsSingleFrame()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var longPath = "/v3/conversations/" + new string('x', Max + 100) + "/activities";

            var requestTask = harness.Protocol.SendRequestAsync(
                "GET",
                longPath,
                body: null,
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            Assert.True(requestFrame.Header.End);
            Assert.True(requestFrame.Header.PayloadLength > Max);
            Assert.Equal(requestFrame.Header.PayloadLength, requestFrame.Payload.Length);

            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task ChunkedFrames_ShareSamePayloadId()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var body = MakePayload(Max * 3 + 100);

            var requestTask = harness.Protocol.SendRequestAsync(
                "POST",
                "/v3/conversations/abc/activities",
                body,
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            // Inspect the request payload to extract the single stream id.
            var payload = JsonSerializer.Deserialize<RequestPayload>(requestFrame.Payload);
            Assert.NotNull(payload);
            Assert.NotNull(payload.Streams);
            Assert.Single(payload.Streams);
            var expectedStreamId = Guid.Parse(payload.Streams[0].Id);

            bool sawEnd = false;
            while (!sawEnd)
            {
                var frame = await harness.ReadFrameAsync();
                Assert.Equal(PayloadTypes.Stream, frame.Header.Type);
                Assert.Equal(expectedStreamId, frame.Header.Id);
                sawEnd = frame.Header.End;
            }

            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
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
        /// Outbound-undrained harness so the test can read each frame written by the protocol.
        /// </summary>
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
                    : [];
                return (header, payload);
            }

            public Task WriteResponseAsync(Guid requestId, int statusCode)
            {
                var payload = new ResponsePayload { StatusCode = statusCode, Streams = null };
                var json = JsonSerializer.SerializeToUtf8Bytes(payload);
                var header = new Header
                {
                    Type = PayloadTypes.Response,
                    Id = requestId,
                    PayloadLength = json.Length,
                    End = true,
                };
                var headerBuf = HeaderSerializer.Serialize(header);
                return WriteAllAsync(_inboundClient, headerBuf, json);
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

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _inboundClient.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
            }
        }
    }
}
