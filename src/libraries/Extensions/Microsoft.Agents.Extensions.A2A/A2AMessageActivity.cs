// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// An A2A-specific <see cref="Activity"/> that surfaces the A2A channel payload as a
/// strongly-typed <see cref="A2A.Message"/>. This is the concrete <see cref="IA2AActivity"/>
/// produced when an incoming A2A message is converted to an Activity.
/// </summary>
/// <remarks>
/// The <c>[ActivityType(ChannelId = "a2a")]</c> annotation auto-registers this type (via the
/// generated <see cref="ActivityTypeInitAssemblyAttribute"/>), so any inbound Activity whose
/// <see cref="Activity.ChannelId"/> is <c>"a2a"</c> deserializes to <see cref="A2AMessageActivity"/>.
/// The typed <see cref="ChannelData"/> shadow reads through the base <see cref="Activity.ChannelData"/>,
/// so both the base and typed views stay in sync.
/// </remarks>
[ActivityType(ChannelId = Channels.A2A)]
public class A2AMessageActivity : Activity, IA2AActivity
{
    /// <summary>
    /// The A2A message payload carried on the Activity.
    /// </summary>
    public new global::A2A.Message ChannelData
    {
        get => this.GetChannelData<global::A2A.Message>();
        set => base.ChannelData = value;
    }
}
