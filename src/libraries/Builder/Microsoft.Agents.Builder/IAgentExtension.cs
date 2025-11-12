// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Contract for Agent Extensions.
    /// </summary>
    public interface IAgentExtension
    {
#if !NETSTANDARD
        ChannelId ChannelId { get; init;}
#else
        ChannelId ChannelId { get; set; }
#endif
        void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, string[] autoSignInHandlers = null);
    }
}