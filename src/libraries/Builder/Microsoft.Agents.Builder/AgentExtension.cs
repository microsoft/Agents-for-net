// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an opinionated base class for Agent Extensions.
    /// </summary>
    public abstract class AgentExtension : IAgentExtension
    {
#if !NETSTANDARD
        public virtual ChannelId ChannelId { get; init; }
#else
        public virtual ChannelId ChannelId { get; set; } = string.Empty;
#endif

        /// <summary>
        /// Adapts a turn context to the specialized context exposed by this extension.
        /// </summary>
        /// <param name="turnContext">The current turn context, including its activity and turn-scoped services.</param>
        /// <returns>The extension's context wrapper, or the original context when no adaptation is needed.</returns>
        /// <remarks>
        /// The default implementation returns <paramref name="turnContext"/> unchanged.
        /// Continuation handlers with specialized context parameters use this hook for extensions registered
        /// on <see cref="Microsoft.Agents.Builder.App.AgentApplication"/> that match the current channel.
        /// Wrappers must preserve the underlying turn's services and activity.
        /// </remarks>
        public virtual ITurnContext WrapTurnContext(ITurnContext turnContext) => turnContext;

        [Obsolete("This method is deprecated. Please use the overload that includes the 'isAgenticOnly' parameter.")]
        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null)
        {
            AddRoute(agentApplication, routeSelector, routeHandler, isInvokeRoute, false, rank, autoSignInHandlers);
        }

        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null)
        {
            var route = RouteBuilder.Create()
                .WithChannelId(ChannelId)
                .WithSelector(routeSelector)
                .WithHandler(routeHandler)
                .AsInvoke(isInvokeRoute)
                .AsAgentic(isAgenticOnly)
                .WithOrderRank(rank)
                .WithOAuthHandlers(autoSignInHandlers)
                .Build();

            agentApplication.AddRoute(route);
        }

        public void AddRoute(AgentApplication agentApplication, Route route)
        {
            agentApplication.AddRoute(route);
        }
    }
}
