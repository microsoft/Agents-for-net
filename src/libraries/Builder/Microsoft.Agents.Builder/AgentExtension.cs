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
        public void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, SignInResolver autoSignInHandlers = null, bool isAgenticOnly = false) {
            var ensureChannelMatches = new RouteSelector(async (turnContext, cancellationToken) => {
                bool isForChannel = false; 
                if (turnContext.Activity.ChannelId != null && ChannelId != null)
                {
                    if (!string.IsNullOrEmpty(ChannelId.SubChannel) && ChannelId.SubChannel.Equals("*"))
                    {
                        isForChannel = turnContext.Activity.ChannelId.IsParentChannel(ChannelId.Channel);
                    }
                    else
                        isForChannel = turnContext.Activity.ChannelId.Equals(ChannelId);
                }
                return isForChannel && await routeSelector(turnContext, cancellationToken);
            });

            agentApplication.AddRoute(ensureChannelMatches, routeHandler, isInvokeRoute, rank, autoSignInHandlers, isAgenticOnly);
        }
    }
}
