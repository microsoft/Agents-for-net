// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing.Http;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest;

public class SlackHttpSimulationTests
{
    private const string PostMessageUrl = "https://slack.com/api/chat.postMessage";

    [Fact]
    public async Task MessageEscapesUserTextAndPreservesThread()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, PostMessageUrl)
            .ForClient("SlackApi")
            .WithHeader("Authorization", "Bearer synthetic-slack-token")
            .WithJsonBody(new { channel = "C123", thread_ts = "1700000000.000001", text = "You said: \"hello\" from C:\\files\nnext line" })
            .RespondJson(new { ok = true, channel = "C123", ts = "1700000001.000001" });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send(SlackActivity("\"hello\" from C:\\files\nnext line")).StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task ButtonsEmitValidBlockKitJson()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, PostMessageUrl)
            .WithJsonBody(body =>
                body.GetProperty("channel").GetString() == "C123" &&
                body.GetProperty("thread_ts").GetString() == "1700000000.000001" &&
                body.GetProperty("blocks")[0].GetProperty("text").GetProperty("text").GetString() == "Pick an option:" &&
                body.GetProperty("blocks")[1].GetProperty("elements").EnumerateArray()
                    .Select(button => button.GetProperty("action_id").GetString()).SequenceEqual(["button_yes", "button_no"]))
            .RespondJson(new { ok = true, channel = "C123", ts = "1700000001.000001" });

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send(SlackActivity("-buttons")).StartTestAsync();
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Theory]
    [InlineData("reaction_added", "positive_feedback")]
    [InlineData("event\"name\\suffix", "feedback\"value\\suffix")]
    public async Task EventAndFeedbackUseTheirActualSlackPayloads(string eventName, string reactionValue)
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, PostMessageUrl)
            .WithJsonBody(new { channel = "C123", text = $"Agent got: {eventName}" })
            .RespondJson(new { ok = true, channel = "C123", ts = "1700000001.000001" });
        http.Expect(HttpMethod.Post, PostMessageUrl)
            .WithJsonBody(new { channel = "C123", thread_ts = "1700000000.000001", text = $"Agent got feedback: {reactionValue}" })
            .RespondJson(new { ok = true, channel = "C123", ts = "1700000002.000001" });

        var reaction = SlackActivity(null);
        reaction.Type = ActivityTypes.Event;
        reaction.Name = eventName;
        var feedback = SlackActivity(null);
        feedback.Type = ActivityTypes.Invoke;
        feedback.Name = "message/submitAction";
        feedback.Value = new { actionName = "feedback", actionValue = new { reaction = reactionValue } };

        await using (var host = CreateHost(http))
        {
            await host.CreateTestFlow().Send(reaction).Send(feedback).StartTestAsync();
        }

        http.Verify();
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "service_unavailable")]
    [InlineData(HttpStatusCode.OK, "channel_not_found")]
    public async Task SlackFailuresPropagateWithoutManufacturingAdapterReplies(HttpStatusCode status, string error)
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, PostMessageUrl)
            .RespondJson(new { ok = false, error }, status);

        await using (var host = CreateHost(http))
        {
            var failure = await Assert.ThrowsAsync<SlackResponseException>(
                () => host.CreateTestFlow().Send(SlackActivity("hello")).StartTestAsync());
            Assert.Contains($"HTTP {(int)status}", failure.Message, StringComparison.Ordinal);
            Assert.Contains(error, failure.Message, StringComparison.Ordinal);
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    [Fact]
    public async Task NetworkFaultPropagatesFromTheRealSlackClient()
    {
        using var http = new HttpSimulation();
        http.Expect(HttpMethod.Post, PostMessageUrl).Throw(() => new HttpRequestException("Slack transport unavailable"));

        await using (var host = CreateHost(http))
        {
            await Assert.ThrowsAsync<HttpRequestException>(
                () => host.CreateTestFlow().Send(SlackActivity("hello")).StartTestAsync());
            Assert.Null(host.Adapter.GetNextReply());
        }

        http.Verify();
    }

    private static Builder.Testing.AgentTestHost CreateHost(HttpSimulation http) =>
        HttpSampleHost.Create(http, (options, _) => new SlackAgent.MyAgent(options), Channels.Slack);

    private static Activity SlackActivity(string text) => new()
    {
        Type = ActivityTypes.Message,
        Text = text,
        ChannelId = Channels.Slack,
        ChannelData = new SlackChannelData
        {
            ApiToken = "synthetic-slack-token",
            Envelope = ProtocolJsonSerializer.ToObject<EventEnvelope>("""
                {
                  "type": "event_callback",
                  "team_id": "T123",
                  "api_app_id": "A123",
                  "event_id": "Ev123",
                  "event_time": 1700000000,
                  "event": {
                    "type": "message",
                    "channel": "C123",
                    "user": "U123",
                    "text": "hello",
                    "ts": "1700000001.000001",
                    "thread_ts": "1700000000.000001"
                  }
                }
                """)
        }
    };
}
