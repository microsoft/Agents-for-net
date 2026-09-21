// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Teams;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Microsoft.Teams.Apps.Schema;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Teams.Apps;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class TeamsTeamAttributeTests
    {
        [Fact]
        public async Task TeamArchivedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.TeamArchived, "archived-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.TeamArchived, app.LastCalledEvent);
            Assert.Equal("archived-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamUnarchivedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.TeamUnarchived, "unarchived-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.TeamUnarchived, app.LastCalledEvent);
            Assert.Equal("unarchived-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamDeletedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.TeamDeleted, "deleted-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.TeamDeleted, app.LastCalledEvent);
            Assert.Equal("deleted-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamRenamedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.TeamRenamed, "renamed-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.TeamRenamed, app.LastCalledEvent);
            Assert.Equal("renamed-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamRestoredAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.TeamRestored, "restored-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.TeamRestored, app.LastCalledEvent);
            Assert.Equal("restored-team", app.LastTeamId);
        }

        public static IEnumerable<object[]> AllTeamEventTypes =>
        [
            [ConversationEventType.TeamArchived],
            [ConversationEventType.TeamUnarchived],
            [ConversationEventType.TeamDeleted],
            [ConversationEventType.TeamRenamed],
            [ConversationEventType.TeamRestored],
        ];

        public static IEnumerable<object[]> ChannelEventTypes =>
        [
            [ConversationEventType.ChannelCreated],
            [ConversationEventType.ChannelDeleted],
            [ConversationEventType.ChannelRenamed],
            [ConversationEventType.ChannelRestored],
            [ConversationEventType.ChannelShared],
            [ConversationEventType.ChannelUnShared],
            [ConversationEventType.ChannelMemberAdded],
            [ConversationEventType.ChannelMemberRemoved],
        ];

        [Theory]
        [MemberData(nameof(AllTeamEventTypes))]
        public async Task TeamUpdateAttribute_AddRoute_FiresForAnyTeamEvent(ConversationEventType eventType)
        {
            // Arrange
            var (app, turnContext) = CreateTeamUpdateAppAndContext(eventType, "test-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.Equal("test-team", app.LastTeamId);
        }

        [Theory]
        [MemberData(nameof(ChannelEventTypes))]
        public async Task TeamUpdateAttribute_AddRoute_DoesNotFireForChannelEvent(ConversationEventType eventType)
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Channel = new TeamsChannel { Id = "c1" } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.HandlerCalled);
        }

        private static (TestTeamAttributeApp app, ITurnContext turnContext) CreateAppAndContext(ConversationEventType eventType, string teamId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = new Team { Id = teamId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
            return (app, turnContext);
        }

        private static (TestTeamUpdateAttributeApp app, ITurnContext turnContext) CreateTeamUpdateAppAndContext(ConversationEventType eventType, string teamId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = new Team { Id = teamId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
            return (app, turnContext);
        }
    }

    class TestTeamAttributeApp : AgentApplication
    {
        public string LastCalledEvent { get; private set; }
        public string LastTeamId { get; private set; }

        public TestTeamAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsTeamArchivedRoute]
        public Task OnTeamArchivedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.TeamArchived;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamUnarchivedRoute]
        public Task OnTeamUnarchivedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.TeamUnarchived;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamDeletedRoute]
        public Task OnTeamDeletedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.TeamDeleted;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamRenamedRoute]
        public Task OnTeamRenamedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.TeamRenamed;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamRestoredRoute]
        public Task OnTeamRestoredAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.TeamRestored;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }
    }

    class TestTeamUpdateAttributeApp : AgentApplication
    {
        public bool HandlerCalled { get; private set; }
        public string LastTeamId { get; private set; }

        public TestTeamUpdateAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsTeamUpdateRoute]
        public Task OnAnyTeamEventAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team team, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }
    }
}
