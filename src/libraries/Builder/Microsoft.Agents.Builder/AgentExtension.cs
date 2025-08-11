// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an opinionated base class for Agent Extensions.
    /// </summary>
    public abstract class AgentExtension : IAgentExtension
    {
#if !NETSTANDARD
        public virtual ChannelId ChannelId { get; init;}
#else
        public virtual ChannelId ChannelId { get; set; } = string.Empty;
#endif
        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null) {
            var ensureChannelMatches = new RouteSelector(async (turnContext, cancellationToken) => {
                return turnContext.Activity.ChannelId == ChannelId && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureChannelMatches, routeHandler, isInvokeRoute, rank, autoSignInHandlers);
        }
    }
}
