// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ProductInfoTests()
    {
        [Fact]
        public void ChannelIdTests()
        {
            //Channels values
            //public const string Msteams = "msteams";
            //public const string M365CopilotSubChannel = "COPILOT";
            //public const string M365Copilot = $"{Msteams}:{M365CopilotSubChannel}";

            // Is Teams?
            string msTeamsJson = "{\"channelId\":\"msteams\"}";
            var activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Case insensitive
            msTeamsJson = "{\"channelId\":\"mStEaMs\"}";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(msTeamsJson);
            Assert.True(Channels.Msteams == activity.ChannelId);

            // Is M365Copilot?
            string m365CopilotJson = "{\"channelId\":\"msteams\",\"membersAdded\":[],\"membersRemoved\":[],\"reactionsAdded\":[],\"reactionsRemoved\":[],\"attachments\":[],\"entities\":[{\"id\":\"COPILOT\",\"type\":\"ProductInfo\"}],\"listenFor\":[],\"textHighlights\":[]}";
            activity = ProtocolJsonSerializer.ToObject<IActivity>(m365CopilotJson);
            Assert.True(Channels.M365Copilot == activity.ChannelId);

            // Base channel vs subchannel eval
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);

            // Serialize back out correctly
            var json = ProtocolJsonSerializer.ToJson(activity);
            Assert.Equal(m365CopilotJson, json);

            // ChannelId construction
            var channelId = new ChannelId(Channels.Msteams);
            Assert.Equal(Channels.Msteams, channelId);

            // With formatted value
            channelId = new ChannelId(Channels.M365Copilot);
            Assert.Equal(Channels.M365Copilot, channelId);
            Assert.True(Channels.M365Copilot == activity.ChannelId);
            Assert.Equal(Channels.Msteams, activity.ChannelId.Channel);
            Assert.Equal(Channels.M365CopilotSubChannel, activity.ChannelId.SubChannel);

            // nulls
            activity = new Activity() { ChannelId = null };
            Assert.False(Channels.Msteams == activity.ChannelId);

            activity = new Activity() { ChannelId = Channels.Msteams };
            Assert.False(activity.ChannelId == null);

            // Equality
            var channelId1 = new ChannelId(Channels.Msteams);
            var channelId2 = new ChannelId(Channels.Msteams);
            var channelId3 = new ChannelId(Channels.M365Copilot);
            Assert.Equal(channelId1, channelId2);
            Assert.NotEqual(channelId1, channelId3);
        }
    }
}
