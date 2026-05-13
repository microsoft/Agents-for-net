// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Registry of <see cref="IStreamingResponseFactory"/> instances keyed by channelId.
    /// Supports both explicit DI registrations and auto-discovered factories from
    /// assembly-level <see cref="StreamingResponseFactoryAssemblyAttribute"/> attributes.
    /// </summary>
    public class StreamingResponseFactoryRegistry
    {
        private readonly ConcurrentDictionary<string, IStreamingResponseFactory> _factories = new();
        private static readonly ConcurrentDictionary<string, Type> _discoveredTypes = new();
        private static bool _scanned;
        private static readonly object _scanLock = new();

        /// <summary>Registers a factory for a channel, overwriting any previous registration.</summary>
        public void Register(string channelId, IStreamingResponseFactory factory)
            => _factories[channelId] = factory;

        /// <summary>Tries to find an explicitly registered factory for the given channelId.</summary>
        public bool TryGet(string channelId, out IStreamingResponseFactory? factory)
            => _factories.TryGetValue(channelId, out factory);

        /// <summary>
        /// Resolves a factory for the channel. Checks explicit registrations first,
        /// then falls back to auto-discovered types from assembly attributes.
        /// Auto-discovered factories are instantiated via <paramref name="services"/>
        /// if available, otherwise via <see cref="Activator.CreateInstance(Type)"/>.
        /// </summary>
        /// <param name="channelId">The channel to resolve a factory for.</param>
        /// <param name="services">Optional service provider for DI-based instantiation.</param>
        /// <returns>The factory, or null if none is registered or discovered.</returns>
        public IStreamingResponseFactory? Resolve(string channelId, IServiceProvider? services)
        {
            // Explicit DI registration wins
            if (_factories.TryGetValue(channelId, out var factory))
                return factory;

            // Auto-discovered type
            EnsureScanned();
            if (_discoveredTypes.TryGetValue(channelId, out var factoryType))
            {
                var instance = (services?.GetService(factoryType) as IStreamingResponseFactory)
                    ?? Activator.CreateInstance(factoryType) as IStreamingResponseFactory;

                if (instance != null)
                {
                    if (_factories.TryAdd(channelId, instance))
                    {
                        return instance;
                    }

                    if (_factories.TryGetValue(channelId, out var cachedFactory))
                    {
                        return cachedFactory;
                    }
                }
            }

            return null;
        }

        private static void EnsureScanned()
        {
            if (_scanned) return;
            lock (_scanLock)
            {
                if (_scanned) return;

                foreach (var (channelId, factoryType) in
                    StreamingResponseFactoryAssemblyAttribute.DiscoverFactories())
                {
                    _discoveredTypes.TryAdd(channelId, factoryType);
                }

                _scanned = true;
            }
        }
    }
}
