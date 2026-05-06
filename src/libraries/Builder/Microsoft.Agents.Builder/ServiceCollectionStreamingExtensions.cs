// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Extension methods for registering <see cref="IStreamingResponseFactory"/> implementations.
    /// </summary>
    /// <remarks>
    /// Requires <c>Microsoft.Extensions.DependencyInjection</c> (MEDI).
    /// The netstandard2.0 registry path uses <c>GetServices&lt;T&gt;()</c> returning multiple instances
    /// of the same concrete type, which is supported by MEDI but not all third-party containers.
    /// </remarks>
    public static class ServiceCollectionStreamingExtensions
    {
        /// <summary>
        /// Registers a streaming response factory for the specified channel.
        /// On .NET 8+, also registers as a keyed service for fast lookup.
        /// </summary>
        /// <typeparam name="TFactory">The factory type to register.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="channelId">The channel ID (e.g., "slack", "msteams").</param>
        public static IServiceCollection AddStreamingResponseFactory<TFactory>(
            this IServiceCollection services,
            string channelId)
            where TFactory : class, IStreamingResponseFactory
        {
            services.TryAddTransient<TFactory>();

            // Each call adds a registration entry. GetServices<T>() returns all of them.
            services.AddSingleton(new StreamingResponseFactoryRegistration(
                channelId,
                sp => sp.GetRequiredService<TFactory>()));

            // Registry singleton built lazily from all accumulated entries at first resolve.
            // TryAddSingleton: only the first registration wins — subsequent calls are no-ops.
            // This is correct: the factory delegate reads all entries from GetServices<T>().
            services.TryAddSingleton<StreamingResponseFactoryRegistry>(sp =>
            {
                var registry = new StreamingResponseFactoryRegistry();
                foreach (var reg in sp.GetServices<StreamingResponseFactoryRegistration>())
                    registry.Register(reg.ChannelId, reg.Factory(sp));
                return registry;
            });

#if NET8_0_OR_GREATER
            services.AddKeyedSingleton<IStreamingResponseFactory, TFactory>(channelId);
#endif

            return services;
        }

        /// <summary>
        /// Registers a streaming response factory instance for the specified channel.
        /// </summary>
        public static IServiceCollection AddStreamingResponseFactory(
            this IServiceCollection services,
            string channelId,
            IStreamingResponseFactory factory)
        {
            services.AddSingleton(new StreamingResponseFactoryRegistration(channelId, _ => factory));

            services.TryAddSingleton<StreamingResponseFactoryRegistry>(sp =>
            {
                var registry = new StreamingResponseFactoryRegistry();
                foreach (var reg in sp.GetServices<StreamingResponseFactoryRegistration>())
                    registry.Register(reg.ChannelId, reg.Factory(sp));
                return registry;
            });

#if NET8_0_OR_GREATER
            services.AddKeyedSingleton<IStreamingResponseFactory>(channelId, factory);
#endif

            return services;
        }
    }
}
