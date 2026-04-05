// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
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
    public class TeamsChannelTests
    {
        [Fact]
        public async Task Test_OnChannelEventReceived_MatchesAnyChannelEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "channel-123" };
            var turnContexts = CreateChannelContexts(EventType.ChannelCreated, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnChannelEventReceived((ctx, _, data, ct) =>
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
            Assert.Equal("channel-123", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnChannelEventReceived_DoesNotMatchTeamEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            // Only team events — no channel events
            var turnContexts = CreateTeamContexts(EventType.TeamArchived, new Team { Id = "t1" }, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnChannelEventReceived((ctx, _, data, ct) =>
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
        public async Task Test_OnCreated_MatchesChannelCreated()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "new-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelCreated, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnCreated((ctx, _, data, ct) =>
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
            Assert.Equal("new-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnCreated_DoesNotMatchOtherChannelEvent()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "c1" };
            // channelDeleted should not trigger an OnCreated handler
            var turnContext = new TurnContext(adapter, CreateChannelActivity(EventType.ChannelDeleted, channel));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var called = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnCreated((ctx, _, data, ct) =>
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
        public async Task Test_OnDeleted_MatchesChannelDeleted()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "deleted-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelDeleted, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnDeleted((ctx, _, data, ct) =>
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
            Assert.Equal("deleted-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnRenamed_MatchesChannelRenamed()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "renamed-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelRenamed, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnRenamed((ctx, _, data, ct) =>
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
            Assert.Equal("renamed-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnShared_MatchesChannelShared()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "shared-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelShared, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnShared((ctx, _, data, ct) =>
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
            Assert.Equal("shared-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnUnShared_MatchesChannelUnShared()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "unshared-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelUnShared, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnUnShared((ctx, _, data, ct) =>
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
            Assert.Equal("unshared-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnRestored_MatchesChannelRestored()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "restored-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelRestored, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnRestored((ctx, _, data, ct) =>
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
            Assert.Equal("restored-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnMemberAdded_MatchesChannelMemberAdded()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "member-added-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelMemberAdded, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnMemberAdded((ctx, _, data, ct) =>
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
            Assert.Equal("member-added-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_OnMemberRemoved_MatchesChannelMemberRemoved()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "member-removed-channel" };
            var turnContexts = CreateChannelContexts(EventType.ChannelMemberRemoved, channel, adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);
            var app = CreateApp(turnState);
            var capturedIds = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnMemberRemoved((ctx, _, data, ct) =>
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
            Assert.Equal("member-removed-channel", capturedIds[0]);
        }

        [Fact]
        public async Task Test_ChannelHandlers_DoNotFireForNonTeamsChannel()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "c1" };
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Webchat,
                ChannelData = new ChannelData { EventType = EventType.ChannelCreated, Channel = channel },
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
                ext.Channels.OnCreated((ctx, _, data, ct) =>
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
        public async Task Test_ChannelHandlers_DoNotFireForNonConversationUpdateActivity()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var channel = new Channel { Id = "c1" };
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = EventType.ChannelCreated, Channel = channel },
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
                ext.Channels.OnCreated((ctx, _, data, ct) =>
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
        public async Task Test_ChannelHandlers_DoNotFireWhenChannelDataMissing()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            // ChannelData has the right event type but no Channel object
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = EventType.ChannelCreated },
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
                ext.Channels.OnCreated((ctx, _, data, ct) =>
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
        public async Task Test_MultipleChannelHandlers_OnlyCorrectHandlerFires()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, CreateChannelActivity(EventType.ChannelRenamed, new Channel { Id = "c1" }));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var renamedCalled = false;
            var createdCalled = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels
                    .OnRenamed((ctx, _, data, ct) => { renamedCalled = true; return Task.CompletedTask; })
                    .OnCreated((ctx, _, data, ct) => { createdCalled = true; return Task.CompletedTask; });
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(renamedCalled);
            Assert.False(createdCalled);
        }

        [Fact]
        public async Task Test_TeamsChannel_MethodChainingReturnsSelf()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, CreateChannelActivity(EventType.ChannelCreated, new Channel { Id = "c1" }));
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = CreateApp(turnState);
            var createdCalled = false;
            var deletedCalled = false;
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                // Verify method chaining works
                var result = ext.Channels
                    .OnCreated((ctx, _, data, ct) => { createdCalled = true; return Task.CompletedTask; })
                    .OnDeleted((ctx, _, data, ct) => { deletedCalled = true; return Task.CompletedTask; });
                Assert.NotNull(result);
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(createdCalled);
            Assert.False(deletedCalled);
        }

        [Fact]
        public async Task Test_OnChannelEventReceived_MultipleChannelEvents_AllMatch()
        {
            // Arrange - register OnChannelEventReceived and send two different channel events
            var adapter = new NotImplementedAdapter();
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(
                new TurnContext(adapter, CreateChannelActivity(EventType.ChannelCreated, new Channel { Id = "c1" })));
            var app = CreateApp(turnState);
            var capturedEvents = new List<string>();
            var extension = new TeamsAgentExtension(app);
            app.RegisterExtension(extension, (ext) =>
            {
                ext.Channels.OnChannelEventReceived((ctx, _, data, ct) =>
                {
                    capturedEvents.Add(data.Id);
                    return Task.CompletedTask;
                });
            });

            var contexts = new[]
            {
                new TurnContext(adapter, CreateChannelActivity(EventType.ChannelCreated, new Channel { Id = "created" })),
                new TurnContext(adapter, CreateChannelActivity(EventType.ChannelDeleted, new Channel { Id = "deleted" })),
                new TurnContext(adapter, CreateChannelActivity(EventType.ChannelRenamed, new Channel { Id = "renamed" })),
            };

            // Act
            foreach (var ctx in contexts)
                await app.OnTurnAsync(ctx, CancellationToken.None);

            // Assert — all three channel events should trigger the general handler
            Assert.Equal(3, capturedEvents.Count);
            Assert.Contains("created", capturedEvents);
            Assert.Contains("deleted", capturedEvents);
            Assert.Contains("renamed", capturedEvents);
        }

        #region Helpers

        private static AgentApplication CreateApp(Task<ITurnState> turnState)
        {
            return new AgentApplication(new AgentApplicationOptions(() => turnState.Result)
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
        }

        private static Activity CreateChannelActivity(string eventType, Channel channel)
        {
            return new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Channel = channel },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            };
        }

        /// <summary>
        /// Returns an array of TurnContexts where only the first (index 0) should match a channel
        /// handler for the given eventType. The remaining contexts are non-matching variations.
        /// </summary>
        private static ITurnContext[] CreateChannelContexts(string eventType, Channel channel, ChannelAdapter adapter)
        {
            return
            [
                // Matching: correct event type, correct ChannelId
                new TurnContext(adapter, CreateChannelActivity(eventType, channel)),
                // Non-matching: team event — no Channel in ChannelData, also won't match "channel.*" regex
                // (specific-event tests have a dedicated test for "different channel event doesn't match")
                // Non-matching: team event (not a channel event)
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Channels.Msteams,
                    ChannelData = new ChannelData { EventType = EventType.TeamArchived, Team = new Team { Id = "t1" } },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
                // Non-matching: wrong ChannelId
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Channels.Webchat,
                    ChannelData = new ChannelData { EventType = eventType, Channel = channel },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
                // Non-matching: wrong activity type
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.Message,
                    ChannelId = Channels.Msteams,
                    ChannelData = new ChannelData { EventType = eventType, Channel = channel },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
            ];
        }

        private static ITurnContext[] CreateTeamContexts(string eventType, Team team, ChannelAdapter adapter)
        {
            return
            [
                new TurnContext(adapter, new Activity
                {
                    Type = ActivityTypes.ConversationUpdate,
                    ChannelId = Channels.Msteams,
                    ChannelData = new ChannelData { EventType = eventType, Team = team },
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From = new() { Id = "fromId" },
                }),
            ];
        }

        #endregion
    }
}
