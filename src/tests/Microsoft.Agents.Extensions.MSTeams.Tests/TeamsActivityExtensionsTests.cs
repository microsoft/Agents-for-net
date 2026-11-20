// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Apps.Schema;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests
{
    public class TeamsActivityExtensionsTests
    {
        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { settings = new { selectedChannel = new { id = "channel123" } } }) };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnNullOnNullSettings()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { }) };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Null(channelId);
        }

        [Theory]
        [Trait("Category", "TeamsApiBoundary")]
        [InlineData("""{"settings":{}}""")]
        [InlineData("""{"settings":{"selectedChannel":null}}""")]
        public void TeamsGetSelectedChannelId_ShouldReturnNullOnMissingSelectedChannel(string channelData)
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.Deserialize<JsonElement>(channelData) };

            Assert.Null(activity.TeamsGetSelectedChannelId());
        }

        [Fact]
        public void TeamsGetMeetingInfo_ShouldReturnMeetingId()
        {
            var activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { meeting = new { id = "meeting123" } }) };

            var meeting = activity.TeamsGetMeetingInfo();

            Assert.NotNull(meeting);
            Assert.Equal("meeting123", meeting.Id);
        }

        [Fact]
        public void TeamsGetMeetingInfo_ShouldReturnMeetingIdFromTypedChannelData()
        {
            var activity = new TeamsActivity
            {
                ChannelData = JsonSerializer.Deserialize<TeamsChannelData>("""{"meeting":{"id":"meeting123"}}""")
            };

            var meeting = activity.TeamsGetMeetingInfo();

            Assert.NotNull(meeting);
            Assert.Equal("meeting123", meeting.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("{}")]
        [InlineData("""{"meeting":null}""")]
        public void TeamsGetMeetingInfo_ShouldReturnNullWhenMeetingDataIsAbsent(string channelData)
        {
            var activity = new Activity
            {
                ChannelData = channelData == null ? null : JsonSerializer.Deserialize<JsonElement>(channelData)
            };
            var typedActivity = new TeamsActivity
            {
                ChannelData = channelData == null ? null : JsonSerializer.Deserialize<TeamsChannelData>(channelData)
            };

            Assert.Null(activity.TeamsGetMeetingInfo());
            Assert.Null(typedActivity.TeamsGetMeetingInfo());
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { channel = new { id = "channel123" } }) };

            var channelId = activity.TeamsGetChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnNullOnNullChannel()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { }) };

            var channelId = activity.TeamsGetChannelId();

            Assert.Null(channelId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { team = new { id = "team1234" } }) };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team1234", teamId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamIdFromTypedActivity()
        {
            IMessageActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { team = new { id = "team123" } }) };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team123", teamId);
        }

        [Fact]
        public void TeamsEnableFeedbackLoop_ShouldAddFeedbackLoopData()
        {
            var activity = new Activity();

            var result = activity.TeamsEnableFeedbackLoop("custom");

            Assert.True(result);
            var channelData = JsonSerializer.SerializeToElement(activity.ChannelData);
            Assert.Equal("custom", channelData.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public void TeamsEnableFeedbackLoop_ShouldReturnFalse_WhenChannelDataAlreadySet()
        {
            var existingChannelData = new TeamsChannelData { Team = new Team { Id = "team123" } };
            var activity = new Activity { ChannelData = existingChannelData };

            var result = activity.TeamsEnableFeedbackLoop();

            Assert.False(result);
            Assert.Same(existingChannelData, activity.ChannelData);
        }

    }
}
