// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Extensions.A2A.Client;

/// <summary>
/// Provides direct access to the A2A primitives for the current turn: the outgoing
/// <see cref="AgentEventQueue"/>, the incoming <c>RequestContext</c>, and the
/// <see cref="ITaskStore"/>.
/// </summary>
/// <remarks>
/// This client is scoped to the current turn. Its event queue and request context must not be
/// retained for use after the turn completes.
/// </remarks>
public class A2AClient
{
    internal A2AClient(ITurnContext turnContext)
    {
        AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

        EventQueue = turnContext.Services.Get<AgentEventQueue>();
        RequestContext = turnContext.Services.Get<RequestContext>();
        TaskStore = turnContext.Services.Get<ITaskStore>();
    }

    /// <summary>
    /// Gets the A2A event queue used to enqueue outgoing messages, artifacts, and status updates for the current turn.
    /// </summary>
    public AgentEventQueue EventQueue { get; }

    /// <summary>
    /// Gets the A2A request context for the current turn (task id, context id, and incoming message).
    /// </summary>
    public RequestContext RequestContext { get; }

    /// <summary>
    /// Gets the A2A task store used to persist and retrieve task state.
    /// </summary>
    public ITaskStore TaskStore { get; }
}
