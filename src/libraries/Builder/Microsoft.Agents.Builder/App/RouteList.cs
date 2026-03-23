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
        private bool _disposedValue = false;

        public void AddRoute(Route route)
        {
            if (rwl.TryEnterWriteLock(1000))
            {
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
            else
            {
                throw new TimeoutException("Failed to acquire write lock to add route.");
            }
        }

        public IEnumerable<Route> Enumerate()
        {
            if (rwl.TryEnterReadLock(1000))
            {
                try
                {
                    return [.. routes.Select(e => e.Route)];
                }
                finally
                {
                    rwl.ExitReadLock();
                }
            }
            else
            {
                throw new TimeoutException("Failed to acquire read lock to enumerate routes.");
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    rwl.Dispose();
                }

                _disposedValue = true;
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
