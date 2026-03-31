// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using Microsoft.Agents.Extensions.Teams.App.Meetings;
using Microsoft.Agents.Extensions.Teams.Models;
using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MeetingAttributeTests
    {
        [Fact]
        public async Task MeetingStartRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingStart", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingStartAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MeetingStartRouteAttribute_DoesNotFire_ForWrongEventName()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingEnd", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingStartAppWithAttribute(new(() => turnState.Result)
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
        public async Task MeetingEndRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingEnd", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingEndAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MeetingEndRouteAttribute_DoesNotFire_ForWrongEventName()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingStart", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingEndAppWithAttribute(new(() => turnState.Result)
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
        public async Task MeetingParticipantsJoinRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantJoin", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingParticipantsJoinAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MeetingParticipantsJoinRouteAttribute_DoesNotFire_ForWrongEventName()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantLeave", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingParticipantsJoinAppWithAttribute(new(() => turnState.Result)
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
        public async Task MeetingParticipantsLeaveRouteAttribute_AddRoute_CreatesWorkingRoute()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantLeave", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingParticipantsLeaveAppWithAttribute(new(() => turnState.Result)
            {
                StartTypingTimer = false,
                Connections = new Mock<IConnections>().Object,
                HttpClientFactory = new Mock<IHttpClientFactory>().Object,
            });

            // Act
            await app.OnTurnAsync(turnContext, CancellationToken.None);

            // Assert
            Assert.True(app.HandlerCalled);
        }

        [Fact]
        public async Task MeetingParticipantsLeaveRouteAttribute_DoesNotFire_ForWrongEventName()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContext = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantJoin", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContext);

            var app = new TestMeetingParticipantsLeaveAppWithAttribute(new(() => turnState.Result)
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

        private static ITurnContext CreateMeetingTurnContext(string activityName, ChannelAdapter adapter)
        {
            return new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Event,
                ChannelId = Channels.Msteams,
                Name = activityName,
                Id = "test.id",
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
        }
    }

    [TeamsExtension]
    partial class TestMeetingStartAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; private set; }

        public TestMeetingStartAppWithAttribute(AgentApplicationOptions options) : base(options) { }

        [MeetingStartRoute]
        public Task OnMeetingStartAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.Meetings.MeetingDetails meeting,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    [TeamsExtension]
    partial class TestMeetingEndAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; private set; }

        public TestMeetingEndAppWithAttribute(AgentApplicationOptions options) : base(options) { }

        [MeetingEndRoute]
        public Task OnMeetingEndAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            Microsoft.Teams.Api.Meetings.MeetingDetails meeting,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    [TeamsExtension]
    partial class TestMeetingParticipantsJoinAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; private set; }

        public TestMeetingParticipantsJoinAppWithAttribute(AgentApplicationOptions options) : base(options) { }

        [MeetingParticipantsJoinRoute]
        public Task OnParticipantsJoinAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            MeetingParticipantsEventDetails participants,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }

    [TeamsExtension]
    partial class TestMeetingParticipantsLeaveAppWithAttribute : AgentApplication
    {
        public bool HandlerCalled { get; private set; }

        public TestMeetingParticipantsLeaveAppWithAttribute(AgentApplicationOptions options) : base(options) { }

        [MeetingParticipantsLeaveRoute]
        public Task OnParticipantsLeaveAsync(
            ITurnContext turnContext,
            ITurnState turnState,
            MeetingParticipantsEventDetails participants,
            CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            return Task.CompletedTask;
        }
    }
}
