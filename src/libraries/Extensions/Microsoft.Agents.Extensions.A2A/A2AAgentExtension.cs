// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A;

public class A2AAgentExtension : Builder.AgentExtension
{
#if !NETSTANDARD
    protected AgentApplication AgentApplication { get; init; }

    public A2AAgentExtension(AgentApplication agentApplication)
    {
        AgentApplication = agentApplication;
        ChannelId = Channels.A2A;
    }

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

    /// <summary>
    /// Registers a message route handler for any A2A message received by the agent.
    /// </summary>
    /// <param name="routeHandler">The delegate that processes incoming A2A message activities. This handler will be invoked when a message
    /// activity is received on the A2A channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.Message)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming A2A message matches the given
    /// text.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the A2A channel.</remarks>
    /// <param name="text">The text pattern to match incoming A2A messages. The route is triggered when a message matches this text.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. Responsible for processing the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in. If null, no auto sign-in handlers are
    /// applied.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated. Use RouteRank.Unspecified for default
    /// ordering.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(string text, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(text)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming A2A message matches the given
    /// text pattern.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the A2A channel.</remarks>
    /// <param name="textPattern">A regular expression used to match the text of incoming A2A messages. The route is triggered when the message
    /// text matches this pattern.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. This delegate processes the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in if authentication is required. May be null
    /// if no auto sign-in is needed.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated relative to other routes. Lower values
    /// indicate higher priority. The default is RouteRank.Unspecified.</param>
    /// <returns>The current instance of A2AAgentExtension to allow method chaining.</returns>
    public A2AAgentExtension OnMessage(Regex textPattern, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(textPattern)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }
}