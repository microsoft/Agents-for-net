// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackAdapterTests
{
    private const string SigningSecret = "8f742231b10e8888abcd99yyyzzz85a5";
    private const string BotToken = "xoxb-test-token";
    private const string BotId = "B123";
    private const string BotUserId = "U123";

    [Fact]
    public async Task ProcessAsync_UrlVerification_ReturnsChallenge()
    {
        var adapter = CreateAdapter(out _);
        const string body = """{"type":"url_verification","challenge":"abc123"}""";
        var context = CreateContext(body, signed: true);

        await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal("abc123", ReadResponse(context));
    }

    [Fact]
    public async Task ProcessAsync_InvalidSignature_Returns401()
    {
        var adapter = CreateAdapter(out _);
        const string body = """{"type":"url_verification","challenge":"abc123"}""";
        var context = CreateContext(body, signed: true, tamperSignature: true);

        var agentCalled = false;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) => { agentCalled = true; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);
        Assert.False(agentCalled);
    }

    [Fact]
    public async Task ProcessAsync_MessageEvent_InvokesAgentWithSlackActivity()
    {
        var adapter = CreateAdapter(out _);
        var context = CreateContext(MessageEventBody(text: "hello there", channel: "C100", ts: "1700000000.000100"), signed: true);

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal(ActivityTypes.Message, slack.Type);
        Assert.Equal("hello there", slack.Text);
        Assert.Equal(Channels.Slack, slack.ChannelId.Channel);
        Assert.Equal("C100", slack.ChannelData.Channel);
        Assert.Equal(BotToken, slack.ChannelData.ApiToken);
        Assert.Equal("U999:T1", slack.From.Id);
        Assert.Equal("B123:T1", slack.Recipient.Id);
        Assert.Equal("B123:T1:C100", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_DuplicateEventId_ProcessedOnce()
    {
        var adapter = CreateAdapter(out _);
        var body = MessageEventBody(text: "hi", channel: "C100", ts: "1700000000.000100", eventId: "Ev0DEDUP");

        var count = 0;
        Task Handler(ITurnContext tc, CancellationToken ct) { count++; return Task.CompletedTask; }

        var first = CreateContext(body, signed: true);
        await adapter.ProcessAsync(first.Request, first.Response, DelegateAgent(Handler), CancellationToken.None);

        var second = CreateContext(body, signed: true);
        await adapter.ProcessAsync(second.Request, second.Response, DelegateAgent(Handler), CancellationToken.None);

        Assert.Equal(1, count);
        Assert.Equal((int)HttpStatusCode.OK, second.Response.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_BotOwnMessage_Ignored()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":"T1",
              "event_id":"EvBOT",
              "event":{"type":"message","channel":"C100","text":"loop","ts":"1700000000.000100","bot_id":"B1"}
            }
            """;
        var context = CreateContext(body, signed: true);

        var agentCalled = false;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) => { agentCalled = true; return Task.CompletedTask; }), CancellationToken.None);

        Assert.False(agentCalled);
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_BotUserOwnMessage_Ignored()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":"T1",
              "event_id":"EvBOTUSER",
              "event":{"type":"message","channel":"C100","text":"loop","ts":"1700000000.000100","user":"U123"}
            }
            """;
        var context = CreateContext(body, signed: true);

        var agentCalled = false;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) =>
        {
            agentCalled = true;
            return Task.CompletedTask;
        }), CancellationToken.None);

        Assert.False(agentCalled);
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ProcessAsync_SendActivity_PostsToSlack()
    {
        var captured = new List<(string Uri, string Body)>();
        var adapter = CreateAdapter(out _, (request, cancellationToken) =>
        {
            var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            captured.Add((request.RequestUri!.ToString(), body));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"ts":"1700000000.000200"}""", Encoding.UTF8, "application/json")
            });
        });

        var context = CreateContext(MessageEventBody(text: "ping", channel: "C100", ts: "1700000000.000100"), signed: true);

        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent(async (tc, ct) =>
        {
            await tc.SendActivityAsync(MessageFactory.Text("pong"), ct);
        }), CancellationToken.None);

        var post = Assert.Single(captured);
        Assert.Equal("https://slack.com/api/chat.postMessage", post.Uri);
        Assert.Contains("\"channel\":\"C100\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"pong\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"thread_ts\"", post.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessAsync_InteractivePayload_InvokesAgentAsEvent()
    {
        var adapter = CreateAdapter(out _);
        var payload = """
            {"type":"block_actions","user":{"id":"U777"},"team":{"id":"T1"},"channel":{"id":"C200"},"message":{"ts":"1700000000.000300"},"actions":[{"action_id":"button_yes","value":"yes"}]}
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal(ActivityTypes.Event, slack.Type);
        Assert.Equal("block_actions", slack.Name);
        Assert.Equal("U777", slack.From.Id);
        Assert.Equal("C200", SlackHelpers.SlackChannelIdFromConversationId(slack.Conversation.Id));
    }

    [Fact]
    public async Task ProcessAsync_NoSigningSecret_SkipsVerification()
    {
        var adapter = new SlackAdapter(
            new SlackAdapterOptions { BotToken = BotToken, BotId = BotId, BotUserId = BotUserId },
            CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))).Object);

        const string body = """{"type":"url_verification","challenge":"nosig"}""";
        var context = CreateContext(body, signed: false);

        await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        Assert.Equal("nosig", ReadResponse(context));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static SlackAdapter CreateAdapter(out Mock<IHttpClientFactory> factory,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? sendFunc = null)
    {
        sendFunc ??= (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"ts":"1"}""", Encoding.UTF8, "application/json")
        });

        factory = CreateFactory(sendFunc);
        return new SlackAdapter(
            new SlackAdapterOptions { BotToken = BotToken, SigningSecret = SigningSecret, BotId = BotId, BotUserId = BotUserId },
            factory.Object);
    }

    private static Mock<IHttpClientFactory> CreateFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(new TestHandler(sendFunc)));
        return factory;
    }

    private static string MessageEventBody(string text, string channel, string ts, string eventId = "Ev001")
        => $$"""
            {
              "type":"event_callback",
              "team_id":"T1",
              "event_id":"{{eventId}}",
              "event":{"type":"message","channel":"{{channel}}","text":"{{text}}","ts":"{{ts}}","user":"U999","channel_type":"channel"}
            }
            """;

    private static DefaultHttpContext CreateContext(string body, bool signed, bool tamperSignature = false, string contentType = "application/json")
    {
        var context = new DefaultHttpContext();
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = contentType;
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
        context.Response.Body = new MemoryStream();

        if (signed)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ComputeSignature(SigningSecret, timestamp, body);
            if (tamperSignature)
            {
                signature = "v0=deadbeef";
            }

            context.Request.Headers["X-Slack-Request-Timestamp"] = timestamp;
            context.Request.Headers["X-Slack-Signature"] = signature;
        }

        return context;
    }

    private static string ComputeSignature(string secret, string timestamp, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestamp}:{body}"));
        var sb = new StringBuilder("v0=");
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    private static string ReadResponse(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    private static IAgent NoopAgent() => DelegateAgent((_, _) => Task.CompletedTask);

    private static IAgent DelegateAgent(Func<ITurnContext, CancellationToken, Task> onTurn) => new TestAgent(onTurn);

    private sealed class TestAgent : IAgent
    {
        private readonly Func<ITurnContext, CancellationToken, Task> _onTurn;
        public TestAgent(Func<ITurnContext, CancellationToken, Task> onTurn) => _onTurn = onTurn;
        public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default) => _onTurn(turnContext, cancellationToken);
    }

    private sealed class TestHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;
        public TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc) => _sendFunc = sendFunc;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => _sendFunc(request, cancellationToken);
    }
}
