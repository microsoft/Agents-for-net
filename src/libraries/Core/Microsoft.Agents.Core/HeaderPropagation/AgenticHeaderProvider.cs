// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Core.HeaderPropagation;

/// <summary>
/// Provides agent identity headers derived from the incoming Activity for agentic requests.
/// Headers are only emitted when the Activity represents an agentic request
/// (i.e., Recipient.Role is AgenticUser or AgenticIdentity).
/// </summary>
public class AgenticHeaderProvider : IHeaderValueProvider
{
    private readonly IActivity _activity;
    private readonly string _agentName;

    /// <summary>
    /// Initializes a new instance of <see cref="AgenticHeaderProvider"/>.
    /// </summary>
    /// <param name="activity">The incoming activity to derive header values from.</param>
    /// <param name="agentName">The human-friendly agent name (typically from the [Agent] attribute).</param>
    public AgenticHeaderProvider(IActivity activity, string agentName)
    {
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _agentName = agentName ?? string.Empty;
    }

    /// <inheritdoc/>
    public IEnumerable<HeaderPropagationEntry> GetHeaders()
    {
        if (!IsAgenticRequest())
        {
            yield break;
        }

        yield return new HeaderPropagationEntry
        {
            Key = "AgentRegistrar",
            Value = "A365",
            Action = HeaderPropagationEntryAction.Add
        };

        yield return new HeaderPropagationEntry
        {
            Key = "AgentID",
            Value = _activity.Recipient?.AgenticAppId ?? string.Empty,
            Action = HeaderPropagationEntryAction.Add
        };

        yield return new HeaderPropagationEntry
        {
            Key = "AgentName",
            Value = _agentName,
            Action = HeaderPropagationEntryAction.Add
        };

        yield return new HeaderPropagationEntry
        {
            Key = "Agent-Referrer",
            Value = _activity.ChannelId?.ToString() ?? string.Empty,
            Action = HeaderPropagationEntryAction.Add
        };
    }

    private bool IsAgenticRequest()
    {
        var role = _activity.Recipient?.Role;
        return string.Equals(role, RoleTypes.AgenticUser, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, RoleTypes.AgenticIdentity, StringComparison.OrdinalIgnoreCase);
    }
}
