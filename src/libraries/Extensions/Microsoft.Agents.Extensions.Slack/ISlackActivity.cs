// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack
{
    /// <summary>
    /// A Slack-specific <see cref="IActivity"/> that exposes the Slack channel data as a strongly-typed
    /// <see cref="SlackChannelData"/> instead of the loosely-typed <see cref="IActivity.ChannelData"/>.
    /// </summary>
    public interface ISlackActivity : IActivity
    {
        /// <summary>
        /// The Slack channel data (envelope / interactive payload) carried on the Activity.
        /// </summary>
        new SlackChannelData ChannelData { get; set; }
    }
}
