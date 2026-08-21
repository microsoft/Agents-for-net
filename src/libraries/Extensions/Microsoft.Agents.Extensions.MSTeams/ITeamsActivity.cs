// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.MSTeams
{
    /// <summary>
    /// A Teams-specific <see cref="IActivity"/> that exposes the Teams channel data as a strongly-typed
    /// <see cref="Microsoft.Teams.Api.ChannelData"/> instead of the loosely-typed <see cref="IActivity.ChannelData"/>.
    /// </summary>
    public interface ITeamsActivity : IActivity
    {
        /// <summary>
        /// The Teams channel data carried on the Activity.
        /// </summary>
        new Microsoft.Teams.Api.ChannelData ChannelData { get; set; }
    }
}
