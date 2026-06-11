// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
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
    /// Verifies the Bot Framework streaming framing invariant that the per-payload-id
    /// End flag is set to true on the final frame of each payload id.
    ///
    /// For Request / Response frames the JSON envelope is a single, self-contained
    /// payload — the body bytes (if any) travel as their own stream id with their own
    /// End flag. A peer assembler (e.g. <c>PayloadStreamAssembler.OnReceive</c> in
    /// botbuilder-dotnet) will hold a payload open until it sees End=true, so emitting
    /// End=false on the Request/Response JSON frame would stall a spec-compliant peer.
    /// </summary>
    public class NamedPipeProtocolEndFlagTests
    {
        [Fact]
        public async Task SendRequestAsync_NoBody_RequestFrameHasEndTrue()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var requestTask = harness.Protocol.SendRequestAsync(
                "GET",
                "/v3/conversations",
                body: null,
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            Assert.True(requestFrame.Header.End, "Request frame must have End=true (per-payload-id semantics).");

            // No body => no stream frame. Complete the request so the call returns.
            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);

            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task SendRequestAsync_WithBody_RequestFrameAndStreamFrameBothHaveEndTrue()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var body = Encoding.UTF8.GetBytes("hello world");

            var requestTask = harness.Protocol.SendRequestAsync(
                "POST",
                "/v3/conversations/abc/activities",
                body,
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            Assert.True(
                requestFrame.Header.End,
                "Request frame JSON payload is a single complete frame; End must be true even when a body stream follows.");

            var streamFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Stream, streamFrame.Header.Type);
            Assert.True(streamFrame.Header.End, "Stream frame containing the body must terminate with End=true.");
            Assert.Equal(body, streamFrame.Payload);

            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task SendResponseAsync_WithBody_ResponseFrameAndStreamFrameBothHaveEndTrue()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();

            // Register a request handler that returns a response carrying a body.
            var responseBody = Encoding.UTF8.GetBytes("ok-body");
            harness.Protocol.OnRequestReceived = (req, ct) =>
                Task.FromResult(new NamedPipeResponse
                {
                    StatusCode = 200,
                    Body = responseBody,
                });

            // Drive an inbound request frame (no body) and expect Response + Stream frames out.
            var requestId = Guid.NewGuid();
            var requestJson = JsonSerializer.SerializeToUtf8Bytes(new RequestPayload
            {
                Verb = "GET",
                Path = "/v3/conversations",
                Streams = null,
            });
            await harness.WriteInboundFrameAsync(PayloadTypes.Request, requestId, requestJson, end: true);

            var responseFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Response, responseFrame.Header.Type);
            Assert.True(
                responseFrame.Header.End,
                "Response frame JSON payload is a single complete frame; End must be true even when a body stream follows.");

            var streamFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Stream, streamFrame.Header.Type);
            Assert.True(streamFrame.Header.End, "Response body stream frame must terminate with End=true.");
            Assert.Equal(responseBody, streamFrame.Payload);
        }

        /// <summary>
        /// Test harness with an undrained outbound pipe so the test can inspect raw frames.
        /// Tests must complete any in-flight outbound request (e.g. by writing a Response inbound)
        /// before disposing.
        /// </summary>
        private sealed class FrameInspectorHarness : IAsyncDisposable, IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;
            private bool _started;

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
                h._started = true;
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

            public Task WriteInboundFrameAsync(char type, Guid id, byte[] payload, bool end)
            {
                var header = new Header
                {
                    Type = type,
                    Id = id,
                    PayloadLength = payload == null ? 0 : payload.Length,
                    End = end,
                };
                var headerBuf = new byte[HeaderSerializer.HeaderSize];
                HeaderSerializer.Serialize(header, headerBuf);
                return WriteAllAsync(_inboundClient, headerBuf, payload);
            }

            public Task WriteResponseAsync(Guid requestId, int statusCode)
            {
                var payload = new ResponsePayload
                {
                    StatusCode = statusCode,
                    Streams = null,
                };
                var json = JsonSerializer.SerializeToUtf8Bytes(payload);
                return WriteInboundFrameAsync(PayloadTypes.Response, requestId, json, end: true);
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

            public async ValueTask DisposeAsync()
            {
                if (_started)
                {
                    await Protocol.DisposeAsync().ConfigureAwait(false);
                }

                Dispose();
            }

            public void Dispose()
            {
                _inboundClient.Dispose();
                _inboundServer.Dispose();
                _outboundClient.Dispose();
                _outboundServer.Dispose();
            }
        }
    }
}
