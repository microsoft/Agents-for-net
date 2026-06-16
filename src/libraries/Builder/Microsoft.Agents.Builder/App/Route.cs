// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Function for selecting whether a route handler should be triggered.
    /// </summary>
    /// <param name="turnContext">Context for the current turn of conversation with the user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>True if the route handler should be triggered. Otherwise, False.</returns>
    public delegate Task<bool> RouteSelector(ITurnContext turnContext, CancellationToken cancellationToken);

    /// <summary>
    /// The common route handler. Function for handling an incoming request.
    /// </summary>
    /// <param name="turnContext">A strongly-typed context object for this turn.</param>
    /// <param name="turnState">The turn state object that stores arbitrary data for this turn.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns></returns>
    public delegate Task RouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);

    public delegate Task RouteHandler<T>(ITurnContext<T> turnContext, ITurnState turnState, CancellationToken cancellationToken) where T : IActivity;

    public enum RouteFlags
    {
        None = 0,
        Agentic = 1,
        Invoke = 2,
        NonTerminal = 4
    }

    public class Route()
    {
        public ChannelId? ChannelId;

        public RouteSelector Selector;

        public RouteHandler Handler;

        public RouteFlags Flags;

        public ushort Rank = RouteRank.Unspecified;

        public Func<ITurnContext, string[]> OAuthHandlers = context => [];

        public bool IsChannelIdMatch(ChannelId channelId)
        {
            return ChannelId == null || IsWildcardMatch(ChannelId, channelId) || ChannelIdsEqual(ChannelId, channelId);
        }

        private static bool IsWildcardMatch(ChannelId pattern, ChannelId candidate)
        {
            if (pattern == null)
            {
                return false;
            }

            bool channelIsWildcard = string.Equals(pattern.Channel, "*", StringComparison.Ordinal);
            bool subChannelIsWildcard = string.Equals(pattern.SubChannel, "*", StringComparison.Ordinal);

            if (!channelIsWildcard && !subChannelIsWildcard)
            {
                return false;
            }

            // Full wildcard "*" (no subchannel) matches everything including null
            if (channelIsWildcard && string.IsNullOrEmpty(pattern.SubChannel))
            {
                return true;
            }

            if (candidate == null)
            {
                return false;
            }

            bool channelMatch = channelIsWildcard
                || string.Equals(pattern.Channel, candidate.Channel, StringComparison.OrdinalIgnoreCase);

            bool subChannelMatch = subChannelIsWildcard
                || string.Equals(pattern.SubChannel, candidate.SubChannel, StringComparison.OrdinalIgnoreCase);

            return channelMatch && subChannelMatch;
        }

        private static bool ChannelIdsEqual(ChannelId left, ChannelId right)
        {
            return left != null
                && right != null
                && left == right;
        }
    }
}
