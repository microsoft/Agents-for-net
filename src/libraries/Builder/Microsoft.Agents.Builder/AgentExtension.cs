// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Represents an opinionated base class for Agent Extensions.
    /// </summary>
    public abstract class AgentExtension : IAgentExtension
    {
        public virtual string ChannelId {get;init;}
        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified) {
            var ensureChannelMatches = new RouteSelector(async (turnContext, cancellationToken) => {
                return turnContext.Activity.ChannelId == ChannelId && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureChannelMatches, routeHandler, isInvokeRoute, rank);
        }
    }
}
