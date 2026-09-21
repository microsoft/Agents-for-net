// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Agents.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// Dependency-injection registration methods for channel adapters.
    /// </summary>
    public static class ChannelAdapterServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an adapter for a channel without changing the default adapter.
        /// </summary>
        /// <typeparam name="TAdapter">The adapter implementation.</typeparam>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="channelId">The channel handled by the adapter.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddChannelAdapter<TAdapter>(this IServiceCollection services, string channelId)
            where TAdapter : class, IChannelAdapter
        {
            AssertionHelpers.ThrowIfNull(services, nameof(services));
            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentException("A channelId is required.", nameof(channelId));
            }

            EnsureSingletonAdapterRegistration<TAdapter>(services);
            if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(ChannelAdapterRegistration)
                && descriptor.ImplementationInstance is ChannelAdapterRegistration registration
                && string.Equals(registration.ChannelId, channelId, StringComparison.OrdinalIgnoreCase)
                && registration.AdapterType == typeof(TAdapter)))
            {
                services.AddSingleton(new ChannelAdapterRegistration(channelId, typeof(TAdapter)));
            }

            services.TryAddSingleton<IChannelAdapterRegistry, ChannelAdapterRegistry>();
            return services;
        }

        /// <summary>
        /// Selects an adapter as the default unless a default has already been selected.
        /// </summary>
        /// <typeparam name="TAdapter">The default adapter implementation.</typeparam>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection TrySetDefaultChannelAdapter<TAdapter>(this IServiceCollection services)
            where TAdapter : class, IChannelAdapter
        {
            AssertionHelpers.ThrowIfNull(services, nameof(services));

            EnsureSingletonAdapterRegistration<TAdapter>(services);
            services.TryAddSingleton<IChannelAdapterRegistry, ChannelAdapterRegistry>();
            services.TryAddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TAdapter>());
            return services;
        }

        /// <summary>
        /// Explicitly selects an adapter as the default, replacing any previous default selection.
        /// </summary>
        /// <typeparam name="TAdapter">The default adapter implementation.</typeparam>
        /// <param name="services">The service collection to configure.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection SetDefaultChannelAdapter<TAdapter>(this IServiceCollection services)
            where TAdapter : class, IChannelAdapter
        {
            AssertionHelpers.ThrowIfNull(services, nameof(services));

            EnsureSingletonAdapterRegistration<TAdapter>(services);
            services.TryAddSingleton<IChannelAdapterRegistry, ChannelAdapterRegistry>();
            services.RemoveAll<IChannelAdapter>();
            services.AddSingleton<IChannelAdapter>(sp => sp.GetRequiredService<TAdapter>());
            return services;
        }

        private static void EnsureSingletonAdapterRegistration<TAdapter>(IServiceCollection services)
            where TAdapter : class, IChannelAdapter
        {
            var registrations = services.Where(descriptor => descriptor.ServiceType == typeof(TAdapter)).ToList();
            if (registrations.Any(descriptor => descriptor.Lifetime != ServiceLifetime.Singleton))
            {
                throw new InvalidOperationException(
                    $"{typeof(TAdapter).FullName} must be registered as a singleton because channel adapter instances are cached by the registry.");
            }

            services.TryAddSingleton<TAdapter>();
        }
    }
}
