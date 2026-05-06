// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Dictionary-based registry of <see cref="IStreamingResponseFactory"/> instances keyed by channelId.
    /// Used as the netstandard2.0-compatible resolution path alongside keyed DI services on .NET 8+.
    /// Requires Microsoft.Extensions.DependencyInjection — third-party containers are not supported.
    /// </summary>
    public class StreamingResponseFactoryRegistry
    {
        private readonly Dictionary<string, IStreamingResponseFactory> _factories = new();

        /// <summary>Registers a factory for a channel, overwriting any previous registration.</summary>
        public void Register(string channelId, IStreamingResponseFactory factory)
            => _factories[channelId] = factory;

        /// <summary>Tries to find a factory for the given channelId.</summary>
        public bool TryGet(string channelId, out IStreamingResponseFactory? factory)
            => _factories.TryGetValue(channelId, out factory);
    }
}
