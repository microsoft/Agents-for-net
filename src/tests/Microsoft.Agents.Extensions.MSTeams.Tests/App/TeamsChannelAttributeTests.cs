// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Channels;
using Microsoft.Agents.Extensions.MSTeams.Tests.Model;
using Microsoft.Teams.Apps.Schema;
using Channel = Microsoft.Teams.Apps.Schema.TeamsChannel;
using Moq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Teams.Apps;

namespace Microsoft.Agents.Extensions.MSTeams.Tests.App
{
    public class TeamsChannelAttributeTests
    {
        [Fact]
        public async Task ChannelCreatedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelCreated, "created-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelCreated, app.LastCalledEvent);
            Assert.Equal("created-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelDeletedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelDeleted, "deleted-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelDeleted, app.LastCalledEvent);
            Assert.Equal("deleted-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelMemberAddedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelMemberAdded, "member-added-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelMemberAdded, app.LastCalledEvent);
            Assert.Equal("member-added-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelMemberRemovedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelMemberRemoved, "member-removed-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelMemberRemoved, app.LastCalledEvent);
            Assert.Equal("member-removed-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelRenamedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelRenamed, "renamed-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelRenamed, app.LastCalledEvent);
            Assert.Equal("renamed-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelRestoredAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelRestored, "restored-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelRestored, app.LastCalledEvent);
            Assert.Equal("restored-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelSharedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelShared, "shared-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelShared, app.LastCalledEvent);
            Assert.Equal("shared-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelUnsharedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(ConversationEventType.ChannelUnShared, "unshared-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(ConversationEventType.ChannelUnShared, app.LastCalledEvent);
            Assert.Equal("unshared-channel", app.LastChannelId);
        }

        public static IEnumerable<object[]> AllChannelEventTypes =>
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

        public static IEnumerable<object[]> TeamEventTypes =>
        [
            [ConversationEventType.TeamArchived],
            [ConversationEventType.TeamDeleted],
            [ConversationEventType.TeamRenamed],
            [ConversationEventType.TeamRestored],
            [ConversationEventType.TeamUnarchived],
        ];

        [Theory]
        [MemberData(nameof(AllChannelEventTypes))]
        public async Task ChannelUpdateAttribute_AddRoute_FiresForAnyChannelEvent(ConversationEventType eventType)
        {
            // Arrange
            var (app, turnContext) = CreateChannelUpdateAppAndContext(eventType, "test-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
            Assert.Equal("test-channel", app.LastChannelId);
        }

        [Theory]
        [MemberData(nameof(TeamEventTypes))]
        public async Task ChannelUpdateAttribute_AddRoute_DoesNotFireForTeamEvent(ConversationEventType eventType)
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Team = new Team { Id = "t1" } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
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

        [Fact]
        public async Task ChannelCreatedAttribute_StaticHandler_DoesNotThrowAndFiresRoute()
        {
            TestStaticChannelAttributeApp.HandlerCalled = false;
            var (app, turnContext) = CreateStaticAppAndContext(ConversationEventType.ChannelCreated, "static-channel");

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.True(TestStaticChannelAttributeApp.HandlerCalled);
        }

        private static (TestStaticChannelAttributeApp app, ITurnContext turnContext) CreateStaticAppAndContext(string eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestStaticChannelAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
            return (app, turnContext);
        }

        private static (TestChannelAttributeApp app, ITurnContext turnContext) CreateAppAndContext(string eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
            return (app, turnContext);
        }

        private static (TestChannelUpdateAttributeApp app, ITurnContext turnContext) CreateChannelUpdateAppAndContext(ConversationEventType eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType(eventType.ToString()), Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new TestHttpClientFactory(),
            });
            return (app, turnContext);
        }
    }

    class TestChannelAttributeApp : AgentApplication
    {
        public string LastCalledEvent { get; private set; }
        public string LastChannelId { get; private set; }

        public TestChannelAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsChannelCreatedRoute]
        public Task OnChannelCreatedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelCreated;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelDeletedRoute]
        public Task OnChannelDeletedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelDeleted;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelMemberAddedRoute]
        public Task OnChannelMemberAddedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelMemberAdded;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelMemberRemovedRoute]
        public Task OnChannelMemberRemovedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelMemberRemoved;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelRenamedRoute]
        public Task OnChannelRenamedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelRenamed;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelRestoredRoute]
        public Task OnChannelRestoredAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelRestored;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelSharedRoute]
        public Task OnChannelSharedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelShared;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [TeamsChannelUnsharedRoute]
        public Task OnChannelUnsharedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = ConversationEventType.ChannelUnShared;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }
    }

    class TestChannelUpdateAttributeApp : AgentApplication
    {
        public bool HandlerCalled { get; private set; }
        public string LastChannelId { get; private set; }

        public TestChannelUpdateAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsChannelUpdateRoute]
        public Task OnAnyChannelEventAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }
    }

    // Regression: static route handlers must not throw ArgumentException from CreateDelegate.
    class TestStaticChannelAttributeApp : AgentApplication
    {
        public static bool HandlerCalled;

        public TestStaticChannelAttributeApp(AgentApplicationOptions options) : base(options)
        {
            var extension = new TeamsAgentExtension(this);
            this.RegisterExtension(extension, (ext) => { });
        }

        [TeamsChannelCreatedRoute]
        public static Task OnChannelCreatedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }
}
