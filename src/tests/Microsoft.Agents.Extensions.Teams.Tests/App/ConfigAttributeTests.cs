// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Configurations;
using Moq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class ConfigAttributeTests
    {
        [Fact]
        public async Task ConfigurationFetchRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.FetchHandlerCalled);
            Assert.False(app.SubmitHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationSubmitRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/submit");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.SubmitHandlerCalled);
            Assert.False(app.FetchHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationFetchRouteAttribute_DoesNotFire_ForSubmitInvoke()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/submit");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.FetchHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationSubmitRouteAttribute_DoesNotFire_ForFetchInvoke()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.SubmitHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationFetchRouteAttribute_DoesNotFire_ForWrongInvokeName()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.FetchHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationSubmitRouteAttribute_DoesNotFire_ForWrongInvokeName()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/submit");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.SubmitHandlerCalled);
        }

        [Fact]
        public async Task ConfigurationFetchRouteAttribute_HandlerReceivesConfigData()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/fetch");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.FetchHandlerCalled);
            Assert.NotNull(app.ReceivedConfigData);
        }

        [Fact]
        public async Task ConfigurationSubmitRouteAttribute_HandlerReceivesConfigData()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("config/submit");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.SubmitHandlerCalled);
            Assert.NotNull(app.ReceivedConfigData);
        }

        [Fact]
        public async Task ConfigurationFetchRouteAttribute_DoesNotFire_ForNonInvokeActivity()
        {
            // Arrange
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestConfigAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.FetchHandlerCalled);
        }

        private static (TestConfigAttributeApp app, ITurnContext turnContext) CreateAppAndContext(
            string invokeName)
        {
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = invokeName,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    entityId = "config-entity-123",
                    settings = new { theme = "dark" }
                }),
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestConfigAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }
    }

    [TeamsExtension]
    partial class TestConfigAttributeApp : AgentApplication
    {
        public bool FetchHandlerCalled { get; private set; }
        public bool SubmitHandlerCalled { get; private set; }
        public object ReceivedConfigData { get; private set; }

        public TestConfigAttributeApp(AgentApplicationOptions options) : base(options) { }

        [ConfigurationFetchRoute]
        public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnConfigFetchAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            object configData,
            CancellationToken cancellationToken)
        {
            FetchHandlerCalled = true;
            ReceivedConfigData = configData;
            return Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
        }

        [ConfigurationSubmitRoute]
        public Task<Microsoft.Teams.Api.Config.ConfigResponse> OnConfigSubmitAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            object configData,
            CancellationToken cancellationToken)
        {
            SubmitHandlerCalled = true;
            ReceivedConfigData = configData;
            return Task.FromResult(new Microsoft.Teams.Api.Config.ConfigResponse());
        }
    }
}
