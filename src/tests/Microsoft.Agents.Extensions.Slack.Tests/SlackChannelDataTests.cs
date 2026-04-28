// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Slack.Api;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

/// <summary>
/// Tests for <see cref="SlackChannelData"/>, <see cref="EventEnvelope"/>,
/// and <see cref="EventContent"/> deserialization
/// and path-navigation helpers.
///
/// Test JSON payloads mirror the real Slack Events API examples shown at
/// https://docs.slack.dev/apis/events-api/#callback-field
/// </summary>
public class SlackChannelDataTests
{
    // ── JSON fixtures ──────────────────────────────────────────────────────────

    /// <summary>
    /// SlackChannelData wrapping a Slack "message" event callback.
    /// The envelope is keyed "SlackMessage" per <see cref="SlackChannelData"/>'s
    /// [JsonPropertyName("SlackMessage")] attribute.
    /// </summary>
    private const string MessageEventJson = """
        {
          "SlackMessage": {
            "token": "7K85CE7U1wjgFDUHafCiPB7l",
            "team_id": "T0AT0TZM9GD",
            "context_team_id": "T0AT0TZM9GD",
            "context_enterprise_id": null,
            "api_app_id": "A0AT4GSCQHG",
            "event": {
              "type": "message",
              "user": "U0ASNSMMY07",
              "ts": "1776271070.726439",
              "client_msg_id": "c9c2aa5e-03fd-48d6-8665-6680d91c8541",
              "text": "hi",
              "team": "T0AT0TZM9GD",
              "blocks": [
                {
                  "type": "rich_text",
                  "block_id": "a8bcU",
                  "elements": [
                    {
                      "type": "rich_text_section",
                      "elements": [
                        { "type": "text", "text": "hi" }
                      ]
                    }
                  ]
                }
              ],
              "channel": "D0AT8AL9LA0",
              "event_ts": "1776271070.726439",
              "channel_type": "im"
            },
            "type": "event_callback",
            "event_id": "Ev0AT2MA48S2",
            "event_time": 1776271070,
            "authorizations": [
              {
                "enterprise_id": null,
                "team_id": "T0AT0TZM9GD",
                "user_id": "U0AT1AL4C5T",
                "is_bot": true,
                "is_enterprise_install": false
              }
            ],
            "is_ext_shared_channel": false,
            "event_context": "4-eyJldCI6Im1lc3NhZ2UiLCJ0aWQiOiJUMEFUMFRaTTlHRCIsImFpZCI6IkEwQVQ0R1NDUUhHIiwiY2lkIjoiRDBBVDhBTDlMQTAifQ"
          },
          "ApiToken": "xoxb-test-message-token"
        }
        """;

    /// <summary>
    /// SlackChannelData wrapping a Slack "reaction_removed" event callback.
    /// This event type has a nested <c>item</c> object within the event content —
    /// a key test case for <see cref="EventContent.Get{T}"/> path navigation.
    /// </summary>
    private const string ReactionRemovedEventJson = """
        {
          "SlackMessage": {
            "token": "7K85CE7U1wjgFDUHafCiPB7l",
            "team_id": "T0AT0TZM9GD",
            "context_team_id": "T0AT0TZM9GD",
            "context_enterprise_id": null,
            "api_app_id": "A0AT4GSCQHG",
            "event": {
              "type": "reaction_removed",
              "user": "U0ASNSMMY07",
              "reaction": "raised_hands",
              "item": {
                "type": "message",
                "channel": "D0AT8AL9LA0",
                "ts": "1776373312.421509"
              },
              "item_user": "U0AT1AL4C5T",
              "event_ts": "1776373798.000200"
            },
            "type": "event_callback",
            "event_id": "Ev0AUASNK732",
            "event_time": 1776373798,
            "authorizations": [
              {
                "enterprise_id": null,
                "team_id": "T0AT0TZM9GD",
                "user_id": "U0AT1AL4C5T",
                "is_bot": true,
                "is_enterprise_install": false
              }
            ],
            "is_ext_shared_channel": false,
            "event_context": "4-eyJldCI6InJlYWN0aW9uX3JlbW92ZWQiLCJ0aWQiOiJUMEFUMFRaTTlHRCIsImFpZCI6IkEwQVQ0R1NDUUhHIiwiY2lkIjoiRDBBVDhBTDlMQTAifQ"
          },
          "ApiToken": "xoxb-test-reaction-token"
        }
        """;

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SlackChannelData Deserialize(string json)
        => JsonSerializer.Deserialize<SlackChannelData>(json);

    // ── SlackChannelData deserialization ───────────────────────────────────────

    [Fact]
    public void Deserialize_MessageEvent_ChannelData_NotNull()
    {
        var cd = Deserialize(MessageEventJson);
        Assert.NotNull(cd);
        Assert.NotNull(cd.Envelope);
    }

    [Fact]
    public void Deserialize_MessageEvent_ApiToken_Preserved()
    {
        var cd = Deserialize(MessageEventJson);
        Assert.Equal("xoxb-test-message-token", cd.ApiToken);
    }

    [Fact]
    public void Deserialize_MessageEvent_Properties_CatchesUnknownFields()
    {
        // Deserialize JSON that contains an unrecognised top-level field.
        var json = """
            {
              "SlackMessage": { "type": "event_callback", "event": { "type": "message" } },
              "ApiToken": "tok",
              "custom_field": "hello"
            }
            """;
        var cd = Deserialize(json);
        Assert.True(cd.Properties.ContainsKey("custom_field"));
        Assert.Equal("hello", cd.Properties["custom_field"].GetString());
    }

    // ── EventEnvelope fields (message event) ──────────────────────────────────

    [Fact]
    public void Deserialize_MessageEvent_Envelope_TopLevelFields()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("7K85CE7U1wjgFDUHafCiPB7l", envelope.token);
        Assert.Equal("T0AT0TZM9GD", envelope.team_id);
        Assert.Equal("T0AT0TZM9GD", envelope.context_team_id);
        Assert.Null(envelope.context_enterprise_id);
        Assert.Equal("A0AT4GSCQHG", envelope.api_app_id);
        Assert.Equal("event_callback", envelope.type);
        Assert.Equal("Ev0AT2MA48S2", envelope.event_id);
        Assert.Equal(1776271070L, envelope.event_time);
        Assert.False(envelope.is_ext_shared_channel);
        Assert.Equal(
            "4-eyJldCI6Im1lc3NhZ2UiLCJ0aWQiOiJUMEFUMFRaTTlHRCIsImFpZCI6IkEwQVQ0R1NDUUhHIiwiY2lkIjoiRDBBVDhBTDlMQTAifQ",
            envelope.event_context);
    }

    [Fact]
    public void Deserialize_MessageEvent_Envelope_EventContentNotNull()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.NotNull(envelope.event_content);
    }

    // ── SlackAuthorization ─────────────────────────────────────────────────────

    [Fact]
    public void Deserialize_MessageEvent_Authorizations_Count()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.NotNull(envelope.authorizations);
        Assert.Single(envelope.Get<JsonArray>("authorizations"));
    }

    [Fact]
    public void Deserialize_MessageEvent_Authorization_Fields()
    {
        var auth = Deserialize(MessageEventJson).Envelope.Get<List<JsonObject>>("authorizations")[0];

        Assert.Null(auth["enterprise_id"]);
        Assert.Equal("T0AT0TZM9GD", auth["team_id"].ToString());
        Assert.Equal("U0AT1AL4C5T", auth["user_id"].ToString());
        Assert.True((bool)auth["is_bot"]);
        Assert.False((bool)auth["is_enterprise_install"]);
    }

    // ── EventContent named properties — message event ─────────────────────────

    [Fact]
    public void Deserialize_MessageEvent_Content_NamedProperties()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.Equal("message", content.type);
        Assert.Equal("U0ASNSMMY07", content.user);
        Assert.Equal("1776271070.726439", content.ts);
        Assert.Equal("1776271070.726439", content.event_ts);
        Assert.Equal("c9c2aa5e-03fd-48d6-8665-6680d91c8541", content.client_msg_id);
        Assert.Equal("hi", content.text);
        Assert.Equal("T0AT0TZM9GD", content.team);
        Assert.Equal("D0AT8AL9LA0", content.channel);
        Assert.Equal("im", content.channel_type);
        Assert.Null(content.subtype);
        Assert.Null(content.reaction);
        Assert.Null(content.item_user);
    }

    // ── EventContent named properties — reaction_removed event ────────────────

    [Fact]
    public void Deserialize_ReactionRemoved_Content_NamedProperties()
    {
        var content = Deserialize(ReactionRemovedEventJson).Envelope.event_content;

        Assert.Equal("reaction_removed", content.type);
        Assert.Equal("U0ASNSMMY07", content.user);
        Assert.Equal("raised_hands", content.reaction);
        Assert.Equal("U0AT1AL4C5T", content.item_user);
        Assert.Equal("1776373798.000200", content.event_ts);
        Assert.Null(content.channel);
        Assert.Null(content.text);
    }

    // ── EventContent.Get — simple path ───────────────────────────────────

    [Fact]
    public void EventContent_Get_SimpleProperty()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.Equal("message", content.Get<string>("type"));
        Assert.Equal("hi", content.Get<string>("text"));
        Assert.Equal("D0AT8AL9LA0", content.Get<string>("channel"));
    }

    [Fact]
    public void EventContent_Get_MissingPath_ReturnsDefault()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.Null(content.Get<string>("nonexistent_field"));
        Assert.Equal(0, content.Get<int>("nonexistent_field"));
    }

    // ── EventContent.Get — nested object path ────────────────────────────

    [Fact]
    public void EventContent_Get_NestedObject_ReactionRemovedItem()
    {
        // reaction_removed carries an "item" sub-object in the event JSON
        var content = Deserialize(ReactionRemovedEventJson).Envelope.event_content;

        Assert.Equal("message", content.Get<string>("item.type"));
        Assert.Equal("D0AT8AL9LA0", content.Get<string>("item.channel"));
        Assert.Equal("1776373312.421509", content.Get<string>("item.ts"));
    }

    [Fact]
    public void EventContent_Get_NestedObject_MissingSegment_ReturnsDefault()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        // "item" does not exist on a message event
        Assert.Null(content.Get<string>("item.type"));
    }

    // ── EventContent.Get — array indexing ────────────────────────────────

    [Fact]
    public void EventContent_Get_ArrayIndex_BlockType()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.Equal("rich_text", content.Get<string>("blocks[0].type"));
        Assert.Equal("a8bcU", content.Get<string>("blocks[0].block_id"));
    }

    [Fact]
    public void EventContent_Get_ArrayIndex_DeepNesting()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        // blocks[0].elements[0].type == "rich_text_section"
        Assert.Equal("rich_text_section", content.Get<string>("blocks[0].elements[0].type"));

        // blocks[0].elements[0].elements[0].text == "hi"
        Assert.Equal("hi", content.Get<string>("blocks[0].elements[0].elements[0].text"));
    }

    [Fact]
    public void EventContent_Get_ArrayIndex_OutOfRange_ReturnsDefault()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.Null(content.Get<string>("blocks[99].type"));
    }

    // ── EventContent.TryGet ──────────────────────────────────────────────

    [Fact]
    public void EventContent_TryGet_ExistingPath_ReturnsTrue()
    {
        var content = Deserialize(ReactionRemovedEventJson).Envelope.event_content;

        Assert.True(content.TryGet<string>("item.type", out var itemType));
        Assert.Equal("message", itemType);
    }

    [Fact]
    public void EventContent_TryGet_MissingPath_ReturnsFalse()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.False(content.TryGet<string>("item.type", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void EventContent_TryGet_ArrayPath_ReturnsTrue()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        Assert.True(content.TryGet<string>("blocks[0].type", out var blockType));
        Assert.Equal("rich_text", blockType);
    }

    // ── EventEnvelope.Get — "event." prefix (matches Slack JSON field name) ──

    [Fact]
    public void EventEnvelope_Get_EventDotPrefix_SimpleProperty()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("message", envelope.Get<string>("event.type"));
        Assert.Equal("hi", envelope.Get<string>("event.text"));
    }

    [Fact]
    public void EventEnvelope_Get_EventDotPrefix_NestedObject()
    {
        var envelope = Deserialize(ReactionRemovedEventJson).Envelope;

        Assert.Equal("message", envelope.Get<string>("event.item.type"));
        Assert.Equal("D0AT8AL9LA0", envelope.Get<string>("event.item.channel"));
    }

    [Fact]
    public void EventEnvelope_Get_EventDotPrefix_ArrayPath()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("rich_text", envelope.Get<string>("event.blocks[0].type"));
    }

    // ── EventEnvelope.Get — "event_content." prefix (C# property name) ──

    [Fact]
    public void EventEnvelope_Get_EventContentDotPrefix_SimpleProperty()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("message", envelope.Get<string>("event_content.type"));
        Assert.Equal("hi", envelope.Get<string>("event_content.text"));
    }

    [Fact]
    public void EventEnvelope_Get_EventContentDotPrefix_NestedObject()
    {
        var envelope = Deserialize(ReactionRemovedEventJson).Envelope;

        Assert.Equal("message", envelope.Get<string>("event_content.item.type"));
        Assert.Equal("D0AT8AL9LA0", envelope.Get<string>("event_content.item.channel"));
    }

    [Fact]
    public void EventEnvelope_Get_EventContentDotPrefix_ArrayPath()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("rich_text", envelope.Get<string>("event_content.blocks[0].type"));
    }

    [Fact]
    public void EventEnvelope_Get_BothPrefixes_ProduceSameResult()
    {
        // "event." and "event_content." must be interchangeable
        var envelope = Deserialize(ReactionRemovedEventJson).Envelope;

        Assert.Equal(
            envelope.Get<string>("event.item.type"),
            envelope.Get<string>("event_content.item.type"));

        Assert.Equal(
            envelope.Get<string>("event.reaction"),
            envelope.Get<string>("event_content.reaction"));
    }

    // ── EventEnvelope.Get — bare "event" / "event_content" ─────────────

    [Fact]
    public void EventEnvelope_Get_BareEventKey_ReturnsDeserializedContent()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        // Get<EventContent>("event") should round-trip back to EventContent
        var content = envelope.Get<EventContent>("event");
        Assert.NotNull(content);
        Assert.Equal("message", content.type);
    }

    [Fact]
    public void EventEnvelope_Get_BareEventContentKey_ReturnsDeserializedContent()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        var content = envelope.Get<EventContent>("event_content");
        Assert.NotNull(content);
        Assert.Equal("message", content.type);
    }

    // ── EventEnvelope.TryGet ─────────────────────────────────────────────

    [Fact]
    public void EventEnvelope_TryGet_ExistingPath_ReturnsTrue()
    {
        var envelope = Deserialize(ReactionRemovedEventJson).Envelope;

        Assert.True(envelope.TryGet<string>("event.item.type", out var itemType));
        Assert.Equal("message", itemType);
    }

    [Fact]
    public void EventEnvelope_TryGet_MissingPath_ReturnsFalse()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.False(envelope.TryGet<string>("event.item.type", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void EventEnvelope_TryGet_TrulyMissingPath_ReturnsFalse()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.False(envelope.TryGet<string>("nonexistent_field", out var value));
        Assert.Null(value);
    }

    // ── Case-insensitivity of path navigation ────────────────────────────────

    [Fact]
    public void EventContent_Get_PathIsCaseInsensitive()
    {
        var content = Deserialize(MessageEventJson).Envelope.event_content;

        // All three casings of "type" should resolve to the same value
        Assert.Equal(content.Get<string>("type"), content.Get<string>("TYPE"));
        Assert.Equal(content.Get<string>("type"), content.Get<string>("Type"));
    }

    [Fact]
    public void EventEnvelope_Get_PrefixIsCaseInsensitive()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;

        Assert.Equal("message", envelope.Get<string>("EVENT.type"));
        Assert.Equal("message", envelope.Get<string>("Event.type"));
        Assert.Equal("message", envelope.Get<string>("EVENT_CONTENT.type"));
    }

    // ── EventEnvelope.Get — root property access ─────────────────────────────

    [Fact]
    public void EventEnvelope_Get_RootProperty_TeamId()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.Equal("T0AT0TZM9GD", envelope.Get<string>("team_id"));
    }

    [Fact]
    public void EventEnvelope_Get_RootProperty_Token()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.Equal("7K85CE7U1wjgFDUHafCiPB7l", envelope.Get<string>("token"));
    }

    [Fact]
    public void EventEnvelope_Get_RootProperty_EventTime()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.Equal(1776271070L, envelope.Get<long>("event_time"));
    }

    [Fact]
    public void EventEnvelope_Get_RootProperty_Missing_ReturnsDefault()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.Null(envelope.Get<string>("nonexistent_field"));
    }

    // ── EventEnvelope.Get — Properties (extension data) fallback ─────────────

    [Fact]
    public void EventEnvelope_Get_ExtensionData_Field()
    {
        var json = """
            {
              "SlackMessage": {
                "type": "event_callback",
                "event": { "type": "message" },
                "custom_envelope_field": "custom_value"
              },
              "ApiToken": "tok"
            }
            """;
        var envelope = Deserialize(json).Envelope;
        Assert.Equal("custom_value", envelope.Get<string>("custom_envelope_field"));
    }

    [Fact]
    public void EventEnvelope_TryGet_RootProperty_ReturnsTrue()
    {
        var envelope = Deserialize(MessageEventJson).Envelope;
        Assert.True(envelope.TryGet<string>("team_id", out var teamId));
        Assert.Equal("T0AT0TZM9GD", teamId);
    }

    // ── Serialization round-trip ──────────────────────────────────────────────

    [Fact]
    public void SlackChannelData_RoundTrip_PreservesEventContent()
    {
        var original = Deserialize(MessageEventJson);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SlackChannelData>(json);

        Assert.Equal(original.ApiToken, restored.ApiToken);
        Assert.Equal(original.Envelope.team_id, restored.Envelope.team_id);
        Assert.Equal(original.Envelope.event_content.type, restored.Envelope.event_content.type);
        Assert.Equal(original.Envelope.event_content.text, restored.Envelope.event_content.text);
        Assert.Equal(original.Envelope.event_content.channel, restored.Envelope.event_content.channel);
    }

    [Fact]
    public void SlackChannelData_RoundTrip_PreservesNestedEventContent()
    {
        // Verify that the EventContentConverter Write method correctly re-emits
        // the full JsonObject so nested data survives the round-trip.
        var original = Deserialize(ReactionRemovedEventJson);
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SlackChannelData>(json);

        Assert.Equal(
            original.Envelope.Get<string>("event.item.type"),
            restored.Envelope.Get<string>("event.item.type"));

        Assert.Equal(
            original.Envelope.Get<string>("event.item.channel"),
            restored.Envelope.Get<string>("event.item.channel"));
    }
}
