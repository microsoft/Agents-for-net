// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using System;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Extensions.A2A;

public class A2AAgentExtension(AgentApplication agentApplication) : Builder.AgentExtension
{
#if !NETSTANDARD
    protected AgentApplication AgentApplication { get; init; } = agentApplication;

#else
    protected AgentApplication AgentApplication { get; set; } = agentApplication;
}
#endif

    /// <summary>
    /// Gets or sets the agent application instance associated with the current context.
    /// </summary>
    /// <remarks>This property is typically used to access or assign the agent application that manages
    /// agent-related operations within the current execution context. The value may be set during initialization or by
    /// derived classes as needed.</remarks>
    /// <param name="turnContext"></param>
    /// <param name="action">The action to perform with the agent event queue and request context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
#pragma warning disable CA1822 // Mark members as static
    public Task A2ADirect(ITurnContext turnContext, Func<AgentEventQueue, RequestContext, Task> action)
#pragma warning restore CA1822 // Mark members as static
    {
        AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
        AssertionHelpers.ThrowIfNull(action, nameof(action));

        var eventQueue = turnContext.Services.Get<AgentEventQueue>();
        var requestContext = turnContext.Services.Get<RequestContext>();

        return action(eventQueue, requestContext);
    }
}