// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack
{
    /// <summary>
    /// A Slack-specific <see cref="Activity"/> that surfaces the Slack channel payload as a
    /// strongly-typed <see cref="SlackChannelData"/>.
    /// </summary>
    /// <remarks>
    /// The <c>[ActivityType(ChannelId = "slack")]</c> annotation auto-registers this type (via the
    /// generated <see cref="ActivityTypeInitAssemblyAttribute"/>), so any inbound Activity whose
    /// <see cref="Activity.ChannelId"/> is <c>"slack"</c> deserializes to <see cref="SlackActivity"/>.
    /// The typed <see cref="ChannelData"/> shadow reads through the base <see cref="Activity.ChannelData"/>
    /// (which the deserializer populates as raw JSON), so both the base and typed views stay in sync.
    /// </remarks>
    [ActivityType(ChannelId = Channels.Slack)]
    public class SlackActivity : Activity, ISlackActivity
    {
        /// <summary>
        /// The Slack channel data (envelope / interactive payload) carried on the Activity.
        /// </summary>
        public new SlackChannelData ChannelData
        {
            get => this.GetChannelData<SlackChannelData>();
            set => base.ChannelData = value;
        }
    }
}
