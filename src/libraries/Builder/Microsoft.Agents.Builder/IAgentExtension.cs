// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Contract for Agent Extensions.
    /// </summary>
    public interface IAgentExtension
    {
        string ChannelId { get; init; }
        void AddRoute(AgentApplication agentApplication, RouteSelector routeSelector, RouteHandler routeHandler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified);
    }
}