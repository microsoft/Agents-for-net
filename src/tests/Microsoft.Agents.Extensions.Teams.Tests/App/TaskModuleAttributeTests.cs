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

        [Fact]
        public async Task FetchRouteAttribute_CustomKeyName_Matches()
        {
            // Arrange — activity data uses "action" key; attribute route registered with keyName: "action"
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/fetch", keyName: "action", keyValue: "fetch-action");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.CustomKeyFetchHandlerCalled);
        }

        [Fact]
        public async Task FetchRouteAttribute_CustomKeyName_DefaultKeyNotMatched()
        {
            // Arrange — activity data uses default "verb" key; route registered with keyName "action" should not fire
            var (app, turnContext) = CreateAppAndContext("task/fetch", "fetch-action");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.CustomKeyFetchHandlerCalled);
        }

        [Fact]
        public async Task FetchRouteAttribute_NullKeyValue_MatchesAnyFetch()
        {
            // Arrange — activity data has no "verb" field; null keyValue attribute should still fire
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/fetch", keyName: null, keyValue: null);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.NullKeyFetchHandlerCalled);
        }

        [Fact]
        public async Task SubmitRouteAttribute_CustomKeyName_Matches()
        {
            // Arrange — activity data uses "action" key; attribute route registered with keyName: "action"
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/submit", keyName: "action", keyValue: "submit-action");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.CustomKeySubmitHandlerCalled);
        }

        [Fact]
        public async Task SubmitRouteAttribute_NullKeyValue_MatchesAnySubmit()
        {
            // Arrange — activity data has no "verb" field; null keyValue attribute should still fire
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/submit", keyName: null, keyValue: null);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.NullKeySubmitHandlerCalled);
        }

        [Fact]
        public async Task SubmitRouteAttribute_CustomKeyName_DefaultKeyNotMatched()
        {
            // Arrange — activity uses default "verb" key but the route expects keyName "action"
            var (app, turnContext) = CreateAppAndContext("task/submit", "submit-action");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert — CustomKeySubmitHandler looks at "action" field, so it must not fire
            Assert.False(app.CustomKeySubmitHandlerCalled);
        }

        [Fact]
        public async Task FetchRoute_NullKeyValue_DoesNotFireForSubmitActivity()
        {
            // Arrange — [FetchRoute] (any) registered; incoming activity is task/submit — must not fire
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/submit", keyName: null, keyValue: null);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert — null-key fetch handler must not fire on submit activities
            Assert.False(app.NullKeyFetchHandlerCalled);
        }

        [Fact]
        public async Task SubmitRoute_NullKeyValue_DoesNotFireForFetchActivity()
        {
            // Arrange — [SubmitRoute] (any) registered; incoming activity is task/fetch — must not fire
            var (app, turnContext) = CreateAppAndContextWithKeyName("task/fetch", keyName: null, keyValue: null);

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert — null-key submit handler must not fire on fetch activities
            Assert.False(app.NullKeySubmitHandlerCalled);
        }

        private static (TestTaskModuleAttributeApp app, ITurnContext turnContext) CreateAppAndContextWithKeyName(string invokeName, string keyName, string keyValue)
        {
            IActivity[] activitiesToSend = null;
            var adapter = new SimpleAdapter((activities) => activitiesToSend = activities);
            object dataPayload = keyName != null && keyValue != null
                ? (object)new { action = keyValue }
                : new { unrelated = "data" };
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Invoke,
                Name = invokeName,
                Value = ProtocolJsonSerializer.ToObject<JsonElement>(new { data = dataPayload }),
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

    [Microsoft.Agents.Extensions.Teams.App.TeamsExtension]
    partial class TestTaskModuleAttributeApp : AgentApplication
    {
        public bool FetchHandlerCalled { get; private set; }
        public bool SubmitHandlerCalled { get; private set; }
        public bool CustomKeyFetchHandlerCalled { get; private set; }
        public bool CustomKeySubmitHandlerCalled { get; private set; }
        public bool NullKeyFetchHandlerCalled { get; private set; }
        public bool NullKeySubmitHandlerCalled { get; private set; }

        public TestTaskModuleAttributeApp(AgentApplicationOptions options) : base(options)
        {
            // TeamsAgentExtension is now lazily initialized via the source-generated Teams property.
            // No manual RegisterExtension call needed.
        }

        [FetchRoute("test-verb")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetchAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            FetchHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [SubmitRoute("test-verb")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmitAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            SubmitHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [FetchRoute("fetch-action", keyName: "action")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetchCustomKeyAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            CustomKeyFetchHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [SubmitRoute("submit-action", keyName: "action")]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmitCustomKeyAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            CustomKeySubmitHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [FetchRoute]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnFetchAnyAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            NullKeyFetchHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }

        [SubmitRoute]
        public Task<Microsoft.Teams.Api.TaskModules.Response> OnSubmitAnyAsync(
            ITurnContext turnContext, ITurnState turnState,
            Microsoft.Teams.Api.TaskModules.Request data,
            CancellationToken cancellationToken)
        {
            NullKeySubmitHandlerCalled = true;
            return Task.FromResult(new Microsoft.Teams.Api.TaskModules.Response());
        }
    }
}
