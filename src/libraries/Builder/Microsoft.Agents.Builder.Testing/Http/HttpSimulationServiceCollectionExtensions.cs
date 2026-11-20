// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Agents.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>Integration with the standard Microsoft HTTP client factory.</summary>
    public static class HttpSimulationServiceCollectionExtensions
    {
        /// <summary>Preserves the standard factory and its client configuration, replacing only supported terminal transports.</summary>
        /// <remarks>The supplied simulation remains caller-owned. Fresh primary delegating chains and additional handlers are preserved.</remarks>
        public static IServiceCollection AddHttpSimulation(this IServiceCollection services, HttpSimulation simulation)
        {
            AssertionHelpers.ThrowIfNull(services, nameof(services));
            AssertionHelpers.ThrowIfNull(simulation, nameof(simulation));

            services.AddHttpClient();
            var existing = services.Select(d => d.ImplementationInstance).OfType<SimulationFilter>().SingleOrDefault();
            if (existing != null)
            {
                if (!ReferenceEquals(existing.Simulation, simulation))
                {
                    throw new InvalidOperationException("Only one HTTP simulation can be registered in a service collection.");
                }

                return services;
            }

            services.Insert(0, ServiceDescriptor.Singleton<IHttpMessageHandlerBuilderFilter>(new SimulationFilter(simulation)));
            return services;
        }

        private sealed class SimulationFilter(HttpSimulation simulation) : IHttpMessageHandlerBuilderFilter
        {
            private readonly object _gate = new();
            private readonly ConditionalWeakTable<HttpMessageHandler, object> _claimed = new();
            internal HttpSimulation Simulation { get; } = simulation;

            public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
            {
                next(builder);
                ReplaceTerminal(builder);
            };

            private void ReplaceTerminal(HttpMessageHandlerBuilder builder)
            {
                lock (_gate)
                {
                    var chain = new List<HttpMessageHandler>();
                    var current = builder.PrimaryHandler;
                    DelegatingHandler last = null;
                    while (current != null)
                    {
                        if (chain.Any(handler => ReferenceEquals(handler, current)))
                        {
                            throw SetupError("a cyclic primary delegating-handler chain");
                        }

                        if (_claimed.TryGetValue(current, out _))
                        {
                            throw SetupError("a reused primary handler or terminal transport");
                        }

                        chain.Add(current);
                        if (current is not DelegatingHandler delegating)
                        {
                            break;
                        }

                        last = delegating;
                        current = delegating.InnerHandler;
                    }

                    if (current == null)
                    {
                        throw SetupError("a primary delegating handler with no terminal transport");
                    }

                    try
                    {
                        if (current is HttpClientHandler clientHandler)
                        {
                            if (clientHandler.GetType().GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic,
                                null, [typeof(HttpRequestMessage), typeof(CancellationToken)], null)?.DeclaringType != typeof(HttpClientHandler))
                            {
                                throw SetupError("a custom HttpClientHandler transport override");
                            }

                            clientHandler.UseCookies = clientHandler.UseCookies;
                        }
#if NET8_0_OR_GREATER
                        else if (current is SocketsHttpHandler socketsHandler)
                        {
                            socketsHandler.UseCookies = socketsHandler.UseCookies;
                        }
#endif
                        else
                        {
                            throw SetupError("an opaque or unsupported primary terminal transport");
                        }

                        foreach (var delegating in chain.OfType<DelegatingHandler>())
                        {
                            delegating.InnerHandler = delegating.InnerHandler;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        throw SetupError("an active, disposed, or unsupported primary handler");
                    }

                    var replacement = new HttpSimulationHandler(Simulation, builder.Name, current);
                    if (last == null)
                    {
                        builder.PrimaryHandler = replacement;
                    }
                    else
                    {
                        last.InnerHandler = replacement;
                    }

                    foreach (var handler in chain)
                    {
                        _claimed.Add(handler, new object());
                    }
                }
            }

            private static InvalidOperationException SetupError(string reason)
                => new($"HTTP simulation cannot replace {reason}. Supply a fresh chain ending in a standard HttpClientHandler"
                    + " (or SocketsHttpHandler on supported targets), or use explicit CreateClient/CreateHandler injection.");
        }
    }
}
