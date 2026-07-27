// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Extensions.A2A;

public class A2AAgentExtension : Builder.AgentExtension
{
    private readonly AgentApplication _agentApplication;

    public A2AAgentExtension(AgentApplication agentApplication)
    {
        _agentApplication = agentApplication;
        ChannelId = Channels.A2A;
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
    public A2AAgentExtension OnMessage(A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.Message)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
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
    public A2AAgentExtension OnMessage(string text, A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(text)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
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
    public A2AAgentExtension OnMessage(Regex textPattern, A2ARouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        _agentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(textPattern)
            .WithChannelId(ChannelId)
            .WithHandler(HandlerUtils.WrapHandler(routeHandler))
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }
}