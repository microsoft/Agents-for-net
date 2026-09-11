// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// A process-wide catalog that maps a channel id to the <see cref="IStreamingResponseFactory"/> type
    /// registered for that channel via <see cref="StreamingResponseFactoryAttribute"/>.
    /// </summary>
    /// <remarks>
    /// The catalog is populated on module load by scanning loaded assemblies (and assemblies loaded later) for
    /// <see cref="StreamingResponseFactoryAssemblyAttribute"/>, mirroring how the serialization subsystem builds
    /// <c>ProtocolJsonSerializer.EntityTypes</c>.  The adapter looks up a factory type here and instantiates it
    /// per channel using its service provider, so no explicit registration call is required.
    /// </remarks>
    public static class StreamingResponseFactoryCatalog
    {
        private static readonly ConcurrentDictionary<string, Type> _factoryTypes =
            new(StringComparer.OrdinalIgnoreCase);

        private static int _initialized;

        /// <summary>
        /// Registers (or replaces) the factory type for a channel id.  Called by
        /// <see cref="StreamingResponseFactoryAssemblyAttribute"/> during assembly scanning.
        /// </summary>
        /// <param name="channelId">The channel id (e.g. "slack").</param>
        /// <param name="factoryType">The <see cref="IStreamingResponseFactory"/> implementation type.</param>
        public static void Register(string channelId, Type factoryType)
        {
            if (string.IsNullOrWhiteSpace(channelId) || factoryType == null)
            {
                return;
            }

            _factoryTypes[channelId] = factoryType;
        }

        /// <summary>
        /// Attempts to get the factory type registered for a channel id.
        /// </summary>
        /// <param name="channelId">The channel id to look up.</param>
        /// <param name="factoryType">The registered factory type, if found.</param>
        /// <returns><c>true</c> when a factory type is registered for <paramref name="channelId"/>.</returns>
        public static bool TryGetFactoryType(string channelId, out Type factoryType)
        {
            EnsureInitialized();

            if (channelId == null)
            {
                factoryType = null;
                return false;
            }

            return _factoryTypes.TryGetValue(channelId, out factoryType);
        }

        /// <summary>
        /// Ensures the catalog has been populated from the currently loaded assemblies and that a handler is
        /// registered to scan assemblies loaded later.  Safe to call multiple times; scanning runs once.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 0)
            {
                StreamingResponseFactoryAssemblyAttribute.InitStreamingResponseFactories();
            }
        }
    }
}
