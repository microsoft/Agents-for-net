// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Agents.Builder.App
{
    internal class RouteList : IDisposable
    {
        private readonly ReaderWriterLockSlim rwl = new();
        private List<RouteEntry> routes = [];

        public void AddRoute(Route route)
        {
            rwl.EnterWriteLock();
            try
            {
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
                rwl.ExitWriteLock();
            }
        }

        public IEnumerable<Route> Enumerate()
        {
            rwl.EnterReadLock();
            try
            {
                return [.. routes.Select(e => e.Route)];
            }
            finally
            {
                rwl.ExitReadLock();
            }
        }

        public void Dispose()
        {
            rwl.Dispose();
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
