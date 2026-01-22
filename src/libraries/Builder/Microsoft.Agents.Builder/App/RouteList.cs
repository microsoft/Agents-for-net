// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Subjects;

namespace Microsoft.Agents.Builder.App
{
    internal class RouteList : IDisposable
    {
        private readonly BehaviorSubject<ImmutableList<RouteEntry>> _routes = new(ImmutableList<RouteEntry>.Empty);
        private bool _disposed;

        public void AddRoute(RouteSelector selector, RouteHandler handler, bool isInvokeRoute = false, ushort rank = RouteRank.Unspecified, params string[] autoSignInHandlers)
        {
            AddRoute(selector, handler, false, isInvokeRoute, rank, autoSignInHandlers);
        }

        public void AddRoute(RouteSelector selector, RouteHandler handler, bool isAgenticRoute, bool isInvokeRoute, ushort rank = RouteRank.Unspecified, params string[] autoSignInHandlers)
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(RouteList));

            // Atomically update the immutable list
            ImmutableList<RouteEntry> currentRoutes, newRoutes;
            do
            {
                currentRoutes = _routes.Value;
                var newEntry = new RouteEntry(rank, new Route(selector, handler, isInvokeRoute, isAgenticRoute, autoSignInHandlers));

                // Ordered by:
                //    Agentic + Invoke
                //    Invoke
                //    Agentic
                //    Other
                // Then by Rank
                newRoutes = currentRoutes
                    .Add(newEntry)
                    .OrderByDescending(entry => entry.Type)
                    .ThenBy(entry => entry.Rank)
                    .ToImmutableList();
            }
            while (_routes.Value != currentRoutes);

            _routes.OnNext(newRoutes);
        }

        public IEnumerable<Route> Enumerate()
        {
            AssertionHelpers.ThrowIfObjectDisposed(_disposed, nameof(RouteList));

            // Return a snapshot - immutable so no locking needed
            return _routes.Value.Select(e => e.Route).ToList();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _routes.Dispose();
                }
                _disposed = true;
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
