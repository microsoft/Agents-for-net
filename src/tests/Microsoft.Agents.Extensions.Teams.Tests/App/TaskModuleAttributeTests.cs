// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Moq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Agents.Extensions.Teams.App.TaskModules;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TaskModuleAttributeTests
    {
        [Fact]
        public async Task FetchRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/fetch", "test-verb");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.FetchHandlerCalled);
            Assert.False(app.SubmitHandlerCalled);
        }

        [Fact]
        public async Task SubmitRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/submit", "test-verb");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.SubmitHandlerCalled);
            Assert.False(app.FetchHandlerCalled);
        }

        [Fact]
        public async Task FetchRouteAttribute_AddRoute_DoesNotFireForWrongVerb()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/fetch", "other-verb");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.FetchHandlerCalled);
        }

        [Fact]
        public async Task SubmitRouteAttribute_AddRoute_DoesNotFireForWrongVerb()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext("task/submit", "other-verb");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.SubmitHandlerCalled);
        }

        private static (TestTaskModuleAttributeApp app, ITurnContext turnContext) CreateAppAndContext(string invokeName, string verb)
        {
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = invokeName,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new
                {
                    data = new { verb }
                }),
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTaskModuleAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }
    }

    class TestTaskModuleAttributeApp : AgentApplication
    {
        public bool FetchHandlerCalled { get; private set; }
        public bool SubmitHandlerCalled { get; private set; }

        public TestTaskModuleAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [FetchRoute("test-verb")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetchAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
        {
            FetchHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [SubmitRoute("test-verb")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmitAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.TaskModules.Request data, CancellationToken cancellationToken)
        {
            SubmitHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }
    }
}
