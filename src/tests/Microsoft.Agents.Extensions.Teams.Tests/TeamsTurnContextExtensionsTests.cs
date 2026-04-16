// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Teams.Tests
{
    public class TeamsTurnContextExtensionsTests
    {
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

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>The user being targeted in outgoing activities.</summary>
        private static readonly ChannelAccount TargetUser = new() { Id = "fromId", Name = "Target User", Role = RoleTypes.User };

        private static ITurnContext CreateTurnContext(ChannelAdapter adapter)
        {
            return new TurnContext(adapter, new Activity
            {
                Type = ActivityTypes.Message,
                ChannelId = Channels.Msteams,
                Recipient = new() { Id = "recipientId" },
                Conversation = new() { Id = "conversationId" },
                From = new() { Id = "fromId" },
            });
        }
    }
}
