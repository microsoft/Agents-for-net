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
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            // Act
            await turnContext.SendTargetedActivityAsync(activity);

            // Assert
            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            var treatment = Assert.Single(sent.Entities.OfType<ActivityTreatment>());
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_OriginalActivityIsNotModified()
        {
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "original" };

            // Act
            await turnContext.SendTargetedActivityAsync(activity);

            // Assert — the original's Entities should not contain any targeted treatment
            // (Clone reads the property which may lazy-init Entities to [], so we check for no treatment)
            Assert.DoesNotContain(activity.Entities ?? [], e => e is ActivityTreatment);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_OriginalActivityWithEntitiesIsNotModified()
        {
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var originalEntity = new Entity { Type = "custom" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Entities = [originalEntity]
            };

            // Act
            await turnContext.SendTargetedActivityAsync(activity);

            // Assert — original still has exactly one entity
            Assert.Single(activity.Entities);
            Assert.Same(originalEntity, activity.Entities[0]);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_PreservesExistingEntitiesOnClone()
        {
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Entities = [new Entity { Type = "custom" }]
            };

            // Act
            await turnContext.SendTargetedActivityAsync(activity);

            // Assert — sent activity has the original entity plus the targeted treatment
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
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Id = "msg-1" };

            // Act
            var response = await turnContext.SendTargetedActivityAsync(activity);

            // Assert — SimpleAdapter echoes the Id back
            Assert.NotNull(response);
            Assert.Equal("msg-1", response.Id);
        }

        [Fact]
        public async Task SendTargetedActivityAsync_SentActivityIsAClone()
        {
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            // Act
            await turnContext.SendTargetedActivityAsync(activity);

            // Assert — sent activity is a different object instance
            Assert.NotNull(captured);
            Assert.NotSame(activity, captured[0]);
        }

        // ── SendTargetedActivitiesAsync ───────────────────────────────────────

        [Fact]
        public async Task SendTargetedActivitiesAsync_AllSentActivitiesHaveTargetedTreatment()
        {
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "msg1" },
                new Activity { Type = ActivityTypes.Message, Text = "msg2" },
                new Activity { Type = ActivityTypes.Message, Text = "msg3" },
            };

            // Act
            await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert — every sent activity has the targeted treatment
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
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "a" },
                new Activity { Type = ActivityTypes.Message, Text = "b" },
            };

            // Act
            await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert — originals should not contain any targeted treatment
            // (Clone reads the property which may lazy-init Entities to [], so we check for no treatment)
            Assert.All(activities, a => Assert.DoesNotContain(a.Entities ?? [], e => e is ActivityTreatment));
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_SentActivitiesAreClones()
        {
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "a" },
                new Activity { Type = ActivityTypes.Message, Text = "b" },
            };

            // Act
            await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert — sent activities are different object instances
            Assert.NotNull(captured);
            Assert.DoesNotContain(captured, sent => activities.Contains(sent));
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_ReturnsResourceResponseForEach()
        {
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Id = "id-1" },
                new Activity { Type = ActivityTypes.Message, Id = "id-2" },
            };

            // Act
            var responses = await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert — SimpleAdapter echoes each Id
            Assert.Equal(2, responses.Length);
            Assert.Contains(responses, r => r.Id == "id-1");
            Assert.Contains(responses, r => r.Id == "id-2");
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_PreservesExistingEntitiesOnClones()
        {
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity
                {
                    Type = ActivityTypes.Message,
                    Entities = [new Entity { Type = "existing" }]
                },
            };

            // Act
            await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert — sent activity has the pre-existing entity plus the targeted treatment
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
            // Arrange
            IActivity[] captured = null;
            var adapter = new SimpleAdapter((Action<IActivity[]>)(activities => captured = activities));
            var turnContext = CreateTurnContext(adapter);
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "solo" }
            };

            // Act
            await turnContext.SendTargetedActivitiesAsync(activities);

            // Assert
            Assert.NotNull(captured);
            var sent = Assert.Single(captured);
            Assert.Single(sent.Entities.OfType<ActivityTreatment>());
        }

        [Fact]
        public async Task SendTargetedActivitiesAsync_SupportsCancellationToken()
        {
            // Arrange
            var adapter = new SimpleAdapter((Action<IActivity[]>)(_ => { }));
            var turnContext = CreateTurnContext(adapter);
            var cts = new CancellationTokenSource();
            var activities = new IActivity[]
            {
                new Activity { Type = ActivityTypes.Message, Text = "msg" }
            };

            // Act & Assert — should not throw
            await turnContext.SendTargetedActivitiesAsync(activities, cts.Token);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
