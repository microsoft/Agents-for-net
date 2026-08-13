// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Tests;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Api.Clients;
using Microsoft.Teams.Common.Http;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests
{
    public class TeamsTurnContextTests
    {
        [Theory]
        [InlineData(null, "currentActivityId")]
        [InlineData("specifiedActivityId", "specifiedActivityId")]
        public async Task AddReactionAsync_UsesResolvedActivityId(string activityId, string expectedActivityId)
        {
            var (turnContext, httpClient) = CreateTurnContextWithApiClient();

            await turnContext.AddReactionAsync("like", activityId);

            VerifyReactionRequest(httpClient, HttpMethod.Put, expectedActivityId, "like");
        }

        [Theory]
        [InlineData(null, "currentActivityId")]
        [InlineData("specifiedActivityId", "specifiedActivityId")]
        public async Task DeleteReactionAsync_UsesResolvedActivityId(string activityId, string expectedActivityId)
        {
            var (turnContext, httpClient) = CreateTurnContextWithApiClient();

            await turnContext.DeleteReactionAsync("heart", activityId);

            VerifyReactionRequest(httpClient, HttpMethod.Delete, expectedActivityId, "heart");
        }

        [Fact]
        public async Task ReplyAsync_String_QuotesCurrentActivity()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter, "incomingActivityId");

            await turnContext.ReplyAsync("reply text");

            var sent = Assert.IsType<Activity>(Assert.Single(captured));
            Assert.Equal("<quoted messageId=\"incomingActivityId\"/> reply text", sent.Text);
            var quote = Assert.Single(sent.Entities.OfType<QuotedReplyEntity>());
            Assert.Equal("incomingActivityId", quote.QuotedReply.MessageId);
        }

        [Fact]
        public async Task ReplyAsync_Activity_PreservesExistingContent()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter, "incomingActivityId");
            var existingEntity = new Entity { Type = "custom" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "reply text",
                Entities = [existingEntity]
            };

            await turnContext.ReplyAsync(activity);

            var sent = Assert.IsType<Activity>(Assert.Single(captured));
            Assert.Same(activity, sent);
            Assert.Equal("<quoted messageId=\"incomingActivityId\"/> reply text", sent.Text);
            Assert.Same(existingEntity, sent.Entities[0]);
            Assert.IsType<QuotedReplyEntity>(sent.Entities[1]);
        }

        [Fact]
        public async Task ReplyAsync_MissingCurrentActivityId_SendsWithoutQuote()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);

            await turnContext.ReplyAsync("reply text");

            var sent = Assert.IsType<Activity>(Assert.Single(captured));
            Assert.Equal("reply text", sent.Text);
            Assert.DoesNotContain(sent.Entities, entity => entity is QuotedReplyEntity);
        }

        [Fact]
        public async Task SendAsync_ConversationId_SendsProactively()
        {
            var (turnContext, adapter) = CreateProactiveTurnContext("currentConversationId");

            var response = await turnContext.SendAsync("conversationId", "proactive text");

            Assert.Equal("sentActivityId", response.Id);
            Assert.Equal("conversationId", adapter.ConversationId);
            Assert.Equal(ActivityTypes.Message, adapter.SentActivity.Type);
            Assert.Equal("proactive text", adapter.SentActivity.Text);
            Assert.Null(adapter.SentActivity.ReplyToId);
            Assert.Equal("agentId", adapter.Reference.Agent.Id);
            Assert.Equal(Microsoft.Agents.Core.Models.Channels.Msteams, adapter.Reference.ChannelId);
            Assert.Equal("https://serviceurl.com/", adapter.Reference.ServiceUrl);
            Assert.Equal("userId", adapter.Reference.User.Id);
            Assert.Null(adapter.Reference.ActivityId);
        }

        [Fact]
        public async Task SendAsync_ActivityId_SendsToThreadWithoutChangingCurrentConversation()
        {
            var (turnContext, adapter) = CreateProactiveTurnContext("currentConversationId");

            await turnContext.SendAsync("conversationId;messageid=111", "222", "threaded text");

            Assert.Equal("conversationId;messageid=222", adapter.ConversationId);
            Assert.Equal("threaded text", adapter.SentActivity.Text);
            Assert.Equal("currentConversationId", turnContext.Activity.Conversation.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("not-numeric")]
        public async Task SendAsync_InvalidActivityId_ThrowsArgumentException(string activityId)
        {
            var (turnContext, adapter) = CreateProactiveTurnContext("currentConversationId");

            await Assert.ThrowsAsync<ArgumentException>(
                () => turnContext.SendAsync("conversationId", activityId, "threaded text"));

            Assert.Null(adapter.SentActivity);
        }

        // ── SendTargetedActivityAsync ─────────────────────────────────────────

        [Fact]
        public async Task SendTargetedActivityAsync_SentActivityHasTargetedTreatment()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello", Recipient = TargetUser };

            await turnContext.SendTargetedActivityAsync(activity);

            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            var treatment = Assert.Single(sent.Entities.OfType<ActivityTreatment>());
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_OriginalActivityIsNotModified()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "original", Recipient = TargetUser };

            await turnContext.SendTargetedActivityAsync(activity);

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

            await turnContext.SendTargetedActivityAsync(activity);

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

            await turnContext.SendTargetedActivityAsync(activity);

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

            var response = await turnContext.SendTargetedActivityAsync(activity);

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

            await turnContext.SendTargetedActivityAsync(activity);

            // Sent activity is a different object instance from the original
            Assert.NotNull(captured);
            Assert.NotSame(activity, captured[0]);
        }

        // ── SendTargetedActivitiesAsync ───────────────────────────────────────

        [Fact]
        public async Task SendTargetedActivitiesAsync_AllSentActivitiesHaveTargetedTreatment()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "msg1", Recipient = TargetUser },
                new Activity { Type = ActivityTypes.Message, Text = "msg2", Recipient = TargetUser },
                new Activity { Type = ActivityTypes.Message, Text = "msg3", Recipient = TargetUser },
            };

            await turnContext.SendTargetedActivitiesAsync(activities);

            Assert.NotNull(captured);
            Assert.Equal(3, captured.Length);
            foreach (var sent in captured)
            {
                var treatment = Assert.Single(sent.Entities.OfType<ActivityTreatment>());
                Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
            }
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_OriginalActivitiesAreNotModified()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "a", Recipient = TargetUser },
                new Activity { Type = ActivityTypes.Message, Text = "b", Recipient = TargetUser },
            };

            await turnContext.SendTargetedActivitiesAsync(activities);

            // Originals should not contain any targeted treatment
            Assert.All(activities, a => Assert.DoesNotContain(a.Entities ?? [], e => e is ActivityTreatment));
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_SentActivitiesAreClones()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "a", Recipient = TargetUser },
                new Activity { Type = ActivityTypes.Message, Text = "b", Recipient = TargetUser },
            };

            await turnContext.SendTargetedActivitiesAsync(activities);

            // Sent activities are different object instances from the originals
            Assert.NotNull(captured);
            Assert.DoesNotContain(captured, sent => activities.Contains(sent));
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_ReturnsResourceResponseForEach()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Id = "id-1", Recipient = TargetUser },
                new Activity { Type = ActivityTypes.Message, Id = "id-2", Recipient = TargetUser },
            };

            var responses = await turnContext.SendTargetedActivitiesAsync(activities);

            // SimpleAdapter echoes each Id
            Assert.Equal(2, responses.Length);
            Assert.Contains(responses, r => r.Id == "id-1");
            Assert.Contains(responses, r => r.Id == "id-2");
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_PreservesExistingEntitiesOnClones()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Recipient = TargetUser,
                    Entities = [new Entity { Type = "existing" }]
                },
            };

            await turnContext.SendTargetedActivitiesAsync(activities);

            // Sent activity has the pre-existing entity plus the targeted treatment
            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            Assert.Equal(2, sent.Entities.Count);
            Assert.Contains(sent.Entities, e => e.Type == "existing");
            Assert.Contains(sent.Entities.OfType<ActivityTreatment>(),
                t => t.Treatment == ActivityTreatmentTypes.Targeted);
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_SingleActivity_HasTargetedTreatment()
        {
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "solo", Recipient = TargetUser }
            };

            await turnContext.SendTargetedActivitiesAsync(activities);

            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            Assert.Single(sent.Entities.OfType<ActivityTreatment>());
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_SupportsCancellationToken()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var cts = new CancellationTokenSource();
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "msg", Recipient = TargetUser }
            };

            // Should not throw
            await turnContext.SendTargetedActivitiesAsync(activities, cts.Token);
        }

        // ── Guard: missing Recipient ──────────────────────────────────────────

        [Fact]
        public async Task SendTargetedActivityAsync_NoRecipientOnActivity_ThrowsInvalidOperationException()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" }; // no Recipient

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                turnContext.SendTargetedActivityAsync(activity));
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_NoRecipientOnActivity_ThrowsInvalidOperationException()
        {
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "hello" } // no Recipient
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                turnContext.SendTargetedActivitiesAsync(activities));
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
                ChannelData = new Microsoft.Teams.Api.ChannelData { EventType = "channelCreated" }
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
                ChannelData = new Microsoft.Teams.Api.ChannelData { EventType = "teamRenamed" }
            });
            var turnContext = new TeamsTurnContext(innerContext);

            ITeamsActivity activity = turnContext.Activity;

            Assert.NotNull(activity);
            Assert.Equal("teamRenamed", activity.ChannelData.EventType);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>The user being targeted in outgoing activities.</summary>
        private static readonly ChannelAccount TargetUser = new() { Id = "fromId", Name = "Target User", Role = RoleTypes.User };

        private static ITeamsTurnContext CreateTurnContext(ChannelAdapter adapter, string activityId = null)
        {
            var innerContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                Id = activityId,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            return new TeamsTurnContext(innerContext);
        }

        private static (ITeamsTurnContext TurnContext, Mock<IHttpClient> HttpClient) CreateTurnContextWithApiClient()
        {
            var responseMessage = new HttpResponseMessage();
            var httpClient = new Mock<IHttpClient>();
            httpClient
                .Setup(client => client.SendAsync(It.IsAny<IHttpRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponse<string>
                {
                    Headers = responseMessage.Headers,
                    StatusCode = HttpStatusCode.OK,
                    Body = string.Empty
                });

            var innerContext = new TurnContext(new SimpleAdapter((Action<IActivity[]>)(_ => { })), new Activity
            {
                Type = ActivityTypes.Message,
                Id = "currentActivityId",
                ServiceUrl = "https://serviceurl.com/",
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
            innerContext.Services.Set(new ApiClient(innerContext.Activity.ServiceUrl, httpClient.Object));

            return (new TeamsTurnContext(innerContext), httpClient);
        }

        private static void VerifyReactionRequest(
            Mock<IHttpClient> httpClient,
            HttpMethod method,
            string activityId,
            string reactionType)
        {
            string expectedUrl =
                $"https://serviceurl.com/v3/conversations/conversationId/activities/{activityId}/reactions/{reactionType}";
            httpClient.Verify(client => client.SendAsync(
                It.Is<IHttpRequest>(request => request.Url == expectedUrl && request.Method == method),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        private static (ITeamsTurnContext TurnContext, ProactiveCaptureAdapter Adapter)
            CreateProactiveTurnContext(string conversationId)
        {
            var adapter = new ProactiveCaptureAdapter();
            var identity = new ClaimsIdentity([new Claim("aud", "agentId")]);
            var innerContext = new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                Id = "currentActivityId",
                ServiceUrl = "https://serviceurl.com/",
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                Recipient = new() { Id = "agentId" },
                Conversation = new() { Id = conversationId },
                From = new() { Id = "userId" },
            }, identity);

            return (new TeamsTurnContext(innerContext), adapter);
        }

        private sealed class ProactiveCaptureAdapter : ChannelAdapter
        {
            public string ConversationId { get; private set; }

            public ConversationReference Reference { get; private set; }

            public IActivity SentActivity { get; private set; }

            public override async Task ContinueConversationAsync(
                ClaimsIdentity claimsIdentity,
                ConversationReference reference,
                AgentCallbackHandler callback,
                CancellationToken cancellationToken = default)
            {
                Reference = reference;
                ConversationId = reference.Conversation.Id;
                using var context = new TurnContext(this, reference.GetContinuationActivity(), claimsIdentity);
                await callback(context, cancellationToken);
            }

            public override Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext,
                IActivity[] activities,
                CancellationToken cancellationToken)
            {
                SentActivity = Assert.Single(activities);
                return Task.FromResult(new[] { new ResourceResponse("sentActivityId") });
            }
        }
    }
}
