// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api
{
    /// <summary>
    /// Represents the outer envelope for a Slack Events API callback, containing metadata and the inner event payload
    /// as received from Slack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class models the top-level structure of a Slack Events API request, including workspace,
    /// application, and authorization context, as well as the event-specific content. Use the strongly-typed properties
    /// for common envelope fields and the <see cref="event_content"/> property for the inner event payload. Additional or
    /// unmodeled fields are accessible via the <see cref="AdditionalProperties"/> dictionary. For more information on the envelope
    /// structure, see https://docs.slack.dev/apis/events-api/#callback-field.
    /// </para>
    /// <para>
    /// Use <see cref="SlackModel.Get{T}"/> and <see cref="SlackModel.TryGet{T}"/> to access any field by dot-notation
    /// path. Top-level envelope fields (e.g. <c>"team_id"</c>), extension data fields, and nested event content fields
    /// using either the Slack JSON prefix <c>"event."</c> or the C# property prefix <c>"event_content."</c> are all
    /// supported.
    /// <code>
    /// var envelope = turnContext.Activity.GetChannelData&lt;SlackChannelData&gt;().EventEnvelope;
    /// string workspaceId = envelope.Get&lt;string&gt;("team_id");
    /// string channel     = envelope.Get&lt;string&gt;("event.channel");
    /// string itemType    = envelope.Get&lt;string&gt;("event.item.type");
    /// string blockType   = envelope.Get&lt;string&gt;("event.blocks[0].type");
    /// </code>
    /// </para>
    /// </remarks>
    public class EventEnvelope : SlackModel
    {
        /// <summary>Deprecated verification token. Slack recommends signed secrets instead.</summary>
        public string token { get; set; }

        /// <summary>Unique identifier for the workspace where this event occurred.</summary>
        public string team_id { get; set; }

        /// <summary>The workspace through which the app receives this event.</summary>
        public string context_team_id { get; set; }

        /// <summary>Enterprise org through which the app receives this event (may be null).</summary>
        public string context_enterprise_id { get; set; }

        /// <summary>Unique identifier for the application this event is intended for.</summary>
        public string api_app_id { get; set; }

        /// <summary>
        /// The inner event content. Use named properties for common fields, or navigate
        /// any event-specific field with <see cref="SlackModel.Get{T}"/>.
        /// Serialized as <c>"event"</c> in the Slack JSON payload.
        /// </summary>
        [JsonPropertyName("event")]
        public EventContent event_content { get; set; }

        /// <summary>Callback type. Typically <c>"event_callback"</c>.</summary>
        public string type { get; set; }

        /// <summary>Unique identifier for this event, globally unique across all workspaces.</summary>
        public string event_id { get; set; }

        /// <summary>Epoch timestamp (seconds) indicating when this event was dispatched.</summary>
        public long event_time { get; set; }

        /// <summary>
        /// Installation authorizations visible to this app for this event.
        /// Each element represents one installation in the scope of this event.
        /// </summary>
        /// <remarks>
        /// Access via <see cref="SlackModel.Get{T}"/>: <c>envelope.Get&lt;JsonArray&gt;("authorizations")</c>
        /// or <c>envelope.Get&lt;List&lt;JsonObject&gt;&gt;("authorizations")</c> for typed access.
        /// See https://docs.slack.dev/apis/events-api/#callback-field
        /// </remarks>
        public object authorizations { get; set; }

        /// <summary>Whether the event occurred in an externally shared channel.</summary>
        public bool is_ext_shared_channel { get; set; }

        /// <summary>
        /// Identifier for this specific event; usable with the
        /// <c>apps.event.authorizations.list</c> API method.
        /// </summary>
        public string event_context { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();

        /// <inheritdoc/>
        /// <remarks>
        /// Maps the C# property alias <c>"event_content"</c> to the JSON field name <c>"event"</c>
        /// so both prefixes are interchangeable in path strings.
        /// </remarks>
        protected override string NormalizePath(string path)
        {
            if (path.Equals("event_content", StringComparison.OrdinalIgnoreCase))
                return "event";

            if (path.StartsWith("event_content.", StringComparison.OrdinalIgnoreCase))
                return string.Concat("event", path.AsSpan("event_content".Length));

            return path;
        }
    }
}
