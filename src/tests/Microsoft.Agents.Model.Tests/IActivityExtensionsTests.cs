// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class IActivityExtensionsTests
    {
        // IsTargetedActivity

        [Fact]
        public void IsTargetedActivity_NullEntities_ReturnsFalse()
        {
            var activity = new Activity { Type = ActivityTypes.Message };
            Assert.False(activity.IsTargetedActivity());
        }

        [Fact]
        public void IsTargetedActivity_NoTargetedEntity_ReturnsFalse()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Entities = [new StreamInfo()]
            };
            Assert.False(activity.IsTargetedActivity());
        }

        [Fact]
        public void IsTargetedActivity_WithTargetedEntity_ReturnsTrue()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new ChannelAccount { Id = "user-id" },
                Entities = [new ActivityTreatment { Treatment = ActivityTreatmentTypes.Targeted }]
            };
            Assert.True(activity.IsTargetedActivity());
        }

        [Fact]
        public void WithRecipient_Targeted_SetsRecipientAndAddsTargetedTreatment()
        {
            var recipient = new ChannelAccount { Id = "user-id", Name = "User" };
            IActivity activity = new Activity { Type = ActivityTypes.Message };

            var result = activity.WithRecipient(recipient, isTargeted: true);

            Assert.Same(activity, result);
            Assert.Same(recipient, result.Recipient);
            var treatment = Assert.Single(result.Entities.OfType<ActivityTreatment>());
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
        }

        [Fact]
        public void WithRecipient_IdTargeted_CreatesUserRecipientAndAddsTargetedTreatment()
        {
            IActivity activity = new Activity { Type = ActivityTypes.Message };

            var result = activity.WithRecipient("user-id", isTargeted: true);

            Assert.Same(activity, result);
            Assert.Equal("user-id", result.Recipient.Id);
            Assert.Equal(RoleTypes.User, result.Recipient.Role);
            var treatment = Assert.Single(result.Entities.OfType<ActivityTreatment>());
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
        }

        [Fact]
        public void WithRecipient_IdDefaultsToNotTargeted()
        {
            IActivity activity = new Activity { Type = ActivityTypes.Message };

            activity.WithRecipient("user-id");

            Assert.Equal("user-id", activity.Recipient.Id);
            Assert.Equal(RoleTypes.User, activity.Recipient.Role);
            Assert.False(activity.IsTargetedActivity());
        }

        [Fact]
        public void WithRecipient_NotTargeted_RemovesTargetedTreatment()
        {
            var recipient = new ChannelAccount { Id = "user-id" };
            IActivity activity = new Activity
            {
                Type = ActivityTypes.Message,
                Entities =
                [
                    new ActivityTreatment { Treatment = ActivityTreatmentTypes.Targeted },
                    new ActivityTreatment { Treatment = "transient" },
                    new Entity("custom")
                ]
            };

            activity.WithRecipient(recipient, isTargeted: false);

            Assert.Same(recipient, activity.Recipient);
            Assert.DoesNotContain(
                activity.Entities.OfType<ActivityTreatment>(),
                treatment => treatment.Treatment == ActivityTreatmentTypes.Targeted);
            Assert.Contains(
                activity.Entities.OfType<ActivityTreatment>(),
                treatment => treatment.Treatment == "transient");
            Assert.Contains(activity.Entities, entity => entity.Type == "custom");
        }

        [Fact]
        public void WithRecipient_Targeted_CollapsesDuplicateTargetedTreatments()
        {
            IActivity activity = new Activity
            {
                Type = ActivityTypes.Message,
                Entities =
                [
                    new ActivityTreatment { Treatment = ActivityTreatmentTypes.Targeted },
                    new ActivityTreatment { Treatment = ActivityTreatmentTypes.Targeted }
                ]
            };

            activity.WithRecipient(new ChannelAccount { Id = "user-id" }, isTargeted: true);

            Assert.Single(
                activity.Entities.OfType<ActivityTreatment>(),
                treatment => treatment.Treatment == ActivityTreatmentTypes.Targeted);
        }

        // MakeTargetedActivity — Recipient and Entity handling

        [Fact]
        public void MakeTargetedActivity_WithRecipientAlreadySet_AddsEntityAndPreservesRecipient()
        {
            var member = new ChannelAccount { Id = "member-id", Name = "Member Name" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = member
            };

            var result = activity.MakeTargetedActivity();

            Assert.Same(activity, result);
            Assert.Equal("member-id", result.Recipient.Id);
            var treatment = result.Entities.OfType<ActivityTreatment>().Single();
            Assert.Equal(ActivityTreatmentTypes.Targeted, treatment.Treatment);
            Assert.True(result.IsTargetedActivity());
        }

        [Fact]
        public void MakeTargetedActivity_WithUserArgument_SetsUserAsRecipient()
        {
            var user = new ChannelAccount { Id = "specific-user", Name = "Specific User" };
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };

            var result = activity.MakeTargetedActivity(user);

            Assert.Equal("specific-user", result.Recipient.Id);
            Assert.True(result.IsTargetedActivity());
        }

        [Fact]
        public void MakeTargetedActivity_WithUserArgument_OverridesExistingRecipient()
        {
            var originalRecipient = new ChannelAccount { Id = "original-id" };
            var newUser = new ChannelAccount { Id = "new-user-id" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                Recipient = originalRecipient
            };

            var result = activity.MakeTargetedActivity(newUser);

            Assert.Equal("new-user-id", result.Recipient.Id);
        }

        [Fact]
        public void MakeTargetedActivity_AlreadyTargeted_WithUserArgument_ReplacesRecipient()
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = new ChannelAccount { Id = "original-id" },
                Entities = [new ActivityTreatment { Treatment = ActivityTreatmentTypes.Targeted }]
            };

            var result = activity.MakeTargetedActivity(new ChannelAccount { Id = "new-user-id" });

            Assert.Equal("new-user-id", result.Recipient.Id);
            Assert.Single(result.Entities.OfType<ActivityTreatment>());
        }

        [Fact]
        public void MakeTargetedActivity_NullRecipientAndNullUser_ThrowsInvalidOperationException()
        {
            var activity = new Activity { Type = ActivityTypes.Message, Text = "hello" };
            Assert.Throws<InvalidOperationException>(() => activity.MakeTargetedActivity());
        }

        [Fact]
        public void MakeTargetedActivity_AlreadyTargeted_IsIdempotent()
        {
            var member = new ChannelAccount { Id = "member-id" };
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Recipient = member
            };
            activity.MakeTargetedActivity();

            var result = activity.MakeTargetedActivity(); // second call

            Assert.Same(activity, result);
            Assert.Single(result.Entities.OfType<ActivityTreatment>()); // no duplicate entity added
        }

        [Fact]
        public void MakeTargetedActivity_NullActivity_Throws()
        {
            IActivity activity = null;
            Assert.Throws<NullReferenceException>(() => activity.MakeTargetedActivity());
        }
    }
}
