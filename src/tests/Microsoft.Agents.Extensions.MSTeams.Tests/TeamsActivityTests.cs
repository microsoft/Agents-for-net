// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests
{
    /// <summary>
    /// Tests for <see cref="TeamsActivity"/> and <see cref="ITeamsActivity"/>.
    ///
    /// Resolution is driven by the <c>[ActivityType(ChannelId = "msteams")]</c> annotation on
    /// <see cref="TeamsActivity"/>, which the source generator auto-registers so that any inbound
    /// Activity on the msteams channel deserializes to <see cref="TeamsActivity"/>.
    /// </summary>
    public class TeamsActivityTests
    {
        [Fact]
        public void Deserialize_MsteamsChannel_ResolvesToTeamsActivity()
        {
            const string json = """
                {
                  "type": "message",
                  "channelId": "msteams",
                  "text": "hello",
                  "channelData": { "eventType": "channelCreated" }
                }
                """;

            var activity = ProtocolJsonSerializer.ToObject<IActivity>(json);

            var teamsActivity = Assert.IsType<TeamsActivity>(activity);
            Assert.Equal("channelCreated", teamsActivity.ChannelData.EventType);
        }

        [Fact]
        public void TypedChannelData_ReadsThroughBaseChannelData()
        {
            var activity = new TeamsActivity
            {
                Type = ActivityTypes.Message,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams,
                ChannelData = new Microsoft.Teams.Api.ChannelData { EventType = "teamRenamed" }
            };

            // The typed shadow and the base ChannelData stay in sync.
            Assert.Equal("teamRenamed", activity.ChannelData.EventType);
            Assert.Equal("teamRenamed", activity.GetChannelData<Microsoft.Teams.Api.ChannelData>().EventType);
        }

        [Fact]
        public void TeamsActivity_IsAnITeamsActivity()
        {
            ITeamsActivity activity = new TeamsActivity
            {
                ChannelData = new Microsoft.Teams.Api.ChannelData { EventType = "channelDeleted" }
            };

            Assert.Equal("channelDeleted", activity.ChannelData.EventType);
        }
    }
}
