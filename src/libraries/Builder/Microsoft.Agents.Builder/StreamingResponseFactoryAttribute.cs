// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Marks an <see cref="IStreamingResponseFactory"/> for automatic, per-channel registration.  A factory
    /// decorated with this attribute is discovered on module load and used to create the
    /// <c>ITurnContext.StreamingResponse</c> for turns whose channel id matches <see cref="ChannelId"/> - no
    /// explicit service-collection registration call is required.
    /// </summary>
    /// <remarks>
    /// This attribute plays the same role as <c>SerializationInitAttribute</c> and <c>EntityNameAttribute</c> in
    /// the serialization subsystem: a source generator emits a
    /// <see cref="StreamingResponseFactoryAssemblyAttribute"/> for each decorated type, and
    /// <see cref="StreamingResponseFactoryAssemblyAttribute.InitStreamingResponseFactories"/> reads this attribute
    /// at runtime to register the factory in <see cref="StreamingResponseFactoryCatalog"/> keyed by
    /// <see cref="ChannelId"/>.  The factory type is instantiated per channel via the adapter's service provider,
    /// so its constructor dependencies (for example <c>IHttpClientFactory</c> and <c>IConfiguration</c>) are
    /// injected from DI.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class StreamingResponseFactoryAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StreamingResponseFactoryAttribute"/> class.
        /// </summary>
        /// <param name="channelId">The channel id the factory applies to (e.g. "slack").</param>
        public StreamingResponseFactoryAttribute(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            ChannelId = channelId;
        }

        /// <summary>
        /// Gets the channel id the decorated factory applies to.
        /// </summary>
        public string ChannelId { get; }
    }
}
