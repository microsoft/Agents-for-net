// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
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
    /// Boundary condition tests for <see cref="NamedPipeMessageHandler"/>:
    /// null URI, non-pipe URI, no protocol, missing /v3/ path, protocol
    /// exceptions returning 502, and content-type fallback.
    /// </summary>
    public class NamedPipeMessageHandlerBoundaryTests
    {
        private readonly NamedPipeMessageHandler _handler;

        public NamedPipeMessageHandlerBoundaryTests()
        {
            _handler = new NamedPipeMessageHandler(NullLogger<NamedPipeMessageHandler>.Instance);
        }

        [Fact]
        public async Task SendAsync_NullRequestUri_Returns400()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, (Uri)null);

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_NonPipeUri_Returns502()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/v3/conversations/abc/activities");

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_NoActiveProtocol_Returns503()
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/v3/conversations/abc/activities");

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_SetProtocolThenNull_Returns503()
        {
            // Wire up a real protocol then disconnect.
            using var harness = CreateProtocolHarness();
            _handler.SetProtocol(harness.Protocol);
            _handler.SetProtocol(null);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/v3/conversations/abc/activities");

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_MissingV3Path_Returns400()
        {
            using var harness = CreateProtocolHarness();
            _handler.SetProtocol(harness.Protocol);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/api/messages");

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_ProtocolDisconnected_Returns502()
        {
            // Create a protocol whose writer pipe is immediately broken.
            using var harness = CreateProtocolHarness();
            harness.Protocol.Start();
            _handler.SetProtocol(harness.Protocol);

            // Close the outbound pipe so the protocol write fails.
            harness.BreakOutboundPipe();

            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/v3/conversations/abc/activities")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };

            var response = await _handler.SendViaPipeAsync(request, CancellationToken.None);

            // IOException from broken pipe → returns 502.
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_ValidRequest_RoutesAndReturnsResponse()
        {
            using var harness = CreateProtocolHarness();
            harness.Protocol.Start();
            _handler.SetProtocol(harness.Protocol);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/v3/conversations/abc/activities")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };

            var sendTask = _handler.SendViaPipeAsync(request, CancellationToken.None);

            // Read the outbound request frame and send a response.
            var frame = await harness.ReadOutboundFrameAsync();
            Assert.Equal(PayloadTypes.Request, frame.Header.Type);

            // Read the stream frame (body)
            var streamFrame = await harness.ReadOutboundFrameAsync();
            Assert.Equal(PayloadTypes.Stream, streamFrame.Header.Type);

            await harness.WriteResponseAsync(frame.Header.Id, 202);

            var response = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        [Fact]
        public async Task SendAsync_ResponseWithInvalidContentType_FallsBackToJson()
        {
            using var harness = CreateProtocolHarness();
            harness.Protocol.Start();
            _handler.SetProtocol(harness.Protocol);

            var request = new HttpRequestMessage(HttpMethod.Post,
                "urn:botframework:namedpipe:testpipe/v3/conversations/abc/activities")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };

            var sendTask = _handler.SendViaPipeAsync(request, CancellationToken.None);

            var frame = await harness.ReadOutboundFrameAsync();
            var streamFrame = await harness.ReadOutboundFrameAsync();

            // Send response with a body stream that has an unparseable content-type.
            var bodyStreamId = Guid.NewGuid();
            var responsePayload = new ResponsePayload
            {
                StatusCode = 200,
                Streams = [new PayloadDescription { Id = bodyStreamId.ToString("D"), ContentType = "garbage!!!content", Length = 4 }],
            };
            var responseJson = JsonSerializer.SerializeToUtf8Bytes(responsePayload);
            await harness.WriteFrameAsync(new Header
            {
                Type = PayloadTypes.Response,
                Id = frame.Header.Id,
                PayloadLength = responseJson.Length,
                End = true,
            }, responseJson);

            await harness.WriteFrameAsync(new Header
            {
                Type = PayloadTypes.Stream,
                Id = bodyStreamId,
                PayloadLength = 4,
                End = true,
            }, "test"u8.ToArray());

            var response = await sendTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
        }

        // ========== Harness ==========

        private sealed class ProtocolHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private AnonymousPipeClientStream _outboundClient;

            public NamedPipeProtocol Protocol { get; }

            public ProtocolHarness()
            {
                _inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                _inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, _inboundServer.GetClientHandleAsString());
                _outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                _outboundClient = new AnonymousPipeClientStream(PipeDirection.In, _outboundServer.GetClientHandleAsString());

                Protocol = new NamedPipeProtocol(
                    new NamedPipeTransport(_inboundServer),
                    new NamedPipeTransport(_outboundServer),
                    NullLogger.Instance);
            }

            public void BreakOutboundPipe()
            {
                _outboundClient?.Dispose();
                _outboundClient = null;
                // Dispose the server so writes fail.
                try { _outboundServer.Dispose(); } catch { }
            }

            public async Task WriteFrameAsync(Header header, byte[] payload)
            {
                var headerBytes = HeaderSerializer.Serialize(header);
                await _inboundClient.WriteAsync(headerBytes);
                if (payload != null && payload.Length > 0)
                {
                    await _inboundClient.WriteAsync(payload);
                }
                await _inboundClient.FlushAsync();
            }

            public async Task WriteResponseAsync(Guid requestId, int statusCode)
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
                await WriteFrameAsync(header, json);
            }

            public async Task<(Header Header, byte[] Payload)> ReadOutboundFrameAsync()
            {
                var headerBuf = await ReadExactAsync(_outboundClient, HeaderSerializer.HeaderSize);
                var header = HeaderSerializer.Deserialize(headerBuf);
                var payload = header.PayloadLength > 0
                    ? await ReadExactAsync(_outboundClient, header.PayloadLength)
                    : [];
                return (header, payload);
            }

            private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
            {
                var buf = new byte[count];
                int read = 0;
                while (read < count)
                {
                    var n = await stream.ReadAsync(buf.AsMemory(read, count - read)).ConfigureAwait(false);
                    if (n == 0) throw new EndOfStreamException();
                    read += n;
                }
                return buf;
            }

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _inboundClient.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient?.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
            }
        }

        private static ProtocolHarness CreateProtocolHarness() => new();
    }
}
