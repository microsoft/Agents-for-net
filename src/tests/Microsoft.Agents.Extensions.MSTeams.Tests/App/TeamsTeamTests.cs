// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
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
    public class TeamsTeamTests
    {
        [Fact]
        public async Task Test_OnTeamEventReceived_MatchesAnyTeamEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "team-123" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamArchived, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedTeamIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnTeamEventReceived((ctx, _, data, ct) =>
                {
                    capturedTeamIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedTeamIds);
            Assert.Equal("team-123", capturedTeamIds[0]);
        }

        [Fact]
        public async Task Test_OnTeamEventReceived_DoesNotMatchChannelEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            // Only channel events — no team events
            var turnContexts = CreateChannelContexts(ConversationEventType.ChannelCreated, new TeamsChannel { Id = "c1" }, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnTeamEventReceived((ctx, _, data, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_OnArchived_MatchesTeamArchived()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "archived-team" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamArchived, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnArchived((ctx, _, data, ct) =>
                {
                    capturedIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedIds);
            Assert.Equal("archived-team", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnArchived_DoesNotMatchOtherTeamEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "t1" };
            // teamDeleted should not trigger an OnArchived handler
            var turnContext = new TurnContext(adapter, CreateTeamActivity(ConversationEventType.TeamDeleted, team));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnArchived((ctx, _, data, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_OnTeamUnarchived_MatchesTeamUnarchived()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "unarchived-team" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamUnarchived, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnUnarchived((ctx, _, data, ct) =>
                {
                    capturedIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedIds);
            Assert.Equal("unarchived-team", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnRenamed_MatchesTeamRenamed()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "renamed-team" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamRenamed, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnRenamed((ctx, _, data, ct) =>
                {
                    capturedIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedIds);
            Assert.Equal("renamed-team", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnRestored_MatchesTeamRestored()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "restored-team" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamRestored, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnRestored((ctx, _, data, ct) =>
                {
                    capturedIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedIds);
            Assert.Equal("restored-team", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnDeleted_MatchesTeamDeleted()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "deleted-team" };
            var turnContexts = CreateTeamContexts(ConversationEventType.TeamDeleted, team, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnDeleted((ctx, _, data, ct) =>
                {
                    capturedIds.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            // Act
            foreach (var ctx in turnContexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert
            Assert.Single(capturedIds);
            Assert.Equal("deleted-team", capturedIds[0]);
        }

        [Fact]
        public async Task Test_TeamHandlers_DoNotFireForNonTeamsChannel()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "t1" };
            // Activity with correct event type but wrong ChannelId
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat,
                ChannelData = new TeamsChannelData { EventType = ConversationEventType.TeamArchived, Team = team },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnArchived((ctx, _, data, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_TeamHandlers_DoNotFireForNonConversationUpdateActivity()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var team = new Team { Id = "t1" };
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = ConversationEventType.TeamArchived, Team = team },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnArchived((ctx, _, data, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_TeamHandlers_DoNotFireWhenTeamDataMissing()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            // TeamsChannelData has the right event type but no Team object
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = ConversationEventType.TeamArchived },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams.OnArchived((ctx, _, data, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.False(called);
        }

        [Fact]
        public async Task Test_MultipleTeamHandlers_OnlyCorrectHandlerFires()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, CreateTeamActivity(ConversationEventType.TeamRenamed, new Team { Id = "t1" }));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var renamedCalled = false;
            var archivedCalled = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Teams
                    .OnRenamed((ctx, _, data, ct) => { renamedCalled = true; return Task.CompletedTask; })
                    .OnArchived((ctx, _, data, ct) => { archivedCalled = true; return Task.CompletedTask; });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(renamedCalled);
            Assert.False(archivedCalled);
        }

        [Fact]
        public async Task Test_TeamsTeam_MethodChainingReturnsSelf()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, CreateTeamActivity(ConversationEventType.TeamArchived, new Team { Id = "t1" }));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var archivedCalled = false;
            var deletedCalled = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                // Verify method chaining works
                var result = ext.Teams
                    .OnArchived((ctx, _, data, ct) => { archivedCalled = true; return Task.CompletedTask; })
                    .OnDeleted((ctx, _, data, ct) => { deletedCalled = true; return Task.CompletedTask; });
                Assert.NotNull(result);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(archivedCalled);
            Assert.False(deletedCalled);
        }

        #region Helpers

        private static AgentApplication CreateApp(Task<ITurnState> turnState)
        {
            return new AgentApplication(new AgentApplicationOptions(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
        }

        private static Activity CreateTeamActivity(string eventType, Team team)
        {
            return new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = team },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
        }

        /// <summary>
        /// Returns an array of TurnContexts where only the first (index 0) should match a team
        /// handler for the given eventType. The remaining contexts are non-matching variations.
        /// </summary>
        private static ITurnContext[] CreateTeamContexts(string eventType, Team team, ChannelAdapter adapter)
        {
            return
            [
                // Matching: correct event type, correct channel
                new TurnContext(adapter, CreateTeamActivity(eventType, team)),
                // Non-matching: channel event — no Team in TeamsChannelData, also won't match "team.*" regex
                // (specific-event tests have a dedicated test for "different team event doesn't match")
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                    ChannelData = new TeamsChannelData { EventType = ConversationEventType.ChannelCreated, Channel = new TeamsChannel { Id = "c-other" } },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
                // Non-matching: wrong ChannelId
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Microsoft.Agents.Core.Models.Channels.Webchat,
                    ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = team },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
                // Non-matching: wrong activity type
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.Message,
                    ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                    ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = team },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
            ];
        }

        private static ITurnContext[] CreateChannelContexts(string eventType, TeamsChannel channel, ChannelAdapter adapter)
        {
            return
            [
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                    ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Channel = channel },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
            ];
        }

        #endregion
    }
}
