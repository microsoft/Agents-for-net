// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.IO;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

/// <summary>
/// Tests for the polymorphic resolution of inbound Activities to <see cref="Microsoft.Agents.Extensions.Slack.SlackActivity"/>.
/// Resolution is driven by the <c>[ActivityType(ChannelId = "slack")]</c> annotation on
/// <see cref="Microsoft.Agents.Extensions.Slack.SlackActivity"/>, which the source generator turns into an assembly-level
/// <see cref="Microsoft.Agents.Core.Serialization.ActivityTypeInitAssemblyAttribute"/> that auto-registers the type at load time.
/// Any Activity whose channelId is "slack" therefore deserializes to <see cref="Microsoft.Agents.Extensions.Slack.SlackActivity"/>,
/// exposing a strongly-typed <see cref="Microsoft.Agents.Extensions.Slack.Api.SlackChannelData"/> via the
/// <see cref="Microsoft.Agents.Extensions.Slack.SlackActivity.ChannelData"/> shadow.
/// </summary>
public class SlackActivityTests
{
    internal static string FeedbackActivityJson => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "feedback-submit-action.json"));

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
    public void FeedbackActivity_ResolvesToSlackActivity()
    {
        var activity = ProtocolJsonSerializer.ToObject<Activity>(FeedbackActivityJson);

        var slack = Assert.IsType<SlackActivity>(activity);
        Assert.Equal(ActivityTypes.Invoke, slack.Type);
        Assert.Equal("message/submitAction", slack.Name);
    }

    [Fact]
    public void FeedbackActivity_ExposesTypedChannelData()
    {
        var slack = Assert.IsType<SlackActivity>(ProtocolJsonSerializer.ToObject<Activity>(FeedbackActivityJson));

        Assert.Equal("D0LOCALDEV01", slack.ChannelData.Channel);
        Assert.Equal("1700000000.000000", slack.ChannelData.ThreadTs);
        Assert.Equal("D0LOCALDEV01", slack.ChannelData.Payload.channel);
        Assert.Equal("D0LOCALDEV01", slack.ChannelData.Payload.Get<string>("container.channel_id"));
        Assert.Equal("feedback_buttons", slack.ChannelData.Payload.Get<string>("actions[0].type"));
        Assert.Equal("A0LOCALDEV01", slack.ChannelData.Payload.AdditionalProperties["api_app_id"].GetString());
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
