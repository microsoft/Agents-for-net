// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackActivityConverterTests
{
    private readonly SlackActivityConverter _converter = new(new SlackAdapterOptions
    {
        BotToken = "xoxb-test-token",
        BotId = "B123",
        BotUserId = "U123",
    });

    [Fact]
    public void Convert_Event_UsesAgentAttributeName()
    {
        var request = ParseEvent("""
            {"type":"event_callback","team_id":"T1","event_id":"Ev1","event":{"type":"message","channel":"C1","text":"hello","ts":"1.0","user":"U1","channel_type":"channel"}}
            """);

        var activity = _converter.Convert(request, typeof(NamedAgent));

        Assert.Equal("Configured Slack Agent", activity!.Recipient.Name);
    }

    [Fact]
    public void Convert_Interactive_UsesAgentClassNameFallback()
    {
        var request = ParseInteractive("""
            {"type":"view_submission","team":{"id":"T1"},"channel":{"id":"C1"},"user":{"id":"U1"}}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal(nameof(FallbackAgent), activity!.Recipient.Name);
    }

    [Fact]
    public void Convert_BotMessage_ReturnsNull()
    {
        var request = ParseEvent("""
            {"type":"event_callback","team_id":"T1","event_id":"Ev1","event":{"type":"message","channel":"C1","text":"hello","ts":"1.0","user":"U123","channel_type":"channel"}}
            """);

        Assert.Null(_converter.Convert(request, typeof(FallbackAgent)));
    }

    [Fact]
    public void Convert_Event_UsesContextTeamIdFallback()
    {
        var request = ParseEvent("""
            {"type":"event_callback","context_team_id":"T2","event_id":"Ev1","event":{"type":"message","channel":"C1","text":"hello","ts":"1.0","user":"U1","channel_type":"channel"}}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal("U1:T2", activity!.From.Id);
        Assert.Equal("B123:T2:C1", activity.Conversation.Id);
    }

    [Fact]
    public void Convert_Interactive_UsesUserTeamIdFallback()
    {
        var request = ParseInteractive("""
            {"type":"view_submission","team":null,"channel":{"id":"C1"},"user":{"id":"U1","team_id":"T2"}}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal("U1:T2", activity!.From.Id);
        Assert.Equal("B123:T2:C1", activity.Conversation.Id);
    }

    [Fact]
    public void Convert_FeedbackButtons_CreatesFeedbackInvoke()
    {
        var request = ParseInteractive("""
            {"type":"block_actions","team":{"id":"T1"},"channel":{"id":"C1"},"user":{"id":"U1"},"message":{"ts":"2.0","thread_ts":"1.0"},"actions":[{"action_id":"feedback","type":"feedback_buttons","value":"positive_feedback"}]}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal(ActivityTypes.Invoke, activity!.Type);
        Assert.Equal("message/submitAction", activity.Name);
        Assert.Equal("1.0", activity.ReplyToId);
        var value = ProtocolJsonSerializer.ToObject<JsonObject>(activity.Value);
        Assert.Equal("feedback", value!["actionName"]!.GetValue<string>());
        Assert.Equal("positive_feedback", value["actionValue"]!["reaction"]!.GetValue<string>());
    }

    [Fact]
    public void Convert_MessageAction_CreatesSlackActivityEvent()
    {
        var request = ParseInteractive("""
            {"type":"message_action","callback_id":"create_ticket","team":{"id":"T1"},"channel":{"id":"C1"},"user":{"id":"U1"}}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal(ActivityTypes.Event, activity!.Type);
        Assert.Equal("SlackActivity", activity.Name);
        Assert.Equal("create_ticket", activity.Value);
    }

    [Theory]
    [InlineData("""{"type":"select","selected_options":[{"value":"choice &amp; more"}]}""", "choice & more", "Configured Slack Agent")]
    [InlineData("""{"type":"button","value":"go &lt;now&gt;"}""", "go <now>", "FallbackAgent")]
    public void Convert_SelectOrButton_CreatesMessageWithRecipientMention(
        string action,
        string expectedText,
        string expectedAgentName)
    {
        var agentType = action.Contains("\"select\"") ? typeof(NamedAgent) : typeof(FallbackAgent);
        var request = ParseInteractive(
            $$"""{"type":"block_actions","team":{"id":"T1"},"channel":{"id":"C1"},"user":{"id":"U1"},"actions":[{{action}}]}""");

        var activity = _converter.Convert(request, agentType);

        Assert.Equal(ActivityTypes.Message, activity!.Type);
        Assert.Equal(expectedText, activity.Text);
        Assert.Equal(expectedAgentName, activity.Recipient.Name);
        var mention = Assert.IsType<Mention>(Assert.Single(activity.Entities));
        Assert.Same(activity.Recipient, mention.Mentioned);
        Assert.Equal($"@{activity.Recipient.Name}", mention.Text);
    }

    [Fact]
    public void Convert_UnknownPayload_PreservesVendorPayload()
    {
        var request = ParseInteractive("""
            {"type":"future_action","team":{"id":"T1"},"channel":{"id":"C1","name":"general"},"user":{"id":"U1"},"future_field":{"version":2}}
            """);

        var activity = _converter.Convert(request, typeof(FallbackAgent));

        Assert.Equal("vnd.slack.action.future_action", activity!.Name);
        var value = Assert.IsType<JsonObject>(activity.Value);
        Assert.Equal("general", value["channel"]!["name"]!.GetValue<string>());
        Assert.Equal(2, value["future_field"]!["version"]!.GetValue<int>());
        Assert.Equal(2, activity.ChannelData.Payload.Get<int>("future_field.version"));
    }

    private static ParsedSlackRequest ParseEvent(string payload)
        => new SlackRequestParser().Parse(payload, "application/json");

    private static ParsedSlackRequest ParseInteractive(string payload)
        => new SlackRequestParser().Parse(
            $"payload={WebUtility.UrlEncode(payload)}",
            "application/x-www-form-urlencoded");

    [Agent("Configured Slack Agent")]
    private sealed class NamedAgent : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FallbackAgent : IAgent
    {
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
