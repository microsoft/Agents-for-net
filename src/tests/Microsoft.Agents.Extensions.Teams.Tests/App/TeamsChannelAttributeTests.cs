// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
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
    public class TeamsChannelAttributeTests
    {
        [Fact]
        public async Task ChannelCreatedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelCreated, "created-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelCreated, app.LastCalledEvent);
            Assert.Equal("created-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelDeletedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelDeleted, "deleted-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelDeleted, app.LastCalledEvent);
            Assert.Equal("deleted-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelMemberAddedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelMemberAdded, "member-added-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelMemberAdded, app.LastCalledEvent);
            Assert.Equal("member-added-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelMemberRemovedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelMemberRemoved, "member-removed-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelMemberRemoved, app.LastCalledEvent);
            Assert.Equal("member-removed-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelRenamedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelRenamed, "renamed-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelRenamed, app.LastCalledEvent);
            Assert.Equal("renamed-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelRestoredAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelRestored, "restored-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelRestored, app.LastCalledEvent);
            Assert.Equal("restored-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelSharedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelShared, "shared-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelShared, app.LastCalledEvent);
            Assert.Equal("shared-channel", app.LastChannelId);
        }

        [Fact]
        public async Task ChannelUnSharedAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var (app, turnContext) = CreateAppAndContext(EventType.ChannelUnShared, "unshared-channel");

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.Equal(EventType.ChannelUnShared, app.LastCalledEvent);
            Assert.Equal("unshared-channel", app.LastChannelId);
        }

        public static IEnumerable<object[]> AllChannelEventTypes =>
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

        public static IEnumerable<object[]> TeamEventTypes =>
        [
            [EventType.TeamArchived],
            [EventType.TeamDeleted],
            [EventType.TeamHardDeleted],
            [EventType.TeamRenamed],
            [EventType.TeamRestored],
            [EventType.TeamUnarchived],
        ];

        [Theory]
        [MemberData(nameof(AllChannelEventTypes))]
        public async Task ChannelUpdateAttribute_AddRoute_FiresForAnyChannelEvent(EventType eventType)
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
        public async Task ChannelUpdateAttribute_AddRoute_DoesNotFireForTeamEvent(EventType eventType)
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Team = new Team { Id = "t1" } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
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

        [Fact]
        public async Task ChannelCreatedAttribute_StaticHandler_DoesNotThrowAndFiresRoute()
        {
            TestStaticChannelAttributeApp.HandlerCalled = false;
            var (app, turnContext) = CreateStaticAppAndContext(EventType.ChannelCreated, "static-channel");

            await app.OnTurnAsync(turnContext, CancellationToken.None);

            Assert.True(TestStaticChannelAttributeApp.HandlerCalled);
        }

        private static (TestStaticChannelAttributeApp app, ITurnContext turnContext) CreateStaticAppAndContext(string eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestStaticChannelAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }

        private static (TestChannelAttributeApp app, ITurnContext turnContext) CreateAppAndContext(string eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });
            return (app, turnContext);
        }

        private static (TestChannelUpdateAttributeApp app, ITurnContext turnContext) CreateChannelUpdateAppAndContext(EventType eventType, string channelId)
        {
            var adapter = new NotImplementedAdapter();
            var turnContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Channels.Msteams,
                ChannelData = new ChannelData { EventType = eventType, Channel = new Channel { Id = channelId } },
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);
            var app = new TestChannelUpdateAttributeApp(new AgentApplicationOptions(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
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

        [ChannelCreatedRoute]
        public Task OnChannelCreatedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelCreated;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelDeletedRoute]
        public Task OnChannelDeletedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelDeleted;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelMemberAddedRoute]
        public Task OnChannelMemberAddedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelMemberAdded;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelMemberRemovedRoute]
        public Task OnChannelMemberRemovedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelMemberRemoved;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelRenamedRoute]
        public Task OnChannelRenamedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelRenamed;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelRestoredRoute]
        public Task OnChannelRestoredAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelRestored;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelSharedRoute]
        public Task OnChannelSharedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelShared;
            LastChannelId = channel.Id;
            return Task.CompletedTask;
        }

        [ChannelUnSharedRoute]
        public Task OnChannelUnSharedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            LastCalledEvent = EventType.ChannelUnShared;
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

        [ChannelUpdateRoute]
        public Task OnAnyChannelEventAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
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

        [ChannelCreatedRoute]
        public static Task OnChannelCreatedAsync(ITurnContext turnContext, ITurnState turnState, Channel channel, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }
}
