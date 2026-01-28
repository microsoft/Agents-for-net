// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Agents.Builder.App
{
    internal class RouteList
    {
        private readonly ReaderWriterLock rwl = new();
        private List<RouteEntry> routes = [];

        public void AddRoute(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, params string[] autoSignInHandlers)
        {
            AddRoute(selector, handler, false, isInvokeRoute, rank, autoSignInHandlers);
        }

        public void AddRoute(RouteSelector selector, RouteHandler handler, bool isAgenticRoute, bool isInvokeRoute, ushort rank = RouteRank.Unspecified, params string[] autoSignInHandlers)
        {
            try
            {
                rwl.AcquireWriterLock(1000);
                routes.Add(new RouteEntry(rank, new(selector, handler, isInvokeRoute, isAgenticRoute, autoSignInHandlers)));

                // Ordered by:
                //    Agentic + Invoke
                //    Invoke
                //    Agentic
                //    Other
                // Then by Rank
                routes = [.. routes
                    .OrderByDescending(entry => entry.Type)
                    .ThenBy(entry => entry.Rank)];
            }
            finally
            {
                rwl.ReleaseWriterLock();
            }
        }

        public IEnumerable<Route> Enumerate()
        {
            try
            {
                rwl.AcquireReaderLock(1000);
                return [.. routes.Select(e => e.Route).ToList()];
            }
            finally
            {
                rwl.ReleaseReaderLock();
            }
        }
    }

    enum RouteEntryType
    {
        Other = 0,
        Agentic = 1,
        Invoke = 2,
        AgenticInvoke = 3
    }

    class RouteEntry
    {
        public RouteEntry(ushort rank, Route route)
        {
            Rank = rank;
            Route = route;
            if (route.IsInvokeRoute)
                Type = RouteEntryType.Invoke;
            if (route.IsAgenticRoute)
                Type |= RouteEntryType.Agentic;
        }

        public ushort Rank { get; private set; }
        public Route Route { get; private set; }
        public RouteEntryType Type { get; private set; }
    }
}
