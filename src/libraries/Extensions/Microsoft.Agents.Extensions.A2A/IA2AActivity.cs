// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// An A2A-specific <see cref="IActivity"/> that exposes the A2A channel data as a strongly-typed
/// <see cref="A2A.Message"/> instead of the loosely-typed <see cref="IActivity.ChannelData"/>.
/// </summary>
public interface IA2AActivity : IActivity
{
    /// <summary>
    /// The A2A message payload carried on the Activity.
    /// </summary>
    new global::A2A.Message ChannelData { get; set; }
}
