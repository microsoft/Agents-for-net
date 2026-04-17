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
    /// for common envelope fields and the `event_content` property for the inner event payload. Additional or unmodeled
    /// fields are accessible via the `Properties` dictionary. For more information on the envelope structure, see
    /// https://docs.slack.dev/apis/events-api/#callback-field.
    /// </para>
    /// <para>
    /// Use the <see cref="Get{T}"/> and <see cref="TryGet{T}"/> methods to access nested fields within the inner 
    /// event content using dot-notation paths.  The path matches the JSON structure of the Slack event payload.  For example:
    /// <code>
    /// var envelope = turnContext.Activity.GetChannelData&lt;SlackChannelData&gt;().EventEnvelope;
    /// var eventType = envelope.Get&lt;string&gt;("event.type"); // Access top-level event type
    /// var channel = envelope.Get&lt;string&gt;("event.channel"); // Access channel field within the event payload
    /// </code>
    /// </para>
    /// </remarks>
    public class EventEnvelope
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
        /// any event-specific field with <see cref="EventContent.Get{T}"/>.
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
        public List<SlackAuthorization> authorizations { get; set; }

        /// <summary>Whether the event occurred in an externally shared channel.</summary>
        public bool is_ext_shared_channel { get; set; }

        /// <summary>
        /// Identifier for this specific event; usable with the
        /// <c>apps.event.authorizations.list</c> API method.
        /// </summary>
        public string event_context { get; set; }

        /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
        [JsonExtensionData]
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();

        /// <summary>
        /// Gets a value at the given dot-notation path within this Slack event envelope.
        /// The inner event content can be addressed with either the <c>"event."</c> prefix
        /// (matching the Slack JSON field name) or the <c>"event_content."</c> prefix
        /// (matching the C# property name).
        /// <example>
        /// <code>
        /// // reaction_removed: access nested item fields — both forms work:
        /// string itemType = envelope.GetValue&lt;string&gt;("event.item.type");
        /// string itemType = envelope.GetValue&lt;string&gt;("event_content.item.type");
        ///
        /// // message: access first block type
        /// string blockType = envelope.GetValue&lt;string&gt;("event.blocks[0].type");
        /// </code>
        /// </example>
        /// See https://docs.slack.dev/apis/events-api/#callback-field for envelope field names
        /// and https://docs.slack.dev/reference/events for event-specific field names.
        /// </summary>
        public T Get<T>(string path)
        {
            if (event_content != null && TryStripEventPrefix(path, out var innerPath))
            {
                return event_content.Get<T>(innerPath);
            }

            return default;
        }

        /// <summary>
        /// Tries to get a value at the given dot-notation path within this Slack event envelope.
        /// Returns <see langword="false"/> when the path does not exist or the value cannot
        /// be converted to <typeparamref name="T"/>.
        /// </summary>
        public bool TryGet<T>(string path, out T value)
        {
            if (event_content != null && TryStripEventPrefix(path, out var innerPath))
            {
                return event_content.TryGet<T>(innerPath, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Checks whether <paramref name="path"/> starts with <c>"event"</c> or
        /// <c>"event_content"</c> and, if so, returns the remainder after stripping that prefix.
        /// An exact match (no trailing dot) returns an empty string, meaning "the whole event".
        /// </summary>
        private static bool TryStripEventPrefix(string path, out string innerPath)
        {
            if (path.Equals("event", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("event_content", StringComparison.OrdinalIgnoreCase))
            {
                innerPath = string.Empty;
                return true;
            }

            if (path.StartsWith("event.", StringComparison.OrdinalIgnoreCase))
            {
                innerPath = path.Substring("event.".Length);
                return true;
            }

            if (path.StartsWith("event_content.", StringComparison.OrdinalIgnoreCase))
            {
                innerPath = path.Substring("event_content.".Length);
                return true;
            }

            innerPath = null;
            return false;
        }
    }
}
