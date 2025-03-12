using Microsoft.Agents.Mcp.Client.Transports;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Server.AspNet;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Xunit;

namespace Microsoft.Agents.Mcp.Tests
{
    public class CallbackTransportTests : TransportTestBase
    {
        protected override IMcpTransport CreateTransport(IMcpProcessor processor, ITransportManager transportManager, ILogger<TransportTestBase> logger)
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
            ILogger<TransportTestBase> logger)
        {
            var handler = new PlumbingHandler(httpClientFactoryMock.Object, clientTransport, processor, transportManager, logger);
            httpClientFactoryMock.Setup(x => x.CreateClient("")).Returns(() => new HttpClient(handler, false));
        }

        [Fact]
        public void HttpCallbackClientTransport_ShouldInitializeCorrectly()
        {
            // Arrange
            var transportManagerMock = new Mock<ITransportManager>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var endpoint = new Uri("https://localhost/server/");
            Func<string, string> callbackEndpointFunc = (s) => $"https://localhost/callback/{s}";

            // Act
            var transport = new HttpCallbackClientTransport(transportManagerMock.Object, httpClientFactoryMock.Object, endpoint, callbackEndpointFunc);

            // Assert
            Assert.NotNull(transport);
        }

        [Fact]
        public async Task CloseAsync_ShouldCloseTransport()
        {
            // Arrange
            var transportManagerMock = new Mock<ITransportManager>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var endpoint = new Uri("https://localhost/server/");
            Func<string, string> callbackEndpointFunc = (s) => $"https://localhost/callback/{s}";
            var transport = new HttpCallbackClientTransport(transportManagerMock.Object, httpClientFactoryMock.Object, endpoint, callbackEndpointFunc);
            var cancellationToken = new CancellationToken();

            var httpClientMock = new Mock<HttpClient>();
            httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClientMock.Object);

            // Act
            await transport.CloseAsync(cancellationToken);

            // Assert
            Assert.True(transport.IsClosed);
        }

        [Fact]
        public async Task SendOutgoingAsync_ShouldSendPayload()
        {
            // Arrange
            var transportManagerMock = new Mock<ITransportManager>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var endpoint = new Uri("https://localhost/server/");
            Func<string, string> callbackEndpointFunc = (s) => $"https://localhost/callback/{s}";
            var transport = new HttpCallbackClientTransport(transportManagerMock.Object, httpClientFactoryMock.Object, endpoint, callbackEndpointFunc);
            var payload = new JsonRpcPayload { Method = "testMethod", Params = JsonSerializer.SerializeToElement(new { param1 = "value1" }) };
            var cancellationToken = new CancellationToken();

            var httpClientMock = new Mock<HttpClient>();
            httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClientMock.Object);

            // Mock the SessionId using reflection
            var sessionIdProperty = typeof(HttpCallbackClientTransport).GetProperty("SessionId");
            sessionIdProperty.SetValue(transport, "test-session-id");

            // Act
            await transport.SendOutgoingAsync(payload, cancellationToken);

            // Assert
            httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once);
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
