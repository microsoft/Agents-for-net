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
    /// Verifies per-stream <c>ContentType</c> plumbing on both inbound and outbound paths,
    /// for the primary body (<c>Streams[0]</c>) and for attachment streams (<c>Streams[1..N]</c>).
    /// The Bot.Streaming wire format advertises a content type per payload descriptor; before
    /// this gap was closed, <c>Streams[0]</c>'s content type was discarded inbound and hardcoded
    /// to <c>application/json</c> outbound.
    /// </summary>
    public class NamedPipeProtocolContentTypeTests
    {
        // ---------- Inbound: primary stream ----------

        [Fact]
        public async Task PrimaryStream_ContentType_FlowsToRequest()
        {
            using var harness = await InboundHarness.CreateAsync();
            var received = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = Encoding.UTF8.GetBytes("hello plain text");

            await harness.WriteRequestAsync(requestId, streamId, body.Length, primaryContentType: "text/plain");
            await harness.WriteFrameAsync(PayloadTypes.Stream, streamId, body, end: true);

            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("text/plain", req.ContentType);
            Assert.Equal(body, req.Body);
        }

        [Fact]
        public async Task PrimaryStream_NoContentType_DefaultsToJson()
        {
            using var harness = await InboundHarness.CreateAsync();
            var received = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var body = Encoding.UTF8.GetBytes("{\"type\":\"message\"}");

            await harness.WriteRequestAsync(requestId, streamId, body.Length, primaryContentType: null);
            await harness.WriteFrameAsync(PayloadTypes.Stream, streamId, body, end: true);

            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("application/json", req.ContentType);
        }

        [Fact]
        public async Task RequestsWithoutAttachmentStreams_GetIndependentEmptyAttachmentLists()
        {
            using var harness = await InboundHarness.CreateAsync();
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondAttachmentCount = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                if (req.Id == firstId)
                {
                    req.Attachments.Add(new NamedPipeAttachment { ContentType = "text/plain", Body = [1] });
                    firstReceived.TrySetResult();
                }
                else if (req.Id == secondId)
                {
                    secondAttachmentCount.TrySetResult(req.Attachments.Count);
                }

                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            await harness.WriteRequestWithoutStreamsAsync(firstId);
            await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await harness.WriteRequestWithoutStreamsAsync(secondId);

            Assert.Equal(0, await secondAttachmentCount.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        // ---------- Inbound: attachment streams (regression coverage) ----------

        [Fact]
        public async Task AttachmentStream_ContentType_FlowsToRequest()
        {
            using var harness = await InboundHarness.CreateAsync();
            var received = new TaskCompletionSource<NamedPipeRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.TrySetResult(req);
                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var primaryId = Guid.NewGuid();
            var attachmentId = Guid.NewGuid();
            var primaryBody = Encoding.UTF8.GetBytes("{}");
            var attachmentBody = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic

            await harness.WriteRequestAsync(
                requestId,
                primaryId,
                primaryBody.Length,
                primaryContentType: "application/json",
                attachmentStreams: [(attachmentId, attachmentBody.Length, "image/png")]);

            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryId, primaryBody, end: true);
            await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentId, attachmentBody, end: true);

            var req = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(req.Attachments);
            Assert.Equal("image/png", req.Attachments[0].ContentType);
            Assert.Equal(attachmentBody, req.Attachments[0].Body);
        }

        [Fact]
        public async Task InboundSingleFrameStream_WithMismatchedDescriptorLength_StillDispatchesCorrectly()
        {
            // Tests that when the descriptor's Length field is larger than the actual framed
            // attachment data, the protocol trusts framing (End=true) and dispatches without blocking.
            using var harness = await InboundHarness.CreateAsync();
            var received = new List<NamedPipeRequest>();
            var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            harness.Protocol.OnRequestReceived = (req, _) =>
            {
                received.Add(req);
                if (received.Count == 2)
                {
                    allReceived.TrySetResult(true);
                }

                return Task.FromResult(NamedPipeResponse.OK());
            };
            harness.Start();

            var requestId = Guid.NewGuid();
            var primaryId = Guid.NewGuid();
            var attachmentId = Guid.NewGuid();
            var primaryBody = Encoding.UTF8.GetBytes("{}");
            var attachmentBody = new byte[100];
            for (int i = 0; i < attachmentBody.Length; i++)
            {
                attachmentBody[i] = (byte)(i % 251);
            }

            // Descriptor declares 262144 bytes, but actual content is only 100 bytes
            await harness.WriteRequestAsync(
                requestId,
                primaryId,
                primaryBody.Length,
                primaryContentType: "application/json",
                attachmentStreams: [(attachmentId, 262144, "application/octet-stream")]);

            await harness.WriteFrameAsync(PayloadTypes.Stream, primaryId, primaryBody, end: true);

            // Attachment is properly framed: header PayloadLength = actual bytes = 100, End=true
            await harness.WriteFrameAsync(PayloadTypes.Stream, attachmentId, attachmentBody, end: true);

            // Wait for probe timeout (20ms) before sending next request.
            // The probe detects trailing bytes by checking if data arrives immediately;
            // in tests with in-memory pipes we need to let the timeout expire first.
            await Task.Delay(50);

            // Second request arrives after probe timeout (no drain needed)
            await harness.WriteRequestWithoutStreamsAsync(Guid.NewGuid());

            await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(2, received.Count);
            Assert.Single(received[0].Attachments);
            Assert.Equal(attachmentBody, received[0].Attachments[0].Body);
        }

        // ---------- Outbound: primary content type on request and response ----------

        [Fact]
        public async Task OutboundRequest_PropagatesPrimaryContentType()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            var body = Encoding.UTF8.GetBytes("plain text body");

            var requestTask = harness.Protocol.SendRequestAsync(
                "POST",
                "/v3/conversations/abc/activities",
                body,
                attachments: null,
                contentType: "text/plain",
                CancellationToken.None);

            var requestFrame = await harness.ReadFrameAsync();
            Assert.Equal(PayloadTypes.Request, requestFrame.Header.Type);
            var payload = JsonSerializer.Deserialize<RequestPayload>(requestFrame.Payload);
            Assert.NotNull(payload);
            Assert.NotNull(payload.Streams);
            Assert.Single(payload.Streams);
            Assert.Equal("text/plain", payload.Streams[0].ContentType);
            var requestJson = Encoding.UTF8.GetString(requestFrame.Payload);
            Assert.Contains("\"type\":\"text/plain\"", requestJson);
            Assert.DoesNotContain("\"contentType\"", requestJson);

            // Drain body stream + complete the call.
            await harness.ReadFrameAsync();
            await harness.WriteResponseAsync(requestFrame.Header.Id, statusCode: 200);
            var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(200, response.StatusCode);
        }

        [Fact]
        public async Task Response_PrimaryContentType_RoundTrips()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, _) => Task.FromResult(new NamedPipeResponse
            {
                StatusCode = 200,
                Body = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><ok/>"),
                ContentType = "application/xml",
            });

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
            var payload = JsonSerializer.Deserialize<ResponsePayload>(responseFrame.Payload);
            Assert.NotNull(payload);
            Assert.NotNull(payload.Streams);
            Assert.Single(payload.Streams);
            Assert.Equal("application/xml", payload.Streams[0].ContentType);
        }

        [Fact]
        public async Task OutboundResponse_PropagatesAttachmentContentTypes()
        {
            using var harness = await FrameInspectorHarness.CreateAsync();
            harness.Protocol.OnRequestReceived = (req, _) => Task.FromResult(new NamedPipeResponse
            {
                StatusCode = 200,
                Body = Encoding.UTF8.GetBytes("{}"),
                Attachments =
                [
                    new() { ContentType = "image/png", Body = [1, 2, 3] },
                    new() { ContentType = "audio/wav", Body = [4, 5, 6, 7] },
                ],
            });

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
            var payload = JsonSerializer.Deserialize<ResponsePayload>(responseFrame.Payload);
            Assert.NotNull(payload);
            Assert.Equal(3, payload.Streams.Count);
            Assert.Equal("application/json", payload.Streams[0].ContentType);
            Assert.Equal("image/png", payload.Streams[1].ContentType);
            Assert.Equal("audio/wav", payload.Streams[2].ContentType);
        }

        // ---------- Inbound harness (supports primary content type + attachments with types) ----------

        private sealed class InboundHarness : IDisposable
        {
            private readonly AnonymousPipeServerStream _inboundServer;
            private readonly AnonymousPipeClientStream _inboundClient;
            private readonly AnonymousPipeServerStream _outboundServer;
            private readonly AnonymousPipeClientStream _outboundClient;
            private readonly Task _outboundDrain;
            private readonly CancellationTokenSource _drainCts;

            public NamedPipeProtocol Protocol { get; }

            private InboundHarness(
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
                    }
                });
            }

            public static Task<InboundHarness> CreateAsync()
            {
                var inboundServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
                var inboundClient = new AnonymousPipeClientStream(PipeDirection.Out, inboundServer.GetClientHandleAsString());
                var outboundServer = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
                var outboundClient = new AnonymousPipeClientStream(PipeDirection.In, outboundServer.GetClientHandleAsString());
                return Task.FromResult(new InboundHarness(inboundServer, inboundClient, outboundServer, outboundClient));
            }

            public void Start() => Protocol.Start();

            public Task WriteRequestAsync(
                Guid requestId,
                Guid primaryStreamId,
                int primaryLength,
                string primaryContentType,
                (Guid Id, int Length, string ContentType)[] attachmentStreams = null)
            {
                var streams = new List<PayloadDescription>
                {
                    new()
                    {
                        Id = primaryStreamId.ToString("D"),
                        ContentType = primaryContentType,
                        Length = primaryLength,
                    },
                };

                if (attachmentStreams != null)
                {
                    foreach (var (id, length, contentType) in attachmentStreams)
                    {
                        streams.Add(new PayloadDescription
                        {
                            Id = id.ToString("D"),
                            ContentType = contentType,
                            Length = length,
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

            public Task WriteRequestWithoutStreamsAsync(Guid requestId)
            {
                var payload = new RequestPayload
                {
                    Verb = "POST",
                    Path = "/v3/conversations/abc/activities",
                    Streams = null,
                };

                var json = JsonSerializer.SerializeToUtf8Bytes(payload);
                return WriteFrameAsync(PayloadTypes.Request, requestId, json, end: true);
            }

            public async Task WriteFrameAsync(char type, Guid id, byte[] payload, bool end, int? declaredPayloadLength = null)
            {
                var header = new Header
                {
                    Type = type,
                    Id = id,
                    PayloadLength = declaredPayloadLength ?? payload?.Length ?? 0,
                    End = end,
                };
                var headerBytes = HeaderSerializer.Serialize(header);
                await _inboundClient.WriteAsync(headerBytes).ConfigureAwait(false);
                if (payload != null && payload.Length > 0)
                {
                    await _inboundClient.WriteAsync(payload).ConfigureAwait(false);
                }

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

        // ---------- Outbound harness (undrained outbound pipe; reads raw frames) ----------

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
