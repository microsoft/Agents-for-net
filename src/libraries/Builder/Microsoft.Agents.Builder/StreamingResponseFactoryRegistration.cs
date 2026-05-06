// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Internal class used to accumulate per-channel factory registrations for
    /// building the <see cref="StreamingResponseFactoryRegistry"/> at first resolve.
    /// </summary>
    internal class StreamingResponseFactoryRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingResponseFactoryRegistration"/> class.
        /// </summary>
        /// <param name="channelId">The channel ID to register the factory for.</param>
        /// <param name="factory">The factory function that creates IStreamingResponseFactory instances.</param>
        public StreamingResponseFactoryRegistration(
            string channelId,
            Func<IServiceProvider, IStreamingResponseFactory> factory)
        {
            ChannelId = channelId;
            Factory = factory;
        }

        /// <summary>Gets the channel ID.</summary>
        public string ChannelId { get; }

        /// <summary>Gets the factory function.</summary>
        public Func<IServiceProvider, IStreamingResponseFactory> Factory { get; }
    }
}
