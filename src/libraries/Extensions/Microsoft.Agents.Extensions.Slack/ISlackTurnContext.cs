// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// Provides Slack-specific helpers for working with the current <see cref="Microsoft.Agents.Builder.ITurnContext"/>.
/// </summary>
public interface ISlackTurnContext : ITurnContext
{
    /// <summary>
    /// Gets the current <see cref="Microsoft.Agents.Extensions.Slack.ISlackActivity"/>, exposing the Activity as a strongly-typed
    /// <see cref="Microsoft.Agents.Extensions.Slack.ISlackActivity"/> instead of <see cref="Microsoft.Agents.Core.Models.IActivity"/>.
    /// </summary>
    new ISlackActivity Activity { get; }

    /// <summary>
    /// Gets the <see cref="Microsoft.Agents.Extensions.Slack.Api.SlackApi"/> client registered for Slack API access in the current turn context.
    /// </summary>
    SlackApi Client { get; }
}
