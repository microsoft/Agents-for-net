// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Represents the inner <c>event</c> object from a Slack Events API callback payload.
/// Slack calls this the "event content"; the full payload is preserved as a
/// <see cref="JsonObject"/> so any field from any event type can be accessed via
/// <see cref="Get{T}"/> using the same snake_case names shown in the Slack docs.
/// See https://docs.slack.dev/reference/events
/// </summary>
[JsonConverter(typeof(EventContentConverter))]
public class EventContent
{
    internal readonly JsonObject _data;

    internal EventContent(JsonObject data)
    {
        _data = data ?? new JsonObject();
    }

    // ── Common event fields (https://docs.slack.dev/apis/events-api/#event-type-structure) ──

    /// <summary>Event type, e.g. "message", "reaction_added", "reaction_removed".</summary>
    public string type => Get<string>("type");

    /// <summary>Timestamp of when this event was fired.</summary>
    public string event_ts => Get<string>("event_ts");

    /// <summary>User ID of the person who triggered this event (not present on all events).</summary>
    public string user => Get<string>("user");

    /// <summary>Timestamp of the object this event describes (e.g. a message ts).</summary>
    public string ts => Get<string>("ts");

    /// <summary>Message subtype, if present (message events).</summary>
    public string subtype => Get<string>("subtype");

    /// <summary>Channel ID where the event occurred.</summary>
    public string channel => Get<string>("channel");

    /// <summary>Channel type, e.g. "im", "channel", "group" (message events).</summary>
    public string channel_type => Get<string>("channel_type");

    /// <summary>Team/workspace ID associated with the event.</summary>
    public string team => Get<string>("team");

    // ── message event fields ──

    /// <summary>The message body text (message events).</summary>
    public string text => Get<string>("text");

    /// <summary>Client-generated unique message ID (message events).</summary>
    public string client_msg_id => Get<string>("client_msg_id");

    // ── reaction_added / reaction_removed event fields ──

    /// <summary>Reaction name without colons, e.g. "raised_hands" (reaction events).</summary>
    public string reaction => Get<string>("reaction");

    /// <summary>User ID of the owner of the item that was reacted to (reaction events).</summary>
    public string item_user => Get<string>("item_user");

    /// <summary>
    /// Gets a value at the given dot-notation path within this event's data, using the
    /// same field names shown in the Slack documentation. Supports dot-separated property
    /// access and bracket array indexing.
    /// <example>
    /// <code>
    /// // reaction_removed event: access item sub-object
    /// string itemType = eventContent.GetValue&lt;string&gt;("item.type");
    /// string itemTs   = eventContent.GetValue&lt;string&gt;("item.ts");
    ///
    /// // message event: access first block type
    /// string blockType = eventContent.GetValue&lt;string&gt;("blocks[0].type");
    /// </code>
    /// </example>
    /// See https://docs.slack.dev/reference/events for event-specific field names.
    /// </summary>
    public T Get<T>(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            try { return _data.Deserialize<T>(); } catch { return default; }
        }

        if (!TryNavigate(_data, path, out var node)) return default;
        try { return node.Deserialize<T>(); } catch { return default; }
    }

    /// <summary>
    /// Tries to get a value at the given dot-notation path within this event's data.
    /// Returns <see langword="false"/> when the path does not exist or the value cannot
    /// be converted to <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(string path, out T value)
    {
        value = default;

        if (string.IsNullOrEmpty(path))
        {
            try { value = _data.Deserialize<T>(); return true; } catch { return false; }
        }

        if (!TryNavigate(_data, path, out var node)) return false;
        try { value = node.Deserialize<T>(); return true; } catch { return false; }
    }

    /// <summary>
    /// Navigates a <see cref="JsonNode"/> tree using a dot-path with optional bracket
    /// array-index notation. Property lookup is case-insensitive so paths match the
    /// lowercase snake_case names used in Slack documentation.
    /// Examples: "item.type", "blocks[0].elements[0].type"
    /// </summary>
    private static bool TryNavigate(JsonNode root, string path, out JsonNode result)
    {
        result = null;
        var current = root;
        var pos = 0;

        while (pos < path.Length && current != null)
        {
            // Skip separator dot (appears after a closing bracket: "array[0].next")
            if (path[pos] == '.') pos++;
            if (pos >= path.Length) break;

            if (path[pos] == '[')
            {
                // Array index bracket: "[0]"
                var close = path.IndexOf(']', pos + 1);
                if (close < 0) return false;

                if (!int.TryParse(path.AsSpan(pos + 1, close - pos - 1), out var idx)) return false;
                var ja = current as JsonArray;
                if (ja == null || idx < 0 || idx >= ja.Count) return false;
                current = ja[idx];
                pos = close + 1;
            }
            else
            {
                // Property name: read until '.', '[', or end of string
                var end = pos;
                while (end < path.Length && path[end] != '.' && path[end] != '[') end++;

                var key = path.Substring(pos, end - pos);
                if (current is JsonObject jobj)
                {
                    // Case-insensitive lookup so paths match Slack docs regardless of casing
                    current = null;
                    foreach (var kvp in jobj)
                    {
                        if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                        {
                            current = kvp.Value;
                            break;
                        }
                    }
                }
                else
                {
                    current = null;
                }

                pos = end;
            }
        }

        result = current;
        return current != null;
    }
}

/// <summary>
/// Deserializes the Slack <c>event</c> JSON object in its entirety into a
/// <see cref="JsonObject"/> so no field is lost regardless of event type.
/// </summary>
internal sealed class EventContentConverter : JsonConverter<EventContent>
{
    public override EventContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var data = JsonSerializer.Deserialize<JsonObject>(ref reader, options);
        return new EventContent(data);
    }

    public override void Write(Utf8JsonWriter writer, EventContent value, JsonSerializerOptions options)
    {
        (value?._data ?? new JsonObject()).WriteTo(writer);
    }
}

/// <summary>
/// Represents one authorization entry in a Slack Events API callback envelope.
/// See https://docs.slack.dev/apis/events-api/#callback-field
/// </summary>
public class SlackAuthorization
{
    /// <summary>Enterprise org ID, or null for non-Enterprise Grid installations.</summary>
    public string enterprise_id { get; set; }

    /// <summary>Workspace ID.</summary>
    public string team_id { get; set; }

    /// <summary>User ID that determines visibility scope for the installation.</summary>
    public string user_id { get; set; }

    /// <summary>Whether this authorization is for a bot user.</summary>
    public bool is_bot { get; set; }

    /// <summary>Whether this is an Enterprise Grid org-wide installation.</summary>
    public bool is_enterprise_install { get; set; }
}
