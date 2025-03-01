using Microsoft.Agents.MCP.Client.DependencyInjection;
using Microsoft.Agents.MCP.Client.Initialization;
using Microsoft.Agents.MCP.Client.Transports;
using Microsoft.Agents.MCP.Core;
using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.DependencyInjection;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.SharedMethods.Ping;
using Microsoft.Agents.MCP.Core.JsonRpc;
using Microsoft.Agents.MCP.Core.Transport;
using Microsoft.Agents.MCP.Server.DependencyInjection;
using Microsoft.Agents.MCP.Server.Transports;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
using System.Web;
using Xunit;

namespace Microsoft.Agents.MCP.Tests
{
    public class CallbackTransportTests : TransportTestBase
    {
        protected override IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<SseTransportTests> logger)
        {
            Mock<IHttpClientFactory> httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var clientTransport = new HttpCallbackClientTransport(
                transportManager, 
                httpClientFactoryMock.Object, 
                new Uri("https://localhost/server/"),
                (s) => $"https://localhost/callback/{s}");
            SetupFakeHttpCalls(httpClientFactoryMock, clientTransport, processor, transportManager, logger);
            return clientTransport;
        }

        private void SetupFakeHttpCalls(
            Mock<IHttpClientFactory> httpClientFactoryMock,
            HttpCallbackClientTransport clientTransport,
            IMcpProcessor processor,
            ITransportManager transportManager,
            ILogger<SseTransportTests> logger)
        {
            var handler = new PlumbingHandler(httpClientFactoryMock.Object, clientTransport, processor, transportManager, logger);
            httpClientFactoryMock.Setup(x => x.CreateClient("")).Returns(() => new HttpClient(handler, false));
        }

        private class PlumbingHandler : HttpClientHandler
        {
            private readonly IHttpClientFactory factory;
            private readonly HttpCallbackClientTransport clientTransport;
            private readonly IMcpProcessor processor;
            private ITransportManager transportManager;
            private ILogger logger;

            public PlumbingHandler(IHttpClientFactory factory, HttpCallbackClientTransport clientTransport, IMcpProcessor processor, ITransportManager transportManager, ILogger logger)
            {
                this.factory = factory;
                this.clientTransport = clientTransport;
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
                if (request.Method == HttpMethod.Post && request.RequestUri.ToString().Contains("server"))
                {
                    var payload = JsonSerializer.Deserialize<CallbackJsonRpcPayload>(request.Content.ReadAsStream(), Serialization.GetDefaultMcpSerializationOptions());
                    var sessionId = HttpUtility.ParseQueryString(request.RequestUri.Query).GetValues("sessionId");

                    IMcpTransport transport;
                    if (sessionId == null || sessionId.Length != 0)
                    {
                        transport = new HttpCallbackServerTransport(transportManager, factory, payload.CallbackUrl);
                        await processor.CreateSessionAsync(transport, cancellationToken);
                    }
                    else
                    {
                        if (!transportManager.TryGetTransport(sessionId[0], out transport))
                        {
                            throw new Exception("server transport should have been registered");
                        }
                    }

                    await transport.ProcessPayloadAsync(payload, cancellationToken);

                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                }

                if (request.Method == HttpMethod.Post && request.RequestUri.ToString().Contains("callback"))
                {
                    var payload = JsonSerializer.Deserialize<JsonRpcPayload>(request.Content.ReadAsStream(), Serialization.GetDefaultMcpSerializationOptions());
                    if (!transportManager.TryGetTransport(request.RequestUri.Segments.Last(), out var transport))
                    {
                        throw new Exception("client transport should have been registered");
                    }

                    await transport.ProcessPayloadAsync(payload, cancellationToken);

                    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                }

                throw new Exception("Unsupported method");
            } 
        }
    }
}
