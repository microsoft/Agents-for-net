// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class TeamsApiClientExtensionsTests
    {
        [Fact]
        public async Task SetTeamsApiClient_UsesTurnUserTokenRestTransport()
        {
            var regionalEndpoint = new Uri("https://regional.token.example/");
            Uri requestedUri = null;
            var tokenHttpClient = new HttpClient(new CallbackHandler(request =>
            {
                requestedUri = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"connectionName":"connection","token":"token"}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }));

            var userTokenClient = CreateRestTransport<IUserTokenClient>(regionalEndpoint, tokenHttpClient);
            var connectorClient = CreateRestTransport<IConnectorClient>(
                new Uri("https://smba.trafficmanager.net/amer/"),
                CreateResponseClient("{}"));
            var turnContext = CreateTurnContext(connectorClient.Object, userTokenClient.Object);
            var app = CreateApplication(turnContext, CreateResponseClient(
                """{"connectionName":"connection","token":"token"}"""));
            var extension = new TeamsAgentExtension(app);
            app.OnActivity(ActivityTypes.Message, async (context, _, cancellationToken) =>
            {
                await extension.GetTeamsClient(context).UserToken.GetAsync(
                    "user",
                    "connection",
                    Microsoft.Agents.Core.Models.Channels.Msteams,
                    cancellationToken: cancellationToken);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.NotNull(requestedUri);
            Assert.Equal(regionalEndpoint.Host, requestedUri.Host);
        }

        [Fact]
        public async Task SetTeamsApiClient_UsesTurnConnectorRestTransport()
        {
            var regionalEndpoint = new Uri("https://regional.service.example/");
            Uri requestedUri = null;
            var connectorHttpClient = new HttpClient(new CallbackHandler(request =>
            {
                requestedUri = request.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                };
            }));

            var connectorClient = CreateRestTransport<IConnectorClient>(regionalEndpoint, connectorHttpClient);
            var userTokenClient = CreateRestTransport<IUserTokenClient>(
                new Uri("https://token.botframework.com/"),
                CreateResponseClient("{}"));
            var turnContext = CreateTurnContext(connectorClient.Object, userTokenClient.Object);
            var app = CreateApplication(turnContext, CreateResponseClient("{}"));
            var extension = new TeamsAgentExtension(app);
            app.OnActivity(ActivityTypes.Message, async (context, _, cancellationToken) =>
            {
                await extension.GetTeamsClient(context).Teams.GetByIdAsync("team", cancellationToken);
            });

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.NotNull(requestedUri);
            Assert.Equal(regionalEndpoint.Host, requestedUri.Host);
        }

        [Fact]
        public async Task SetTeamsApiClient_ThrowsWhenConnectorClientIsNotRestTransport()
        {
            var connectorClient = new Mock<IConnectorClient>();
            var userTokenClient = CreateRestTransport<IUserTokenClient>(
                new Uri("https://token.botframework.com/"),
                CreateResponseClient("{}"));
            var turnContext = CreateTurnContext(connectorClient.Object, userTokenClient.Object);
            var app = CreateApplication(turnContext, CreateResponseClient("{}"));
            _ = new TeamsAgentExtension(app);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => app.OnTurnAsync(turnContext, CancellationToken.None));

            Assert.Contains(nameof(IConnectorClient), exception.Message);
            Assert.Contains(nameof(IRestTransport), exception.Message);
        }

        [Fact]
        public async Task SetTeamsApiClient_ThrowsWhenUserTokenClientIsNotRestTransport()
        {
            var connectorClient = CreateRestTransport<IConnectorClient>(
                new Uri("https://smba.trafficmanager.net/amer/"),
                CreateResponseClient("{}"));
            var userTokenClient = new Mock<IUserTokenClient>();
            var turnContext = CreateTurnContext(connectorClient.Object, userTokenClient.Object);
            var app = CreateApplication(turnContext, CreateResponseClient("{}"));
            _ = new TeamsAgentExtension(app);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => app.OnTurnAsync(turnContext, CancellationToken.None));

            Assert.Contains(nameof(IUserTokenClient), exception.Message);
            Assert.Contains(nameof(IRestTransport), exception.Message);
        }

        [Fact]
        public async Task SetTeamsApiClient_ThrowsWhenConnectorTransportEndpointIsNull()
        {
            var connectorClient = CreateRestTransport<IConnectorClient>(
                endpoint: null,
                CreateResponseClient("{}"));
            var userTokenClient = CreateRestTransport<IUserTokenClient>(
                new Uri("https://token.botframework.com/"),
                CreateResponseClient("{}"));
            var turnContext = CreateTurnContext(connectorClient.Object, userTokenClient.Object);
            var app = CreateApplication(turnContext, CreateResponseClient("{}"));
            _ = new TeamsAgentExtension(app);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => app.OnTurnAsync(turnContext, CancellationToken.None));

            Assert.Contains(nameof(IConnectorClient), exception.Message);
            Assert.Contains(nameof(IRestTransport.Endpoint), exception.Message);
        }

        private static Mock<TClient> CreateRestTransport<TClient>(Uri endpoint, HttpClient httpClient)
            where TClient : class
        {
            var client = new Mock<TClient>();
            var transport = client.As<IRestTransport>();
            transport.SetupGet(value => value.Endpoint).Returns(endpoint);
            transport.Setup(value => value.GetHttpClientAsync()).ReturnsAsync(httpClient);
            return client;
        }

        private static TurnContext CreateTurnContext(
            IConnectorClient connectorClient,
            IUserTokenClient userTokenClient)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ServiceUrl = "https://smba.trafficmanager.net/amer/",
                From = new ChannelAccount("user"),
                Recipient = new ChannelAccount("agent"),
                Conversation = new ConversationAccount(id: "conversation")
            };
            var turnContext = new TurnContext(new SimpleAdapter(), activity, new ClaimsIdentity());
            turnContext.Services.Set(connectorClient);
            turnContext.Services.Set(userTokenClient);
            return turnContext;
        }

        private static AgentApplication CreateApplication(TurnContext turnContext, HttpClient fallbackHttpClient)
        {
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            return new AgentApplication(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new StaticHttpClientFactory(fallbackHttpClient),
                LoggerFactory = NullLoggerFactory.Instance
            });
        }

        private static HttpClient CreateResponseClient(string content)
        {
            return new HttpClient(new CallbackHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                }));
        }

        private sealed class CallbackHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(callback(request));
            }
        }

        private sealed class StaticHttpClientFactory(HttpClient httpClient) : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => httpClient;
        }
    }
}
