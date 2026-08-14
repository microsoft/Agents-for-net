// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Agents.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// Default <see cref="IChannelAdapterRegistry"/> implementation.
    /// </summary>
    /// <remarks>
    /// Channel-specific adapters are discovered from two sources and merged at construction:
    /// <list type="number">
    /// <item>Attribute-based discovery — <see cref="ChannelAdapterInitAssemblyAttribute"/> instances
    /// (emitted by the source generator for every <see cref="ChannelAdapterAttribute"/>) read off the
    /// loaded assemblies.</item>
    /// <item>Explicit <see cref="ChannelAdapterRegistration"/> descriptors registered in DI, which take
    /// precedence over discovered registrations for the same channelId.</item>
    /// </list>
    /// Adapter instances are resolved lazily and cached (one instance per adapter type) using the service
    /// provider — an adapter type registered in DI resolves to its DI singleton; otherwise it is created
    /// via <see cref="ActivatorUtilities"/> so that annotated adapters do not require an explicit DI
    /// registration.
    /// </remarks>
    internal sealed class ChannelAdapterRegistry : IChannelAdapterRegistry
    {
        private readonly IServiceProvider _services;
        private readonly Dictionary<string, Type> _channelAdapters;
        private readonly ConcurrentDictionary<Type, IChannelAdapter> _instances = new();
        private IChannelAdapter _default;

        public ChannelAdapterRegistry(IServiceProvider services, IEnumerable<ChannelAdapterRegistration> registrations)
            : this(services, registrations, null)
        {
        }

        internal ChannelAdapterRegistry(
            IServiceProvider services,
            IEnumerable<ChannelAdapterRegistration> registrations,
            IChannelAdapter defaultAdapter)
        {
            _services = services;
            _default = defaultAdapter;
            _channelAdapters = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            if (defaultAdapter != null)
            {
                _instances.TryAdd(defaultAdapter.GetType(), defaultAdapter);
            }

            AgentSdkInitializer.EnsureInitialized();

            // Attribute-discovered registrations first...
            foreach (var registration in ChannelAdapterInitAssemblyAttribute.GetRegistrations())
            {
                _channelAdapters[registration.ChannelId] = registration.AdapterType;
            }

            // ...then explicit DI registrations, which override discovered ones for the same channelId.
            if (registrations != null)
            {
                foreach (var registration in registrations)
                {
                    if (registration != null && !string.IsNullOrEmpty(registration.ChannelId) && registration.AdapterType != null)
                    {
                        _channelAdapters[registration.ChannelId] = registration.AdapterType;
                    }
                }
            }
        }

        public bool HasChannelSpecificAdapters => _channelAdapters.Count > 0;

        public IChannelAdapter GetDefault()
        {
            if (_default != null)
            {
                return _default;
            }

            return _default = _services.GetRequiredService<IChannelAdapter>();
        }

        public IChannelAdapter GetAdapter(string channelId)
        {
            if (channelId != null && _channelAdapters.TryGetValue(channelId, out var type))
            {
                return Resolve(type);
            }

            throw new InvalidOperationException($"No adapter registered for channel '{channelId}'.");
        }

        public bool TryGetAdapter(string channelId, out IChannelAdapter adapter)
        {
            if (channelId != null && _channelAdapters.TryGetValue(channelId, out var type))
            {
                adapter = Resolve(type);
                return true;
            }

            adapter = null;
            return false;
        }

        public IEnumerable<IChannelAdapter> GetAll()
        {
            var defaultAdapter = GetDefault();
            yield return defaultAdapter;
            foreach (var type in _channelAdapters.Values.Distinct())
            {
                var adapter = Resolve(type);
                if (!ReferenceEquals(adapter, defaultAdapter))
                {
                    yield return adapter;
                }
            }
        }

        private IChannelAdapter Resolve(Type type)
        {
            return _instances.GetOrAdd(type, t => (IChannelAdapter)ActivatorUtilities.GetServiceOrCreateInstance(_services, t));
        }
    }
}
