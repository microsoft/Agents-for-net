// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// The discriminator values peeked from an inbound Activity's JSON, used to resolve which
    /// custom <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass to deserialize into.
    /// </summary>
    /// <remarks>
    /// Only the well-known top-level discriminators are surfaced (cheap to peek without a full
    /// parse). <see cref="ChannelId"/> is the channel segment only — any <c>:product</c> sub-channel
    /// suffix is stripped — so matching against a bare channel id (e.g. "msteams") works regardless
    /// of the <see cref="ProtocolJsonSerializer.ChannelIdIncludesProduct"/> setting.
    /// </remarks>
    public readonly struct ActivityResolutionContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityResolutionContext"/> struct.
        /// </summary>
        /// <param name="type">The Activity <c>type</c> field, or <see langword="null"/> if absent.</param>
        /// <param name="channelId">The Activity <c>channelId</c> (channel segment only), or <see langword="null"/> if absent.</param>
        /// <param name="name">The Activity <c>name</c> field, or <see langword="null"/> if absent.</param>
        public ActivityResolutionContext(string type, string channelId, string name)
        {
            Type = type;
            ChannelId = channelId;
            Name = name;
        }

        /// <summary>The Activity <c>type</c> field (e.g., "message", "invoke", "x-custom").</summary>
        public string Type { get; }

        /// <summary>The Activity <c>channelId</c> channel segment (e.g., "msteams").</summary>
        public string ChannelId { get; }

        /// <summary>The Activity <c>name</c> field (e.g., an invoke name like "task/fetch").</summary>
        public string Name { get; }
    }

    /// <summary>
    /// An imperative resolver that maps an inbound Activity to a custom
    /// <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass to deserialize into.
    /// </summary>
    /// <param name="reader">
    /// A private, forward-only <see cref="System.Text.Json.Utf8JsonReader"/> positioned at the
    /// Activity's opening <see cref="System.Text.Json.JsonTokenType.StartObject"/> token. This is a
    /// throwaway copy — reading from it does not affect deserialization, and each resolver receives
    /// its own copy, so resolvers never interfere with one another. Use it to discriminate on any
    /// property (including nested ones) that the well-known <paramref name="context"/> discriminators
    /// don't surface. Resolvers that only need <c>type</c>/<c>channelId</c>/<c>name</c> can ignore it.
    /// </param>
    /// <param name="context">
    /// The well-known top-level discriminators (<c>type</c>/<c>channelId</c>/<c>name</c>) already
    /// peeked from the Activity JSON, provided for convenience so common cases avoid re-scanning.
    /// </param>
    /// <returns>
    /// The CLR type (must derive from <see cref="Microsoft.Agents.Core.Models.Activity"/>) to
    /// deserialize into, or <see langword="null"/> to defer to other resolvers / declarative
    /// registrations / the base <see cref="Microsoft.Agents.Core.Models.Activity"/>.
    /// </returns>
    public delegate System.Type ActivityTypeResolver(ref System.Text.Json.Utf8JsonReader reader, in ActivityResolutionContext context);
}
