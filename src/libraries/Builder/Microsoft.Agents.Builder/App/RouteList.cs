// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Agents.Builder.App
{
    internal class RouteList
    {
        private readonly ReaderWriterLockSlim rwl = new();
        private List<RouteEntry> routes = [];

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

        // Returns both the count and formatted string from a single lock acquisition,
        // avoiding a TOCTOU race if routes are modified between separate Count and FormatRouteList calls.
        internal (int Count, string Formatted) FormatRouteList(ITurnContext turnContext = null)
        {
            var orderedRoutes = Enumerate();
            var entries = new List<Dictionary<string, object>>();
            int index = 0;
            foreach (var route in orderedRoutes)
            {
                var parts = new List<string>();
                if ((route.Flags & RouteFlags.Invoke) != 0) parts.Add("Invoke");
                if ((route.Flags & RouteFlags.Agentic) != 0) parts.Add("Agentic");
                if ((route.Flags & RouteFlags.NonTerminal) != 0) parts.Add("NonTerminal");

                var entry = new Dictionary<string, object>
                {
                    ["Index"] = index,
                    ["Handler"] = GetHandlerName(route.Handler),
                    ["Flags"] = parts.Count > 0 ? string.Join(",", parts) : "None",
                    ["Rank"] = route.Rank,
                    ["Channel"] = route.ChannelId?.Channel ?? "*"
                };

                if (turnContext != null && route.OAuthHandlers != null)
                {
                    try
                    {
                        var oauthHandlers = route.OAuthHandlers(turnContext);
                        if (oauthHandlers != null && oauthHandlers.Length > 0)
                        {
                            entry["OAuthHandlers"] = oauthHandlers;
                        }
                    }
                    catch (Exception ex)
                    {
                        entry["OAuthHandlersError"] = string.IsNullOrEmpty(ex.Message)
                            ? ex.GetType().Name
                            : $"{ex.GetType().Name}: {ex.Message}";
                    }
                }

                entries.Add(entry);
                index++;
            }

            return (index, JsonSerializer.Serialize(entries));
        }

        // Produces a human-readable name for a route handler delegate.
        // For named methods: "MyAgent.OnMessageAsync"
        // For lambdas/local functions: "MyAgent.OnConfigureApplicationAsync" (enclosing method extracted from compiler-generated name)
        private static string GetHandlerName(RouteHandler handler)
        {
            // Resolve the real declaring type by walking up through compiler-generated nested types.
            // Lambdas/closures are hosted on types like "MyAgent+<>c__DisplayClass0_0"; DeclaringType gives "MyAgent".
            var type = handler.Target?.GetType() ?? handler.Method.DeclaringType;
            while (type != null && type.Name.Contains('<'))
            {
                type = type.DeclaringType;
            }
            var typeName = type?.Name;

            // Extract the enclosing method name from compiler-generated names like "<OnConfigureApplicationAsync>b__0".
            var methodName = handler.Method.Name;
            if (methodName.Length > 1 && methodName[0] == '<')
            {
                var end = methodName.IndexOf('>');
                if (end > 1)
                {
                    methodName = methodName.Substring(1, end - 1);
                }
            }

            return typeName != null ? $"{typeName}.{methodName}" : methodName;
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
