// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// A single declarative channel-adapter registration: the <c>channelId</c> the adapter handles and
    /// the CLR adapter type that handles it. Built from <see cref="ChannelAdapterAttribute"/> (via the
    /// source-generated <see cref="ChannelAdapterInitAssemblyAttribute"/>) or supplied explicitly through
    /// dependency injection.
    /// </summary>
    public sealed class ChannelAdapterRegistration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelAdapterRegistration"/> class.
        /// </summary>
        /// <param name="channelId">The <c>channelId</c> this adapter handles (e.g., "msteams", "a2a").</param>
        /// <param name="adapterType">The adapter CLR type. Must implement <see cref="IChannelAdapter"/>.</param>
        public ChannelAdapterRegistration(string channelId, Type adapterType)
        {
            ChannelId = channelId;
            AdapterType = adapterType;
        }

        /// <summary>The <c>channelId</c> this adapter handles (e.g., "msteams", "a2a").</summary>
        public string ChannelId { get; }

        /// <summary>The adapter CLR type. Must implement <see cref="IChannelAdapter"/>.</summary>
        public Type AdapterType { get; }
    }
}
