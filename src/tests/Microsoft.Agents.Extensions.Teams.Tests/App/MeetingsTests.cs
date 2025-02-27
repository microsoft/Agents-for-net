// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Tests.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests.App
{
    public class MeetingsTests
    {
        [Fact]
        public async Task Test_OnStart()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingStart", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new TeamsApplication(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var ids = new List<string>();
            app.Meetings.OnStart((context, _, _, _) =>
            {
                ids.Add(context.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnEnd()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingEnd", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new TeamsApplication(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var ids = new List<string>();
            app.Meetings.OnEnd((context, _, _, _) =>
            {
                ids.Add(context.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnParticipantsJoin()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantJoin", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new TeamsApplication(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var ids = new List<string>();
            app.Meetings.OnParticipantsJoin((context, _, _, _) =>
            {
                ids.Add(context.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        [Fact]
        public async Task Test_OnParticipantsLeave()
        {
            // Arrange
            var adapter = new NotImplementedAdapter();
            var turnContexts = CreateMeetingTurnContext("application/vnd.microsoft.meetingParticipantLeave", adapter);
            var turnState = TurnStateConfig.GetTurnStateWithConversationStateAsync(turnContexts[0]);

            var app = new TeamsApplication(new()
            {
                RemoveRecipientMention = false,
                StartTypingTimer = false,
                TurnStateFactory = () => turnState.Result,
            });
            var ids = new List<string>();
            app.Meetings.OnParticipantsLeave((context, _, _, _) =>
            {
                ids.Add(context.Activity.Id);
                return Task.CompletedTask;
            });

            // Act
            foreach (var turnContext in turnContexts)
            {
                await app.OnTurnAsync(turnContext, CancellationToken.None);
            }

            // Assert
            Assert.Single(ids);
            Assert.Equal("test.id", ids[0]);
        }

        private static TurnContext[] CreateMeetingTurnContext(string activityName, ChannelAdapter adapter)
        {
            return new TurnContext[]
            {
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Msteams,
                    Name = activityName,
                    Id = "test.id",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Msteams,
                    Name = "fake.name",
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Invoke,
                    ChannelId = Channels.Msteams,
                    Name = activityName,
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
                new(adapter, new Activity
                {
                    Type = ActivityTypes.Event,
                    ChannelId = Channels.Webchat,
                    Name = activityName,
                    Recipient = new() { Id = "recipientId" },
                    Conversation = new() { Id = "conversationId" },
                    From =  new() { Id = "fromId" },
                }),
            };
        }
    }
}
