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

        public void AddRoute(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified)
        {
            try
            {
                rwl.AcquireWriterLock(1000);
                routes.Add(new RouteEntry() { Rank = rank, Route = new(selector, handler), IsInvokeRoute = isInvokeRoute });

                // Invoke selectors are first.
                // Invoke Activities from Teams need to be responded to in less than 5 seconds and the selectors are async
                // which could incur delays, so we need to limit this possibility.
                routes = [.. routes.OrderByDescending(entry => entry.IsInvokeRoute).ThenBy(entry => entry.Rank)];
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

    class RouteEntry
    {
        public ushort Rank;
        public Route Route;
        public bool IsInvokeRoute;
    }
}
