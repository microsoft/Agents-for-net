// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// Registry of <see cref="IChannelAdapter"/> instances keyed by <c>channelId</c>.
    /// Lets SDK features and developers resolve the correct adapter for a given channel — for example to
    /// send proactive messages or continue a conversation — without needing to know adapter types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The interface lives in the Builder layer so it can be surfaced through
    /// <see cref="AgentApplicationOptions.ChannelAdapterRegistry"/>. The default implementation also lives
    /// in the Builder layer and is populated from adapters annotated with the
    /// <c>[ChannelAdapter("channelId")]</c> attribute and adapters registered explicitly through DI.
    /// </para>
    /// <para>
    /// Shared-endpoint (Tier 2) HTTP dispatch also consults the registry, casting resolved adapters to the
    /// host's HTTP adapter contract. It is only consulted when <see cref="HasChannelSpecificAdapters"/> is
    /// <see langword="true"/> — preserving current performance for the common single-adapter case.
    /// </para>
    /// </remarks>
    public interface IChannelAdapterRegistry
    {
        /// <summary>
        /// Gets the adapter registered for the specified <paramref name="channelId"/>.
        /// </summary>
        /// <param name="channelId">The channelId to resolve.</param>
        /// <returns>The adapter registered for the channel.</returns>
        /// <exception cref="System.InvalidOperationException">No adapter is registered for the channel.</exception>
        IChannelAdapter GetAdapter(string channelId);

        /// <summary>
        /// Gets the configured default adapter. Used as fallback when no channel-specific adapter is registered.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">No default adapter is configured.</exception>
        IChannelAdapter GetDefault();

        /// <summary>
        /// Tries to get the adapter for the specified <paramref name="channelId"/>.
        /// </summary>
        /// <param name="channelId">The channelId to resolve.</param>
        /// <param name="adapter">The resolved adapter, or <see langword="null"/> if none is registered.</param>
        /// <returns><see langword="true"/> if a channel-specific adapter is registered; otherwise <see langword="false"/>.</returns>
        bool TryGetAdapter(string channelId, out IChannelAdapter adapter);

        /// <summary>
        /// Gets a value indicating whether any channel-specific adapters are registered beyond the default.
        /// When <see langword="false"/>, the Tier 2 channelId peek in <c>MapAgentApplicationEndpoints</c>
        /// is skipped entirely — preserving current performance.
        /// </summary>
        bool HasChannelSpecificAdapters { get; }

        /// <summary>
        /// Gets all registered adapters (default plus channel-specific).
        /// </summary>
        IEnumerable<IChannelAdapter> GetAll();
    }
}
