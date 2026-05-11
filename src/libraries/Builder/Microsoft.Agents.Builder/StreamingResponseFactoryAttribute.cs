// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Marks an <see cref="IStreamingResponseFactory"/> implementation for automatic
    /// discovery via source generator. The generator emits assembly-level metadata so
    /// the runtime can register this factory for the specified channel without explicit DI calls.
    /// </summary>
    /// <example>
    /// <code>
    /// [StreamingResponseFactory("slack")]
    /// public class SlackStreamingFactory : IStreamingResponseFactory
    /// {
    ///     public IStreamingResponse Create(ITurnContext context) => new SlackStreamingResponse(context);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class StreamingResponseFactoryAttribute : Attribute
    {
        public StreamingResponseFactoryAttribute(string channelId)
        {
            ChannelId = channelId ?? throw new ArgumentNullException(nameof(channelId));
        }

        /// <summary>The channel ID this factory handles (e.g., "msteams", "slack").</summary>
        public string ChannelId { get; }
    }
}
