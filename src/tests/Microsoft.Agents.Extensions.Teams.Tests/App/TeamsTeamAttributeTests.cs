// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.App.TeamsTeams;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Microsoft.Teams.Api;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Teams.Api.Activities.ConversationUpdateActivity;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class TeamsTeamAttributeTests
    {
        [Fact]
        public async Task TeamArchivedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamArchived, "archived-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamArchived, app.LastCalledEvent);
            Assert.Equal("archived-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamUnarchivedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamUnarchived, "unarchived-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamUnarchived, app.LastCalledEvent);
            Assert.Equal("unarchived-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamDeletedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamDeleted, "deleted-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamDeleted, app.LastCalledEvent);
            Assert.Equal("deleted-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamHardDeletedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamHardDeleted, "hard-deleted-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamHardDeleted, app.LastCalledEvent);
            Assert.Equal("hard-deleted-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamRenamedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamRenamed, "renamed-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamRenamed, app.LastCalledEvent);
            Assert.Equal("renamed-team", app.LastTeamId);
        }

        [Fact]
        public async Task TeamRestoredAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.TeamRestored, "restored-team");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.TeamRestored, app.LastCalledEvent);
            Assert.Equal("restored-team", app.LastTeamId);
        }

        public static IEnumerable<object[]> AllTeamEventTypes =>
        [
            [EventType.TeamArchived],
            [EventType.TeamUnarchived],
            [EventType.TeamDeleted],
            [EventType.TeamHardDeleted],
            [EventType.TeamRenamed],
            [EventType.TeamRestored],
        ];

        public static IEnumerable<object[]> ChannelEventTypes =>
        [
            [EventType.ChannelCreated],
            [EventType.ChannelDeleted],
            [EventType.ChannelRenamed],
            [EventType.ChannelRestored],
            [EventType.ChannelShared],
            [EventType.ChannelUnShared],
            [EventType.ChannelMemberAdded],
            [EventType.ChannelMemberRemoved],
        ];

        [Theory]
        [MemberData(nameof(AllTeamEventTypes))]
        public async Task TeamUpdateAttribute_AddRoute_FiresForAnyTeamEvent(EventType eventType)
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
        public async Task TeamUpdateAttribute_AddRoute_DoesNotFireForChannelEvent(EventType eventType)
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Channel = new Channel { Id = "c1" } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(app.HandlerCalled);
        }

        private static (TestTeamAttributeApp app, ITurnContext turnContext) CreateAppAndContext(EventType eventType, string teamId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Team = new Team { Id = teamId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }

        private static (TestTeamUpdateAttributeApp app, ITurnContext turnContext) CreateTeamUpdateAppAndContext(EventType eventType, string teamId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Team = new Team { Id = teamId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestTeamUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
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

        [TeamsTeamAttributes.TeamArchivedRoute]
        public Task OnTeamArchivedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamArchived;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamAttributes.TeamUnarchivedRoute]
        public Task OnTeamUnarchivedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamUnarchived;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamAttributes.TeamDeletedRoute]
        public Task OnTeamDeletedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamDeleted;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamAttributes.TeamHardDeletedRoute]
        public Task OnTeamHardDeletedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamHardDeleted;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamAttributes.TeamRenamedRoute]
        public Task OnTeamRenamedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamRenamed;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }

        [TeamsTeamAttributes.TeamRestoredRoute]
        public Task OnTeamRestoredAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.TeamRestored;
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

        [TeamsTeamAttributes.TeamUpdateRoute]
        public Task OnAnyTeamEventAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team team, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            LastTeamId = team.Id;
            return Task.CompletedTask;
        }
    }
}
