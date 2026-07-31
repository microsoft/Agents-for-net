// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.MSTeams
{
    /// <summary>
    /// A Teams-specific <see cref="Activity"/> that surfaces the Teams channel payload as a
    /// strongly-typed <see cref="Microsoft.Teams.Api.ChannelData"/>.
    /// </summary>
    /// <remarks>
    /// The <c>[ActivityType(ChannelId = "msteams")]</c> annotation auto-registers this type (via the
    /// generated <see cref="ActivityTypeInitAssemblyAttribute"/>), so any inbound Activity whose
    /// <see cref="Activity.ChannelId"/> is <c>"msteams"</c> deserializes to <see cref="TeamsActivity"/>.
    /// The typed <see cref="ChannelData"/> shadow reads through the base <see cref="Activity.ChannelData"/>
    /// (which the deserializer populates as raw JSON), so both the base and typed views stay in sync.
    /// </remarks>
    [ActivityType(ChannelId = Microsoft.Agents.Core.Models.Channels.Msteams)]
    public class TeamsActivity : Activity, ITeamsActivity
    {
        /// <summary>
        /// The Teams channel data carried on the Activity.
        /// </summary>
        public new Microsoft.Teams.Api.ChannelData ChannelData
        {
            get => this.GetChannelData<Microsoft.Teams.Api.ChannelData>();
            set => base.ChannelData = value;
        }
    }
}
