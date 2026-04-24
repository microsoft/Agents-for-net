// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Represents the inner <c>event</c> object from a Slack Events API callback payload.
/// Slack calls this the "event content"; the full payload is preserved as a
/// <see cref="JsonObject"/> so any field from any event type can be accessed via
/// <see cref="SlackModel.Get{T}"/> using the same snake_case names shown in the Slack docs.
/// See https://docs.slack.dev/reference/events
/// </summary>
[JsonConverter(typeof(EventContentConverter))]
public class EventContent : SlackModel
{
    internal readonly JsonObject _data;

    internal EventContent(JsonObject data)
    {
        _data = data ?? new JsonObject();
    }

    /// <inheritdoc/>
    protected override JsonObject GetData() => _data;

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

    /// <summary>Catch-all for any envelope fields not explicitly modelled above.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement> AdditionalProperties { get; set; } = new Dictionary<string, JsonElement>();
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
