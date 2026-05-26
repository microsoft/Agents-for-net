// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.Slack.Api;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackResponseTests
{
    // ── JSON fixtures ──────────────────────────────────────────────────────────

    /// <summary>Typical chat.postMessage success response.</summary>
    private const string PostMessageJson = """
        {
          "ok": true,
          "ts": "1776271070.726439",
          "channel": "D0AT8AL9LA0",
          "message": {
            "type": "message",
            "text": "hello",
            "ts": "1776271070.726439",
            "bot_id": "B0AT1AL4C5T"
          }
        }
        """;

    /// <summary>Error response from Slack.</summary>
    private const string ErrorJson = """
        {
          "ok": false,
          "error": "channel_not_found",
          "warning": "missing_charset"
        }
        """;

    /// <summary>Error response with a "detail" extension field.</summary>
    private const string ErrorWithDetailJson = """
        {
          "ok": false,
          "error": "channel_not_found",
          "detail": "Invalid channel_id"
        }
        """;

    /// <summary>Minimal ok response with a boolean extension field.</summary>
    private const string AiSearchJson = """
        {
          "ok": true,
          "is_ai_search_enabled": true
        }
        """;

    /// <summary>chat.postMessage response with a nested attachments array.</summary>
    private const string PostMessageWithAttachmentsJson = """
        {
          "ok": true,
          "channel": "C123ABC456",
          "ts": "1503435956.000247",
          "message": {
            "text": "Here's a message for you",
            "username": "ecto1",
            "bot_id": "B123ABC456",
            "attachments": [
              {
                "text": "This is an attachment",
                "id": 1,
                "fallback": "This is an attachment's fallback"
              }
            ],
            "type": "message",
            "subtype": "bot_message",
            "ts": "1503435956.000247"
          }
        }
        """;

    /// <summary>conversations.info response where "channel" is an object, not a string.</summary>
    private const string ConversationsInfoJson = """
        {
          "ok": true,
          "channel": {
            "id": "C0EAQDV4Z",
            "name": "endeavor",
            "is_channel": true,
            "is_group": false,
            "is_im": false,
            "created": 1504554479,
            "creator": "U0123456",
            "is_archived": false,
            "is_general": false,
            "unlinked": 0,
            "name_normalized": "endeavor",
            "is_shared": false,
            "is_ext_shared": false,
            "is_org_shared": false,
            "pending_shared": [],
            "is_pending_ext_shared": false,
            "is_member": true,
            "is_private": false,
            "is_mpim": false,
            "last_read": "0000000000.000000",
            "latest": null,
            "unread_count": 0,
            "unread_count_display": 0,
            "topic": {
              "value": "",
              "creator": "",
              "last_set": 0
            },
            "properties": {
              "canvas": {
                "file_id": "F123ABC456",
                "is_empty": true,
                "quip_thread_id": "JAB1CDefGhI"
              }
            },
            "purpose": {
              "value": "",
              "creator": "",
              "last_set": 0
            },
            "previous_names": [],
            "priority": 0
          }
        }
        """;

    private static SlackResponse Deserialize(string json)
        => JsonSerializer.Deserialize<SlackResponse>(json);

    // ── Named property deserialization ────────────────────────────────────────

    [Fact]
    public void Deserialize_Success_NamedProperties()
    {
        var r = Deserialize(PostMessageJson);

        Assert.True(r.ok);
        Assert.Equal("1776271070.726439", r.ts);
        Assert.Null(r.error);
        Assert.Null(r.warning);
    }

    [Fact]
    public void Deserialize_Error_NamedProperties()
    {
        var r = Deserialize(ErrorJson);

        Assert.False(r.ok);
        Assert.Equal("channel_not_found", r.error);
        Assert.Equal("missing_charset", r.warning);
        Assert.Null(r.ts);
    }

    // ── Extension data (Properties) ───────────────────────────────────────────

    [Fact]
    public void Deserialize_Success_ExtensionData_ContainsUnmodeledFields()
    {
        var r = Deserialize(PostMessageJson);

        Assert.NotNull(r.AdditionalProperties);
        Assert.True(r.AdditionalProperties.ContainsKey("channel"));
        Assert.Equal("D0AT8AL9LA0", r.AdditionalProperties["channel"].GetString());
    }

    // ── Get — named top-level fields ──────────────────────────────────────────

    [Fact]
    public void Get_TopLevel_Ok()
    {
        var r = Deserialize(PostMessageJson);
        Assert.True(r.Get<bool>("ok"));
    }

    [Fact]
    public void Get_TopLevel_Ts()
    {
        var r = Deserialize(PostMessageJson);
        Assert.Equal("1776271070.726439", r.Get<string>("ts"));
    }

    // ── Get — extension data fields ───────────────────────────────────────────

    [Fact]
    public void Get_ExtensionData_Channel()
    {
        var r = Deserialize(PostMessageJson);
        Assert.Equal("D0AT8AL9LA0", r.Get<string>("channel"));
    }

    // ── Get — nested object path ──────────────────────────────────────────────

    [Fact]
    public void Get_NestedObject_MessageText()
    {
        var r = Deserialize(PostMessageJson);
        Assert.Equal("message", r.Get<string>("message.type"));
        Assert.Equal("hello",   r.Get<string>("message.text"));
        Assert.Equal("B0AT1AL4C5T", r.Get<string>("message.bot_id"));
    }

    [Fact]
    public void Get_NestedObject_MessageTs()
    {
        // "ts" exists both at root and inside "message" — each path should return its own value
        var r = Deserialize(PostMessageJson);
        Assert.Equal("1776271070.726439", r.Get<string>("ts"));
        Assert.Equal("1776271070.726439", r.Get<string>("message.ts"));
    }

    // ── Get — missing path returns default ───────────────────────────────────

    [Fact]
    public void Get_MissingPath_ReturnsDefault()
    {
        var r = Deserialize(PostMessageJson);
        Assert.Null(r.Get<string>("nonexistent"));
        Assert.Null(r.Get<string>("message.nonexistent"));
    }

    // ── TryGet ────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ExistingPath_ReturnsTrue()
    {
        var r = Deserialize(PostMessageJson);

        Assert.True(r.TryGet<string>("channel", out var channel));
        Assert.Equal("D0AT8AL9LA0", channel);

        Assert.True(r.TryGet<string>("message.text", out var text));
        Assert.Equal("hello", text);
    }

    [Fact]
    public void TryGet_MissingPath_ReturnsFalse()
    {
        var r = Deserialize(PostMessageJson);

        Assert.False(r.TryGet<string>("nonexistent", out var value));
        Assert.Null(value);
    }

    // ── Error response with "detail" extension field ─────────────────────────

    [Fact]
    public void Get_Error_Detail_ExtensionString()
    {
        var r = Deserialize(ErrorWithDetailJson);

        Assert.False(r.ok);
        Assert.Equal("channel_not_found", r.error);
        Assert.Equal("Invalid channel_id", r.Get<string>("detail"));
    }

    [Fact]
    public void TryGet_Error_Detail_ReturnsTrue()
    {
        var r = Deserialize(ErrorWithDetailJson);

        Assert.True(r.TryGet<string>("detail", out var detail));
        Assert.Equal("Invalid channel_id", detail);
    }

    // ── Boolean extension data field ──────────────────────────────────────────

    [Fact]
    public void Get_ExtensionData_BoolField()
    {
        var r = Deserialize(AiSearchJson);

        Assert.True(r.ok);
        Assert.True(r.Get<bool>("is_ai_search_enabled"));
    }

    [Fact]
    public void TryGet_ExtensionData_BoolField_ReturnsTrue()
    {
        var r = Deserialize(AiSearchJson);

        Assert.True(r.TryGet<bool>("is_ai_search_enabled", out var enabled));
        Assert.True(enabled);
    }

    // ── Attachment array indexing ─────────────────────────────────────────────

    [Fact]
    public void Get_Attachments_ByIndex_StringFields()
    {
        var r = Deserialize(PostMessageWithAttachmentsJson);

        Assert.Equal("This is an attachment",              r.Get<string>("message.attachments[0].text"));
        Assert.Equal("This is an attachment's fallback",  r.Get<string>("message.attachments[0].fallback"));
    }

    [Fact]
    public void Get_Attachments_ByIndex_IntField()
    {
        var r = Deserialize(PostMessageWithAttachmentsJson);

        Assert.Equal(1, r.Get<int>("message.attachments[0].id"));
    }

    [Fact]
    public void Get_Attachments_OutOfRange_ReturnsDefault()
    {
        var r = Deserialize(PostMessageWithAttachmentsJson);

        Assert.Null(r.Get<string>("message.attachments[99].text"));
    }

    // ── Channel-as-object (conversations.info) ────────────────────────────────

    [Fact]
    public void Get_ChannelObject_TopLevelFields()
    {
        var r = Deserialize(ConversationsInfoJson);

        Assert.Equal("C0EAQDV4Z", r.Get<string>("channel.id"));
        Assert.Equal("endeavor",  r.Get<string>("channel.name"));
    }

    [Fact]
    public void Get_ChannelObject_BoolField()
    {
        var r = Deserialize(ConversationsInfoJson);

        Assert.True(r.Get<bool>("channel.is_channel"));
        Assert.False(r.Get<bool>("channel.is_group"));
        Assert.False(r.Get<bool>("channel.is_private"));
    }

    [Fact]
    public void Get_ChannelObject_IntField()
    {
        var r = Deserialize(ConversationsInfoJson);

        Assert.Equal(1504554479, r.Get<int>("channel.created"));
        Assert.Equal(0, r.Get<int>("channel.unread_count"));
    }

    [Fact]
    public void Get_ChannelObject_DeepNested_TopicValue()
    {
        var r = Deserialize(ConversationsInfoJson);

        // topic.value is an empty string — path should resolve, not return null
        Assert.True(r.TryGet<string>("channel.topic.value", out var topicValue));
        Assert.Equal(string.Empty, topicValue);
    }

    [Fact]
    public void Get_ChannelObject_VeryDeep_CanvasFileId()
    {
        var r = Deserialize(ConversationsInfoJson);

        Assert.Equal("F123ABC456",    r.Get<string>("channel.properties.canvas.file_id"));
        Assert.Equal("JAB1CDefGhI",   r.Get<string>("channel.properties.canvas.quip_thread_id"));
        Assert.True(r.Get<bool>("channel.properties.canvas.is_empty"));
    }

    [Fact]
    public void Get_ChannelObject_WhenChannelIsObject_StringGetReturnsNull()
    {
        // "channel" is a JSON object here, not a string — Get<string> should return null gracefully
        var r = Deserialize(ConversationsInfoJson);

        Assert.Null(r.Get<string>("channel"));
    }

    // ── Serialization round-trip ──────────────────────────────────────────────

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = Deserialize(PostMessageJson);
        var json = JsonSerializer.Serialize(original);
        var restored = Deserialize(json);

        Assert.Equal(original.ok,      restored.ok);
        Assert.Equal(original.ts,       restored.ts);
        Assert.Equal(original.Get<string>("channel"),      restored.Get<string>("channel"));
        Assert.Equal(original.Get<string>("message.text"), restored.Get<string>("message.text"));
    }
}
