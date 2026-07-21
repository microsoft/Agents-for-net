// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

/// <summary>
/// Tests for the polymorphic resolution of inbound Activities to <see cref="SlackActivity"/>.
/// The resolver (registered in <c>Serialization.SerializationInit.Init</c>) maps any Activity whose
/// channelId is "slack" to <see cref="SlackActivity"/>, exposing a strongly-typed
/// <see cref="Api.SlackChannelData"/> via the <see cref="SlackActivity.ChannelData"/> shadow.
/// </summary>
public class SlackActivityTests
{
    private const string SlackMessageJson = """
        {
          "type": "message",
          "channelId": "slack",
          "channelData": {
            "SlackMessage": {
              "event": {
                "type": "message",
                "channel": "C0AT123",
                "text": "hello",
                "ts": "1776271070.726439"
              }
            },
            "ApiToken": "xoxb-token"
          }
        }
        """;

    [Fact]
    public void SlackChannelId_ResolvesToSlackActivity()
    {
        var activity = ProtocolJsonSerializer.ToObject<Activity>(SlackMessageJson);

        var slack = Assert.IsType<SlackActivity>(activity);
        Assert.Equal(Channels.Slack, slack.ChannelId.Channel);
    }

    [Fact]
    public void SlackActivity_ExposesTypedChannelData()
    {
        var slack = Assert.IsType<SlackActivity>(ProtocolJsonSerializer.ToObject<Activity>(SlackMessageJson));

        var channelData = slack.ChannelData;
        Assert.NotNull(channelData);
        Assert.Equal("C0AT123", channelData.Channel);
        Assert.Equal("xoxb-token", channelData.ApiToken);
        Assert.Equal("hello", channelData.Envelope.Get<string>("event.text"));
    }

    [Fact]
    public void TypedChannelData_StaysInSyncWithBaseChannelData()
    {
        var slack = Assert.IsType<SlackActivity>(ProtocolJsonSerializer.ToObject<Activity>(SlackMessageJson));

        // The typed shadow reads through the base (loosely-typed) ChannelData that the deserializer
        // populated, so the base view and the GetChannelData<T>() extension agree.
        var viaExtension = ((IActivity)slack).GetChannelData<Api.SlackChannelData>();
        Assert.Equal(slack.ChannelData.Channel, viaExtension.Channel);
    }

    [Fact]
    public void NonSlackChannelId_ResolvesToBaseActivity()
    {
        var activity = ProtocolJsonSerializer.ToObject<Activity>(
            """{"type":"message","channelId":"test","text":"hi"}""");

        Assert.Equal(typeof(Activity), activity.GetType());
    }

    [Fact]
    public void SlackActivity_RoundTripsChannelData()
    {
        var slack = Assert.IsType<SlackActivity>(ProtocolJsonSerializer.ToObject<Activity>(SlackMessageJson));

        var json = ProtocolJsonSerializer.ToJson(slack);
        var roundTripped = Assert.IsType<SlackActivity>(ProtocolJsonSerializer.ToObject<Activity>(json));

        Assert.Equal("C0AT123", roundTripped.ChannelData.Channel);
        Assert.Equal("xoxb-token", roundTripped.ChannelData.ApiToken);
    }
}
