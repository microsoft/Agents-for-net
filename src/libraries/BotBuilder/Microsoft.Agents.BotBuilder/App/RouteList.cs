// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Microsoft.Agents.BotBuilder.App
{
    internal class RouteList
    {
        private readonly ReaderWriterLock rwl = new();
        private List<RouteEntry> routes = [];

        public void AddRoute(RouteSelectorAsync selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified)
        {
            try
            {
                rwl.AcquireWriterLock(1000);
                routes.Add(new RouteEntry() { Rank = rank, Route = new(selector, handler), IsInvokeRoute = isInvokeRoute });
                routes = [.. routes.OrderBy(entry => entry.Rank)];
            }
            finally
            {
                rwl.ReleaseWriterLock();
            }
        }

        public IEnumerable<Route> Enumerate(bool isInvokeRoute = false)
        {
            try
            {
                rwl.AcquireReaderLock(1000);
                return new List<Route>(routes.Where(e => e.IsInvokeRoute == isInvokeRoute).Select(e => e.Route).ToList());
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
