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
        public void MakeTargetedActivity_NullActivity_ThrowsArgumentNullException()
        {
            IActivity activity = null;
            Assert.Throws<ArgumentNullException>(() => activity.MakeTargetedActivity());
        }
    }
}
