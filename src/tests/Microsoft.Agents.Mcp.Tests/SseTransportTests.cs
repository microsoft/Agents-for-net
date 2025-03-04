using Microsoft.Agents.Mcp.Client.Transports;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Server.AspNet;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.Tests
{
    public class SseTransportTests : TransportTestBase
    {
        protected override IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<SseTransportTests> logger)
        {
            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
            SetupFakeHttpCalls(httpClientFactoryMock, processor, transportManager, logger);
            return new HttpSseClientTransport("http://localhost/", httpClientFactoryMock.Object);
        }

        private void SetupFakeHttpCalls(
            Mock<IHttpClientFactory> httpClientFactoryMock,
            IMcpProcessor processor,
            ITransportManager transportManager,
            ILogger<SseTransportTests> logger)
        {
            var handler = new PlumbingHandler(processor, transportManager, logger);
            httpClientFactoryMock.Setup(x => x.CreateClient("")).Returns(() => new HttpClient(handler, false));
        }

        private class PlumbingHandler : HttpClientHandler
        {
            private readonly IMcpProcessor processor;
            private ITransportManager transportManager;
            private ILogger logger;

            public PlumbingHandler(IMcpProcessor processor, ITransportManager transportManager, ILogger logger)
            {
                this.processor = processor;
                this.transportManager = transportManager;
                this.logger = logger;
            }

            protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Get)
                {
                    var response = new PlumbingResponse();

                    var transport = new HttpSseServerTransport(transportManager, (s) => $"http://localhost/{s}", response, cancellationToken, logger);
                    await processor.CreateSessionAsync(transport, cancellationToken);
                    
                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                    {
                        Content = new PlumbingContent(response.Body)
                    };
                }

                if (request.Method == HttpMethod.Post)
                {
                    transportManager.TryGetTransport(request.RequestUri.Segments.Last(), out var transport);
                    await transport.ProcessPayloadAsync(
                        JsonSerializer.Deserialize<JsonRpcPayload>(request.Content.ReadAsStream(), Serialization.GetDefaultMcpSerializationOptions()),
                        cancellationToken);

                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                }

                throw new Exception("Unsupported method");
            }

            private class PlumbingContent : HttpContent
            {
                private Stream body;

                public PlumbingContent(Stream body)
                {
                    this.body = body;
                }

                protected override Task<Stream> CreateContentReadStreamAsync()
                {
                    return Task.FromResult(body);
                }

                protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
                {
                    return Task.CompletedTask;
                }

                protected override bool TryComputeLength(out long length)
                {
                    length = 0;
                    return false;
                }
            }

            private class PlumbingResponse : HttpResponse
            {
                public override HttpContext HttpContext => throw new NotImplementedException();

                public override int StatusCode { get; set; }

                public override IHeaderDictionary Headers { get; } = new HeaderDictionary();

                public override Stream Body { get; set; } = new ChannelStream();
                public override long? ContentLength { get; set; }
                public override string ContentType { get; set; }

                public override IResponseCookies Cookies => throw new NotImplementedException();

                public override bool HasStarted => Body.Length > 0;

                public override void OnCompleted(Func<object, Task> callback, object state)
                {
                }

                public override void OnStarting(Func<object, Task> callback, object state)
                {
                }

                public override void Redirect([StringSyntax("Uri")] string location, bool permanent)
                {
                }
            }

            private class ChannelStream : Stream
            {
                private SemaphoreSlim lockObject = new SemaphoreSlim(0, 2000);
                private Channel<byte[]> channel;
                IEnumerator<byte> enumerator;

                public ChannelStream()
                {
                    channel = Channel.CreateUnbounded<byte[]>();
                }

                public override bool CanRead => true;

                public override bool CanSeek => false;

                public override bool CanWrite => true;

                public override long Length => channel.Reader.Count;

                public override long Position { set => throw new NotImplementedException(); get => throw new NotImplementedException(); }

                public override void Flush()
                {
                }

                public override int Read(byte[] buffer, int offset, int count)
                {
                    if (enumerator == null)
                    {
                        lockObject.Wait();
                        if (channel.Reader.TryRead(out var b))
                        {
                            enumerator = b.AsEnumerable<byte>().GetEnumerator();
                        }
                        else
                        {
                            return 0;
                        }
                    }

                    int c = 0;
                    while (count > 0)
                    {
                        if (!enumerator.MoveNext())
                        {
                            enumerator = null;
                            if (c == 0)
                            {
                                return Read(buffer, offset, count);
                            }

                            return c;
                        }

                        buffer[offset + c] = enumerator.Current;
                        c++;
                        count--;
                    }

                    return c;
                }

                public override long Seek(long offset, SeekOrigin origin)
                {
                    throw new NotImplementedException();
                }

                public override void SetLength(long value)
                {
                    throw new NotImplementedException();
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    channel.Writer.WriteAsync(buffer[offset..(offset + count)]).GetAwaiter().GetResult();
                    lockObject.Release();
                }
            }

        }
    }
}
