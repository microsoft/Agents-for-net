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

        public void AddRoute(Route route)
        {
            try
            {
                rwl.AcquireWriterLock(1000);
                routes.Add(new RouteEntry(route));

                // Ordered by:
                //    Agentic + Invoke
                //    Invoke
                //    Agentic
                //    Other
                // Then by Rank
                routes = [.. routes
                    .OrderByDescending(entry => entry.Order)
                    .ThenBy(entry => entry.Route.Rank)];
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

    enum RouteEntryOrder
    {
        Other = 0,
        Agentic = 1,
        Invoke = 2,
        AgenticInvoke = 3
    }

    class RouteEntry
    {
        public RouteEntry(Route route) 
        { 
            Route = route;
            if (route.Flags.HasFlag(RouteFlags.Invoke))
                Order = RouteEntryOrder.Invoke;
            if (route.Flags.HasFlag(RouteFlags.Agentic))
                Order |= RouteEntryOrder.Agentic;
        }

        public Route Route { get; private set; }
        public RouteEntryOrder Order { get; private set; }
    }
}
