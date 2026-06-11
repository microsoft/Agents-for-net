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
    /// Tests for protocol-level boundary conditions: double-start, input validation,
    /// malformed inbound data, oversized payloads, resource exhaustion limits,
    /// disconnection during read, and dispatch failure isolation.
    /// </summary>
    public class NamedPipeProtocolBoundaryTests
    {
        // ----- Start guard -----

        [Fact]
        public async Task Start_CalledTwice_ThrowsInvalidOperation()
        {
            using var harness = await TestHarness.CreateAsync();

            Assert.Throws<InvalidOperationException>(() => harness.Protocol.Start());
        }

        // ----- SendRequestAsync input validation -----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendRequestAsync_NullOrEmptyVerb_ThrowsArgumentException(string verb)
        {
            using var harness = await TestHarness.CreateAsync();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await harness.Protocol.SendRequestAsync(verb, "/v3/test", null, CancellationToken.None));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendRequestAsync_NullOrEmptyPath_ThrowsArgumentException(string path)
        {
            using var harness = await TestHarness.CreateAsync();

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await harness.Protocol.SendRequestAsync("POST", path, null, CancellationToken.None));
        }

        // ----- Timeout on no response -----

        [Fact]
        public async Task SendRequestAsync_CallerCancellation_ThrowsOperationCanceled()
        {
            using var harness = await TestHarness.CreateAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await harness.Protocol.SendRequestAsync("POST", "/v3/test", null, cts.Token));
        }

        // ----- Pipe disconnect during read loop -----

        [Fact]
        public async Task ReadLoop_PipeDisconnect_CompletionTaskCompletes()
        {
            using var harness = await TestHarness.CreateAsync();

            // Close the writer end that feeds the read loop.
            harness.CloseInboundWriter();

            // Completion should finish without throwing.
            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // ----- Malformed header in read loop -----

        [Fact]
        public async Task ReadLoop_MalformedHeader_CompletesGracefully()
        {
            using var harness = await TestHarness.CreateAsync();

            // Write garbage that's exactly 48 bytes (header size) but not valid.
            var garbage = new byte[HeaderSerializer.HeaderSize];
            Array.Fill(garbage, (byte)'Z');
            await harness.WriteRawInboundAsync(garbage);

            // Read loop should terminate on FormatException.
            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // ----- Pipe closure completes gracefully -----

        [Fact]
        public async Task ReadLoop_PipeClosed_CompletesGracefully()
        {
            using var harness = await TestHarness.CreateAsync();

            // Close the inbound writer — the read loop should detect end-of-pipe
            // and complete without throwing.
            harness.CloseInboundWriter();
            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // ----- Unknown frame type is logged but not fatal -----

        [Fact]
        public async Task ReadLoop_UnknownFrameType_ContinuesProcessing()
        {
            using var harness = await TestHarness.CreateAsync();

            // Send a frame with unknown type 'Z', then a valid response to a request.
            var unknownId = Guid.NewGuid();
            var unknownHeader = new Header
            {
                Type = 'Z',
                Id = unknownId,
                PayloadLength = 0,
                End = true,
            };
            await harness.WriteFrameAsync(unknownHeader, []);

            // Now send a real request and prove the loop is still alive.
            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            var outFrame = await harness.ReadOutboundFrameAsync();
            Assert.Equal(PayloadTypes.Request, outFrame.Header.Type);

            await harness.WriteResponseAsync(outFrame.Header.Id, 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        // ----- Null payload on request frame is silently ignored -----

        [Fact]
        public async Task ReadLoop_RequestFrameWithZeroPayload_DoesNotCrash()
        {
            using var harness = await TestHarness.CreateAsync();

            // Send a Request frame with 0 payload — HandleRequestFrame guards on null.
            var header = new Header
            {
                Type = PayloadTypes.Request,
                Id = Guid.NewGuid(),
                PayloadLength = 0,
                End = true,
            };
            await harness.WriteFrameAsync(header, []);

            // Send another request to verify the loop is still alive.
            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            var outFrame = await harness.ReadOutboundFrameAsync();
            await harness.WriteResponseAsync(outFrame.Header.Id, 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        // ----- Response frame with zero payload is silently ignored -----

        [Fact]
        public async Task ReadLoop_ResponseFrameWithZeroPayload_DoesNotCrash()
        {
            using var harness = await TestHarness.CreateAsync();

            // Send a Response frame with 0 payload — HandleResponseFrame guards on null.
            var header = new Header
            {
                Type = PayloadTypes.Response,
                Id = Guid.NewGuid(),
                PayloadLength = 0,
                End = true,
            };
            await harness.WriteFrameAsync(header, []);

            // Verify read loop is still alive.
            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            var outFrame = await harness.ReadOutboundFrameAsync();
            await harness.WriteResponseAsync(outFrame.Header.Id, 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        // ----- Response for unknown request id is ignored -----

        [Fact]
        public async Task ReadLoop_ResponseForUnknownRequest_DoesNotCrash()
        {
            using var harness = await TestHarness.CreateAsync();

            // Send a Response for an id that was never requested.
            var fakeId = Guid.NewGuid();
            var responsePayload = new ResponsePayload { StatusCode = 200, Streams = null };
            var json = JsonSerializer.SerializeToUtf8Bytes(responsePayload);
            var header = new Header
            {
                Type = PayloadTypes.Response,
                Id = fakeId,
                PayloadLength = json.Length,
                End = true,
            };
            await harness.WriteFrameAsync(header, json);

            // Verify read loop is still alive.
            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            var outFrame = await harness.ReadOutboundFrameAsync();
            await harness.WriteResponseAsync(outFrame.Header.Id, 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        // ----- Pipe disconnect fails pending outbound requests -----

        [Fact]
        public async Task ReadLoop_Disconnect_FailsPendingOutboundRequests()
        {
            using var harness = await TestHarness.CreateAsync();

            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            // Consume the outbound frame so the write doesn't block.
            await harness.ReadOutboundFrameAsync();

            // Disconnect the inbound pipe.
            harness.CloseInboundWriter();

            // The pending request should fail with IOException.
            var ex = await Assert.ThrowsAsync<IOException>(async () =>
                await requestTask.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Contains("disconnected", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ----- MaxStreamBuffers limit disconnects -----

        [Fact]
        public async Task ReadLoop_ExceedMaxStreamBuffers_ReadLoopExits()
        {
            using var harness = await TestHarness.CreateAsync();

            // MaxStreamBuffers = 100. Send 101 stream frames with distinct ids (no End=true).
            for (int i = 0; i <= 100; i++)
            {
                var header = new Header
                {
                    Type = PayloadTypes.Stream,
                    Id = Guid.NewGuid(),
                    PayloadLength = 1,
                    End = false,
                };
                await harness.WriteFrameAsync(header, [0xFF]);
            }

            // Read loop should exit due to MaxStreamBuffers exceeded.
            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        }

        // ----- Dispatch with no handler returns 404 -----

        [Fact]
        public async Task InboundRequest_NoHandler_Returns404()
        {
            using var harness = await TestHarness.CreateAsync();
            // Do NOT set OnRequestReceived — it defaults to null.

            var requestId = Guid.NewGuid();
            var bodyStreamId = Guid.NewGuid();

            // Send a request with a body stream.
            var requestPayload = new RequestPayload
            {
                Verb = "POST",
                Path = "/api/messages",
                Streams = [new PayloadDescription { Id = bodyStreamId.ToString("D"), ContentType = "application/json", Length = 2 }],
            };
            var requestJson = JsonSerializer.SerializeToUtf8Bytes(requestPayload);
            var requestHeader = new Header
            {
                Type = PayloadTypes.Request,
                Id = requestId,
                PayloadLength = requestJson.Length,
                End = true,
            };
            await harness.WriteFrameAsync(requestHeader, requestJson);

            // Send the body stream.
            var streamHeader = new Header
            {
                Type = PayloadTypes.Stream,
                Id = bodyStreamId,
                PayloadLength = 2,
                End = true,
            };
            await harness.WriteFrameAsync(streamHeader, "{}"u8.ToArray());

            // Read the outbound response frame.
            var responseFrame = await harness.ReadOutboundFrameAsync();
            Assert.Equal(PayloadTypes.Response, responseFrame.Header.Type);
            Assert.Equal(requestId, responseFrame.Header.Id);

            var response = JsonSerializer.Deserialize<ResponsePayload>(responseFrame.Payload);
            Assert.Equal(404, response.StatusCode);
        }

        // ----- Malformed JSON in request payload terminates read loop -----

        [Fact]
        public async Task ReadLoop_MalformedRequestPayloadJson_TerminatesReadLoop()
        {
            using var harness = await TestHarness.CreateAsync();

            // Send a Request frame with invalid JSON — JsonSerializer.Deserialize throws,
            // which is caught by the read loop's outer catch and terminates it.
            var badJson = "{ not valid json !!!"u8.ToArray();
            var header = new Header
            {
                Type = PayloadTypes.Request,
                Id = Guid.NewGuid(),
                PayloadLength = badJson.Length,
                End = true,
            };
            await harness.WriteFrameAsync(header, badJson);

            // The read loop should exit due to the JsonException.
            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // ----- DisposeAsync is idempotent -----

        [Fact]
        public async Task DisposeAsync_SecondCall_DoesNotThrow()
        {
            using var harness = await TestHarness.CreateAsync();

            await harness.Protocol.DisposeAsync();

            // Second call should be a no-op.
            await harness.Protocol.DisposeAsync();
        }

        // ----- DisposeAsync sends CancelAll frame -----

        [Fact]
        public async Task DisposeAsync_SendsCancelAllFrame()
        {
            using var harness = await TestHarness.CreateAsync();

            await harness.Protocol.DisposeAsync();

            // Read the CancelAll frame that DisposeAsync emits.
            var frame = await harness.ReadOutboundFrameAsync();
            Assert.Equal(PayloadTypes.CancelAll, frame.Header.Type);
            Assert.True(frame.Header.End);
        }

        // ----- CancelAll fails all pending outbound requests -----

        [Fact]
        public async Task InboundCancelAll_FailsPendingOutboundRequests()
        {
            using var harness = await TestHarness.CreateAsync();

            var requestTask = harness.Protocol.SendRequestAsync("POST", "/v3/test", null, CancellationToken.None);
            await harness.ReadOutboundFrameAsync(); // consume request frame

            // Send CancelAll inbound.
            var cancelHeader = new Header
            {
                Type = PayloadTypes.CancelAll,
                Id = Guid.Empty,
                PayloadLength = 0,
                End = true,
            };
            await harness.WriteFrameAsync(cancelHeader, []);

            // The pending request should fail with OperationCanceledException.
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await requestTask.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        // ----- Pipe disconnect mid-payload read -----

        [Fact]
        public async Task ReadLoop_DisconnectMidPayload_ReadLoopExits()
        {
            using var harness = await TestHarness.CreateAsync();

            // Write a header that claims 1000 bytes payload, but only write 10 then close.
            var header = new Header
            {
                Type = PayloadTypes.Stream,
                Id = Guid.NewGuid(),
                PayloadLength = 1000,
                End = false,
            };
            var headerBytes = HeaderSerializer.Serialize(header);
            await harness.WriteRawInboundAsync(headerBytes);
            await harness.WriteRawInboundAsync(new byte[10]); // only 10 of 1000
            harness.CloseInboundWriter();

            await harness.Protocol.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // ========== Test Harness ==========

        private sealed class TestHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;

            public NamedPipeProtocol Protocol { get; }

            private TestHarness(
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

            public static Task<TestHarness> CreateAsync()
            {
                var inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                var inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, inboundServer.GetClientHandleAsString());
                var outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                var outboundClient = new AnonymousPipeClientStream(PipeDirection.In, outboundServer.GetClientHandleAsString());

                var h = new TestHarness(inboundServer, inboundClient, outboundServer, outboundClient);
                h.Protocol.Start();
                return Task.FromResult(h);
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

            public async Task WriteRawInboundAsync(byte[] data)
            {
                await _inboundClient.WriteAsync(data);
                await _inboundClient.FlushAsync();
            }

            public void CloseInboundWriter()
            {
                _inboundClient?.Dispose();
                _inboundClient = null;
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
                return WriteFrameAsync(header, json);
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
                        throw new EndOfStreamException($"Pipe closed before {count} bytes read (got {read}).");
                    }
                    read += n;
                }
                return buf;
            }

            public void Dispose()
            {
                try { Protocol.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
                try { _inboundClient?.Dispose(); } catch { }
                try { _inboundServer.Dispose(); } catch { }
                try { _outboundClient.Dispose(); } catch { }
                try { _outboundServer.Dispose(); } catch { }
            }
        }
    }
}
