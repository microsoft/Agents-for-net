// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.Adapters
{
    /// <summary>
    /// Declares that an <see cref="IChannelAdapter"/> implementation handles inbound requests for a
    /// specific <c>channelId</c>. Annotated adapters are auto-registered in the
    /// <see cref="IChannelAdapterRegistry"/> so that shared-endpoint (Tier 2) dispatch and SDK features
    /// (proactive messaging, diagnostics) can resolve the correct adapter by channelId.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute operates like <c>Microsoft.Agents.Core.Serialization.ActivityTypeAttribute</c>:
    /// a source generator (<c>ChannelAdapterInitSourceGenerator</c>) emits an assembly-level
    /// <see cref="ChannelAdapterInitAssemblyAttribute"/> for every annotated adapter, and those
    /// assembly attributes are read off the loaded assemblies at runtime — so no explicit DI
    /// registration call (such as <c>AddChannelAdapter</c>) is required. Simply reference the
    /// assembly that declares the annotated adapter.
    /// </para>
    /// <para>
    /// The annotated type must implement <see cref="IChannelAdapter"/> (so it can be resolved through
    /// <see cref="IChannelAdapterRegistry"/>). A public annotated adapter can preload its assembly in a
    /// consuming application. An internal adapter is discovered after its assembly is loaded, so its
    /// extension must provide another public preload anchor. To also serve shared-endpoint HTTP dispatch
    /// the adapter must
    /// implement the host's HTTP adapter contract (<c>IAgentHttpAdapter</c> in
    /// <c>Microsoft.Agents.Hosting.AspNetCore</c>). <c>CloudAdapter</c> implements both contracts;
    /// a type deriving directly from <c>ChannelAdapter</c> must implement the host contract separately
    /// when it participates in HTTP dispatch. It does <b>not</b> need to derive from
    /// <c>CloudAdapter</c> — the default Activity Protocol adapter (CloudAdapter) remains the registry
    /// default and is not annotated.
    /// </para>
    /// <example>
    /// <code>
    /// // Handles only Teams traffic routed through the shared /api/messages endpoint:
    /// [ChannelAdapter("msteams")]
    /// public class TeamsChannelAdapter : CloudAdapter { }
    ///
    /// // A dedicated-protocol adapter that also participates in the registry:
    /// [ChannelAdapter("a2a")]
    /// internal class DedicatedProtocolAdapter : ChannelAdapter, IAgentHttpAdapter { }
    /// </code>
    /// </example>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class ChannelAdapterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelAdapterAttribute"/> class.
        /// </summary>
        /// <param name="channelId">The <c>channelId</c> this adapter handles (e.g., "msteams", "a2a").</param>
        public ChannelAdapterAttribute(string channelId)
        {
            ChannelId = channelId;
        }

        /// <summary>
        /// The <c>channelId</c> this adapter handles (e.g., "msteams", "a2a").
        /// </summary>
        public string ChannelId { get; }
    }
}
