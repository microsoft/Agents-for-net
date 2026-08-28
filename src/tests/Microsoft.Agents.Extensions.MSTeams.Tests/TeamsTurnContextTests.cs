// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.MSTeams.Models;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests
{
    public class TeamsTurnContextTests
    {
        // ── SendTargetedActivityAsync ─────────────────────────────────────────

        [Fact]
        public async Task SendTargetedActivityAsync_SentActivityHasTargetedTreatment()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            Assert.Same(TargetUser, sent.Recipient);
            var treatment = Assert.Single(sent.Entities.OfType<ActivityTreatment>());
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_OriginalActivityIsNotModified()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "original", Recipient = TargetUser };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            // The original's Entities should not contain any targeted treatment
            Assert.DoesNotContain(activity.Entities ?? [], e => e is ActivityTreatment);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_OriginalActivityWithEntitiesIsNotModified()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var originalEntity = new Entity { Type = "custom" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = TargetUser,
                Entities = [originalEntity]
            };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            // Original still has exactly one entity
            Assert.Single(activity.Entities);
            Assert.Same(originalEntity, activity.Entities[0]);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_PreservesExistingEntitiesOnClone()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = TargetUser,
                Entities = [new Entity { Type = "custom" }]
            };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            // Sent activity has the original entity plus the targeted treatment
            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            Assert.Equal(2, sent.Entities.Count);
            Assert.Contains(sent.Entities, e => e.Type == "custom");
            Assert.Contains(sent.Entities.OfType<ActivityTreatment>(),
                t => t.Treatment == ActivityTreatmentTypes.Targeted);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_ReturnsResourceResponse()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Id = "msg-1", Recipient = TargetUser };

            var response = await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            // SimpleAdapter echoes the Id back
            Assert.NotNull(response);
            Assert.Equal("msg-1", response.Id);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_SentActivityIsAClone()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello", Recipient = TargetUser };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            // Sent activity is a different object instance from the original
            Assert.NotNull(captured);
            Assert.NotSame(activity, captured[0]);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_TargetedInbound_ReplacesQuotedReplyWithPromptPreview()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreatePromptPreviewTurnContext(adapter);
            ITeamsActivity outbound = new TeamsActivity
            {
                Type = ActivityTypes.Message,
                Text = string.Empty,
                Recipient = TargetUser
            };
            outbound.AddQuotedReply("quoted-message", "response");

            await turnContext.SendTargetedActivityAsync(outbound, TargetUser);

            var sent = Assert.Single(captured);
            Assert.DoesNotContain(sent.Entities, entity => entity is QuotedReplyEntity);
            var promptPreview = Assert.Single(sent.Entities.OfType<TargetedMessageInfoEntity>());
            Assert.Equal("inbound-message", promptPreview.MessageId);
            Assert.Equal("response", sent.Text);
            Assert.Single(sent.Entities.OfType<ActivityTreatment>());
        }

        [Fact]
        public async Task SendActivityAsync_TargetedInbound_PreservesExistingPromptPreview()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreatePromptPreviewTurnContext(adapter);
            var outbound = new TeamsActivity
            {
                Type = ActivityTypes.Message,
                Text = "response",
                Entities =
                [
                    new TargetedMessageInfoEntity { MessageId = "explicit-message" }
                ]
            };

            await turnContext.SendActivityAsync(outbound);

            var sent = Assert.Single(captured);
            var promptPreview = Assert.Single(sent.Entities.OfType<TargetedMessageInfoEntity>());
            Assert.Equal("explicit-message", promptPreview.MessageId);
        }

        [Fact]
        public async Task SendActivityAsync_NonTargetedInbound_DoesNotAddPromptPreview()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);

            await turnContext.SendActivityAsync(new Activity
            {
                Type = ActivityTypes.Message,
                Text = "response"
            });

            var sent = Assert.Single(captured);
            Assert.DoesNotContain(sent.Entities ?? [], entity => entity is TargetedMessageInfoEntity);
        }

        [Fact]
        public async Task SendActivityAsync_String_TargetedInbound_AddsPromptPreview()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreatePromptPreviewTurnContext(adapter);

            await turnContext.SendActivityAsync("response");

            var sent = Assert.Single(captured);
            var promptPreview = Assert.Single(sent.Entities.OfType<TargetedMessageInfoEntity>());
            Assert.Equal("inbound-message", promptPreview.MessageId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SendActivityAsync_String_RejectsNullOrWhitespace(string text)
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => turnContext.SendActivityAsync(text));
        }

        [Fact]
        public async Task SendActivityAsync_TargetedInboundWithoutQuotedPlaceholder_PreservesWhitespace()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreatePromptPreviewTurnContext(adapter);

            await turnContext.SendActivityAsync("    indented code");

            Assert.Equal("    indented code", Assert.Single(captured).Text);
        }

        [Fact]
        public async Task SendActivitiesAsync_TargetedInbound_AddsPromptPreviewToEachMessage()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreatePromptPreviewTurnContext(adapter);

            await turnContext.SendActivitiesAsync(
            [
                new Activity { Type = ActivityTypes.Message, Text = "first" },
                new Activity { Type = ActivityTypes.Message, Text = "second" }
            ]);

            Assert.Equal(2, captured.Length);
            Assert.All(captured, sent =>
            {
                var promptPreview = Assert.Single(sent.Entities.OfType<TargetedMessageInfoEntity>());
                Assert.Equal("inbound-message", promptPreview.MessageId);
            });
        }

        [Fact]
        public async Task SendActivitiesAsync_NullActivities_ThrowsArgumentNullException()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => turnContext.SendActivitiesAsync(null));
        }

        [Fact]
        public async Task SendTargetedActivityAsync_RecipientArgumentOverridesActivityRecipient()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new ChannelAccount { Id = "original-user" }
            };

            await turnContext.SendTargetedActivityAsync(activity, TargetUser);

            Assert.Same(TargetUser, Assert.Single(captured).Recipient);
            Assert.Equal("original-user", activity.Recipient.Id);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_NullActivity_ThrowsArgumentNullException()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                turnContext.SendTargetedActivityAsync((IActivity)null, TargetUser));
        }

        [Fact]
        public async Task SendTargetedActivityAsync_TextAndRecipient_CreatesTargetedMessage()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);

            await turnContext.SendTargetedActivityAsync("hello", TargetUser);

            var sent = Assert.Single(captured);
            Assert.Equal(ActivityTypes.Message, sent.Type);
            Assert.Equal("hello", sent.Text);
            Assert.Same(TargetUser, sent.Recipient);
            Assert.True(sent.IsTargetedActivity());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public async Task SendTargetedActivityAsync_Text_RejectsNullOrWhitespace(string text)
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                turnContext.SendTargetedActivityAsync(text, TargetUser));
        }

        [Fact]
        public async Task SendTargetedActivityAsync_ActivityAndRecipientId_CreatesUserRecipient()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Event };

            await turnContext.SendTargetedActivityAsync(activity, "user-id");

            var sent = Assert.Single(captured);
            Assert.Equal(ActivityTypes.Event, sent.Type);
            Assert.Equal("user-id", sent.Recipient.Id);
            Assert.Equal(RoleTypes.User, sent.Recipient.Role);
            Assert.True(sent.IsTargetedActivity());
        }

        [Fact]
        public async Task SendTargetedActivityAsync_TextAndRecipientId_CreatesTargetedMessageForUser()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);

            await turnContext.SendTargetedActivityAsync("hello", "user-id");

            var sent = Assert.Single(captured);
            Assert.Equal(ActivityTypes.Message, sent.Type);
            Assert.Equal("hello", sent.Text);
            Assert.Equal("user-id", sent.Recipient.Id);
            Assert.Equal(RoleTypes.User, sent.Recipient.Role);
            Assert.True(sent.IsTargetedActivity());
        }

        // ── Activity shadow ───────────────────────────────────────────────────

        [Fact]
        public void Activity_ReturnsTeamsActivity_WithTypedChannelData()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var innerContext = new TurnContext(adapter, new TeamsActivity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelData = new Microsoft.Teams.Apps.Schema.TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType("channelCreated") }
            });
            var turnContext = new TeamsTurnContext(innerContext);

            ITeamsActivity activity = turnContext.Activity;

            Assert.NotNull(activity);
            Assert.Equal("channelCreated", activity.ChannelData.EventType);
        }

        [Fact]
        public void Activity_ConvertsPlainActivity_ToTeamsActivity()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var innerContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
                ChannelData = new Microsoft.Teams.Apps.Schema.TeamsChannelData { EventType = new Microsoft.Teams.Apps.ConversationEventType("teamRenamed") }
            });
            var turnContext = new TeamsTurnContext(innerContext);

            ITeamsActivity activity = turnContext.Activity;

            Assert.NotNull(activity);
            Assert.Equal("teamRenamed", activity.ChannelData.EventType);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>The user being targeted in outgoing activities.</summary>
        private static readonly ChannelAccount TargetUser = new() { Id = "fromId", Name = "Target User", Role = RoleTypes.User };

        private static ITeamsTurnContext CreateTurnContext(ChannelAdapter adapter)
        {
            var innerContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            return new TeamsTurnContext(innerContext);
        }

        private static TeamsTurnContext CreatePromptPreviewTurnContext(ChannelAdapter adapter)
        {
            var inbound = new Activity
            {
                Type = ActivityTypes.Message,
                Id = "inbound-message",
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new ChannelAccount
                {
                    Id = "recipientId",
                    Properties =
                    {
                        ["isTargeted"] = JsonSerializer.SerializeToElement(true)
                    }
                },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" }
            };
            return new TeamsTurnContext(new TurnContext(adapter, inbound));
        }
    }
}
