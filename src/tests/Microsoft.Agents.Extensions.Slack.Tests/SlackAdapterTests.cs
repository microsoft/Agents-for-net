// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
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
    public void SlackAdapterOptions_BotName_RetainsConfiguredValue()
    {
        var options = new SlackAdapterOptions { BotName = "SlackAgent" };

        Assert.Equal("SlackAgent", options.BotName);
    }

    [Fact]
    public void SlackAdapterOptions_BotName_DefaultsToEmptyString()
    {
        var options = new SlackAdapterOptions();

        Assert.Equal(string.Empty, options.BotName);
    }

    [Fact]
    public void Constructors_PreserveOriginalAndLoggerAwareSignatures()
    {
        var originalConstructor = typeof(SlackAdapter).GetConstructor(
            [typeof(SlackAdapterOptions), typeof(IHttpClientFactory), typeof(ILogger<SlackAdapter>)]);
        var loggerAwareConstructor = typeof(SlackAdapter).GetConstructor(
            [typeof(SlackAdapterOptions), typeof(IHttpClientFactory), typeof(ILogger<SlackAdapter>), typeof(ILogger<SlackApi>)]);

        Assert.NotNull(originalConstructor);
        Assert.True(originalConstructor.GetParameters()[2].IsOptional);
        Assert.Null(originalConstructor.GetParameters()[2].DefaultValue);
        Assert.NotNull(loggerAwareConstructor);
    }

    [Fact]
    public async Task DependencyInjection_UsesSlackApiLogger()
    {
        var apiLogger = new RecordingLogger<SlackApi>();
        var turnCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factory = CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"ts":"1"}""", Encoding.UTF8, "application/json")
        }));
        var agent = DelegateAgent(async (turnContext, cancellationToken) =>
        {
            await turnContext.SendActivityAsync("pong", cancellationToken: cancellationToken);
            turnCompleted.TrySetResult();
        });
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSlack(new SlackAdapterOptions
                {
                    BotToken = BotToken,
                    SigningSecret = SigningSecret,
                    BotId = BotId,
                    BotUserId = BotUserId
                });
                services.AddSingleton(factory.Object);
                services.AddSingleton<ILogger<SlackAdapter>>(new RecordingLogger<SlackAdapter>());
                services.AddSingleton<ILogger<SlackApi>>(apiLogger);
                services.AddSingleton(agent.GetType(), agent);
                services.AddSingleton(agent);
            })
            .Build();
        await host.StartAsync();

        var adapter = host.Services.GetRequiredService<SlackAdapter>();
        var context = CreateContext(MessageEventBody("ping", "C100", "1700000000.000100"), signed: true);

        try
        {
            await adapter.ProcessAsync(
                context.Request,
                context.Response,
                agent,
                CancellationToken.None);
            await turnCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Contains(apiLogger.Entries, entry => entry.EventId.Id == 1);
            Assert.Contains(apiLogger.Entries, entry => entry.EventId.Id == 2);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task ProcessAsync_RequestCancellation_DoesNotCancelQueuedTurn()
    {
        var turnStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTurn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var turnCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken turnCancellationToken = default;
        var agent = DelegateAgent(async (_, cancellationToken) =>
        {
            turnCancellationToken = cancellationToken;
            turnStarted.TrySetResult();
            await releaseTurn.Task;
            turnCompleted.TrySetResult();
        });

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSlack(new SlackAdapterOptions
                {
                    BotToken = BotToken,
                    SigningSecret = SigningSecret,
                    BotId = BotId,
                    BotUserId = BotUserId
                });
                services.AddSingleton(agent.GetType(), agent);
                services.AddSingleton(agent);
            })
            .Build();

        await host.StartAsync();
        using var requestCancellation = new CancellationTokenSource();
        var adapter = host.Services.GetRequiredService<SlackAdapter>();
        var context = CreateContext(MessageEventBody("stream", "C100", "1700000000.000100"), signed: true);

        try
        {
            var processTask = adapter.ProcessAsync(
                context.Request,
                context.Response,
                agent,
                requestCancellation.Token);

            await turnStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(processTask.IsCompletedSuccessfully);
            Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);

            requestCancellation.Cancel();

            Assert.False(turnCancellationToken.IsCancellationRequested);
        }
        finally
        {
            releaseTurn.TrySetResult();
            await turnCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await host.StopAsync();
        }
    }

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
    public async Task ProcessAsync_VerifiedEvent_LogsSanitizedPayloadAndActivity()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, logger: logger);
        var body = """
            {
              "type":"event_callback",
              "token":"legacy-secret",
              "team_id":"T1",
              "event_id":"EvLOG",
              "event":{"type":"message","channel":"C100","text":"hello","ts":"1700000000.000100","user":"U999","channel_type":"channel"}
            }
            """;
        var context = CreateContext(body, signed: true);

        await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

        var payloadLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 1);
        Assert.Equal(LogLevel.Debug, payloadLog.Level);
        Assert.Contains("hello", payloadLog.Message);
        Assert.Contains("[REDACTED]", payloadLog.Message);
        Assert.DoesNotContain("legacy-secret", payloadLog.Message);

        var activityLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 2);
        Assert.Equal(LogLevel.Debug, activityLog.Level);
        Assert.Contains("\"type\":\"message\"", activityLog.Message);
        Assert.Contains("[REDACTED]", activityLog.Message);
        Assert.DoesNotContain(BotToken, activityLog.Message);
    }

    [Fact]
    public async Task ProcessAsync_VerifiedInteractivePayload_LogsDecodedSanitizedPayload()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, logger: logger);
        var payload = """
            {"type":"block_actions","response_url":"https://hooks.slack.com/actions/secret","user":{"id":"U777"},"team":{"id":"T1"},"channel":{"id":"C200"},"actions":[{"action_id":"button_yes","value":"yes"}]}
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

        var payloadLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 1);
        Assert.Contains("\"type\":\"block_actions\"", payloadLog.Message);
        Assert.Contains("[REDACTED]", payloadLog.Message);
        Assert.DoesNotContain("hooks.slack.com", payloadLog.Message);
        Assert.DoesNotContain("payload=", payloadLog.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidSignature_DoesNotLogPayload()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, logger: logger);
        const string body = """{"type":"event_callback","body_marker":"do-not-log"}""";
        var context = CreateContext(body, signed: true, tamperSignature: true);

        await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 1);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("do-not-log", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_AdapterLoggerFailure_DoesNotChangeInboundProcessing()
    {
        var logger = new ThrowingLogger<SlackAdapter>(
            isEnabledException: new InvalidOperationException("Adapter logger failed"));
        var adapter = CreateAdapter(out _, logger: logger);
        var context = CreateContext(
            MessageEventBody(text: "hello", channel: "C100", ts: "1700000000.000100"),
            signed: true);
        var agentCalled = false;

        await adapter.ProcessAsync(
            context.Request,
            context.Response,
            DelegateAgent((_, _) =>
            {
                agentCalled = true;
                return Task.CompletedTask;
            }),
            CancellationToken.None);

        Assert.True(agentCalled);
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
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
    public async Task ProcessAsync_OrgInstalledEvent_UsesContextTeamId()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":null,
              "context_team_id":"T2",
              "event_id":"EvCONTEXTTEAM",
              "event":{"type":"message","team":"T3","channel":"C100","text":"hello","ts":"1700000000.000100","user":"U999","channel_type":"channel"}
            }
            """;
        var context = CreateContext(body, signed: true);

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal("U999:T2", slack.From.Id);
        Assert.Equal("B123:T2", slack.Recipient.Id);
        Assert.Equal("B123:T2:C100", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_OrgInstalledEvent_UsesEventTeamId()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":null,
              "context_team_id":null,
              "event_id":"EvEVENTTEAM",
              "event":{"type":"message","team":"T3","channel":"C100","text":"hello","ts":"1700000000.000100","user":"U999","channel_type":"channel"}
            }
            """;
        var context = CreateContext(body, signed: true);

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal("U999:T3", slack.From.Id);
        Assert.Equal("B123:T3", slack.Recipient.Id);
        Assert.Equal("B123:T3:C100", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_EventWithoutTeamId_Returns400()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":null,
              "context_team_id":null,
              "event_id":"EvNOTEAM",
              "event":{"type":"message","channel":"C100","text":"hello","ts":"1700000000.000100","user":"U999","channel_type":"channel"}
            }
            """;
        var context = CreateContext(body, signed: true);

        var agentCalled = false;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) =>
        {
            agentCalled = true;
            return Task.CompletedTask;
        }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.False(agentCalled);
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
    public async Task ProcessAsync_QueueRejected_AllowsSlackRetry()
    {
        var queue = new Mock<IActivityTaskQueue>();
        queue.SetupSequence(q => q.QueueBackgroundActivity(
                It.IsAny<ClaimsIdentity>(),
                It.IsAny<IChannelAdapter>(),
                It.IsAny<IActivity>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<Type>(),
                It.IsAny<Func<InvokeResponse, Task>>(),
                It.IsAny<IHeaderDictionary>()))
            .Returns(false)
            .Returns(true);
        var factory = CreateFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var adapter = new SlackAdapter(
            new SlackAdapterOptions
            {
                BotToken = BotToken,
                SigningSecret = SigningSecret,
                BotId = BotId,
                BotUserId = BotUserId
            },
            factory.Object,
            null!,
            null!,
            queue.Object);
        var body = MessageEventBody(text: "retry", channel: "C100", ts: "1700000000.000100", eventId: "EvQUEUE");

        var first = CreateContext(body, signed: true);
        await adapter.ProcessAsync(first.Request, first.Response, NoopAgent(), CancellationToken.None);

        var second = CreateContext(body, signed: true);
        await adapter.ProcessAsync(second.Request, second.Response, NoopAgent(), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, first.Response.StatusCode);
        Assert.Equal((int)HttpStatusCode.OK, second.Response.StatusCode);
        queue.Verify(q => q.QueueBackgroundActivity(
            It.IsAny<ClaimsIdentity>(),
            It.IsAny<IChannelAdapter>(),
            It.IsAny<IActivity>(),
            It.IsAny<bool>(),
            It.IsAny<string>(),
            It.IsAny<Type>(),
            It.IsAny<Func<InvokeResponse, Task>>(),
            It.IsAny<IHeaderDictionary>()), Times.Exactly(2));
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
    public async Task ProcessAsync_BotOwnChangedMessage_Ignored()
    {
        var adapter = CreateAdapter(out _);
        var body = """
            {
              "type":"event_callback",
              "team_id":"T1",
              "event_id":"EvBOTCHANGED",
              "event":{
                "type":"message",
                "subtype":"message_changed",
                "channel":"C100",
                "message":{"user":"U123","bot_id":"B123","text":"stream result","ts":"1700000000.000200"}
              }
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
        var adapterLogger = new RecordingLogger<SlackAdapter>();
        var apiLogger = new RecordingLogger<SlackApi>();
        var adapter = CreateAdapter(out _, (request, cancellationToken) =>
        {
            var body = request.Content!.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            captured.Add((request.RequestUri!.ToString(), body));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"ts":"1700000000.000200"}""", Encoding.UTF8, "application/json")
            });
        }, adapterLogger, apiLogger);

        var context = CreateContext(MessageEventBody(text: "ping", channel: "C100", ts: "1700000000.000100"), signed: true);

        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent(async (tc, ct) =>
        {
            var activity = MessageFactory.Text("pong");
            activity.ChannelData = new SlackChannelData { ApiToken = BotToken };
            await tc.SendActivityAsync(activity, ct);
        }), CancellationToken.None);

        var post = Assert.Single(captured);
        Assert.Equal("https://slack.com/api/chat.postMessage", post.Uri);
        Assert.Contains("\"channel\":\"C100\"", post.Body, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"pong\"", post.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"thread_ts\"", post.Body, StringComparison.Ordinal);

        var requestLog = Assert.Single(apiLogger.Entries, entry => entry.EventId.Id == 1);
        Assert.Equal(LogLevel.Debug, requestLog.Level);
        Assert.Contains("chat.postMessage", requestLog.Message);

        var responseLog = Assert.Single(apiLogger.Entries, entry => entry.EventId.Id == 2);
        Assert.Equal(LogLevel.Debug, responseLog.Level);
        Assert.Contains("chat.postMessage", responseLog.Message);

        var sentLog = Assert.Single(adapterLogger.Entries, entry => entry.EventId.Id == 3);
        Assert.Equal(LogLevel.Debug, sentLog.Level);
        Assert.Contains("B123:T1:C100", sentLog.Message);
        Assert.Contains("pong", sentLog.Message);
        Assert.Contains("1700000000.000200", sentLog.Message);
        Assert.DoesNotContain(BotToken, sentLog.Message);
    }

    [Fact]
    public async Task SendActivitiesAsync_NonMessage_DoesNotLogSentResponse()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, logger: logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
        });

        await adapter.SendActivitiesAsync(
            turnContext.Object,
            [new Activity { Type = ActivityTypes.Typing }],
            CancellationToken.None);

        Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 3);
    }

    [Fact]
    public async Task SendActivitiesAsync_ActivityWithCollidingJsonProperties_LogsUnavailableAndReturnsSuccess()
    {
        var slackCalls = 0;
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, (_, _) =>
        {
            slackCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"ts":"1700000000.000250"}""", Encoding.UTF8, "application/json")
            });
        }, logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });
        var activity = MessageFactory.Text("sent");
        activity.Value = new CollidingJsonProperties();

        var responses = await adapter.SendActivitiesAsync(
            turnContext.Object,
            [activity],
            CancellationToken.None);

        var response = Assert.Single(responses);
        Assert.Equal("1700000000.000250", response.Id);
        Assert.Equal(1, slackCalls);
        var sentLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 3);
        Assert.Contains("[UNAVAILABLE]", sentLog.Message);
    }

    [Fact]
    public async Task SendActivitiesAsync_ActivityWithThrowingProperty_LogsUnavailableAndReturnsSuccess()
    {
        var slackCalls = 0;
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, (_, _) =>
        {
            slackCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"ts":"1700000000.000251"}""", Encoding.UTF8, "application/json")
            });
        }, logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });
        var activity = MessageFactory.Text("sent");
        activity.Value = new ThrowingJsonProperty();

        var responses = await adapter.SendActivitiesAsync(
            turnContext.Object,
            [activity],
            CancellationToken.None);

        var response = Assert.Single(responses);
        Assert.Equal("1700000000.000251", response.Id);
        Assert.Equal(1, slackCalls);
        var sentLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 3);
        Assert.Contains("[UNAVAILABLE]", sentLog.Message);
    }

    [Fact]
    public async Task SendActivitiesAsync_FailedSlackCall_DoesNotLogSentResponse()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":false,"error":"channel_not_found"}""", Encoding.UTF8, "application/json")
        }), logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });

        await Assert.ThrowsAsync<SlackResponseException>(() => adapter.SendActivitiesAsync(
            turnContext.Object,
            [MessageFactory.Text("not sent")],
            CancellationToken.None));

        Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 3);
    }

    [Fact]
    public async Task SendActivitiesAsync_AdapterLoggerFailure_DoesNotFailSuccessfulSend()
    {
        var slackCalls = 0;
        var logger = new ThrowingLogger<SlackAdapter>(
            logException: eventId => eventId.Id == 3
                ? new InvalidOperationException("Adapter logger failed")
                : null);
        var adapter = CreateAdapter(out _, (_, _) =>
        {
            slackCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"ts":"1700000000.000252"}""", Encoding.UTF8, "application/json")
            });
        }, logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });

        var responses = await adapter.SendActivitiesAsync(
            turnContext.Object,
            [MessageFactory.Text("sent")],
            CancellationToken.None);

        Assert.Equal(1, slackCalls);
        Assert.Equal("1700000000.000252", Assert.Single(responses).Id);
    }

    [Fact]
    public async Task UpdateActivityAsync_LogsSuccessfulResponse()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"ts":"1700000000.000300"}""", Encoding.UTF8, "application/json")
        }), logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });
        var activity = MessageFactory.Text("updated");
        activity.Id = "1700000000.000100";
        activity.ChannelData = new SlackChannelData { ApiToken = BotToken };

        var response = await adapter.UpdateActivityAsync(turnContext.Object, activity, CancellationToken.None);

        Assert.Equal("1700000000.000300", response.Id);
        var updateLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 4);
        Assert.Equal(LogLevel.Debug, updateLog.Level);
        Assert.Contains("B123:T1:C100", updateLog.Message);
        Assert.Contains("updated", updateLog.Message);
        Assert.Contains("1700000000.000300", updateLog.Message);
        Assert.DoesNotContain(BotToken, updateLog.Message);
    }

    [Fact]
    public async Task DeleteActivityAsync_LogsSuccessfulResponse()
    {
        var logger = new RecordingLogger<SlackAdapter>();
        var adapter = CreateAdapter(out _, (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true}""", Encoding.UTF8, "application/json")
        }), logger);
        var turnContext = new Mock<ITurnContext>();
        turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
        {
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
            ChannelData = new SlackChannelData { ApiToken = BotToken },
        });
        var reference = new ConversationReference
        {
            ActivityId = "1700000000.000100",
            Conversation = new ConversationAccount(id: "B123:T1:C100"),
        };

        await adapter.DeleteActivityAsync(turnContext.Object, reference, CancellationToken.None);

        var deleteLog = Assert.Single(logger.Entries, entry => entry.EventId.Id == 5);
        Assert.Equal(LogLevel.Debug, deleteLog.Level);
        Assert.Contains("B123:T1:C100", deleteLog.Message);
        Assert.Contains("SlackTimestamp=1700000000.000100", deleteLog.Message);
        Assert.Contains("1700000000.000100", deleteLog.Message);
        Assert.DoesNotContain(BotToken, deleteLog.Message);
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
        Assert.Equal("U777:T1", slack.From.Id);
        Assert.Equal("B123:T1", slack.Recipient.Id);
        Assert.Equal("B123:T1:C200:1700000000.000300", slack.Conversation.Id);
        Assert.Equal("C200", SlackHelpers.SlackChannelIdFromConversationId(slack.Conversation.Id));
    }

    [Fact]
    public async Task ProcessAsync_FeedbackButtons_CreatesFeedbackInvokeActivity()
    {
        var adapter = CreateAdapter(out _);
        var payload = """
            {
              "type":"block_actions",
              "user":{"id":"U777","team_id":"T1"},
              "team":{"id":"T1"},
              "channel":{"id":"C200","name":"directmessage"},
              "message":{"ts":"1700000000.000300","thread_ts":"1600000000.000200"},
              "actions":[{
                "action_id":"feedback",
                "type":"feedback_buttons",
                "value":"positive_feedback",
                "action_ts":"1700000001.000400"
              }]
            }
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        IActivity? captured = null;
        await adapter.ProcessAsync(
            context.Request,
            context.Response,
            DelegateAgent((turnContext, _) =>
            {
                captured = turnContext.Activity;
                return Task.CompletedTask;
            }),
            CancellationToken.None);

        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal(ActivityTypes.Invoke, slack.Type);
        Assert.Equal("message/submitAction", slack.Name);

        var value = ProtocolJsonSerializer.ToObject<JsonObject>(slack.Value);
        Assert.Equal("feedback", value!["actionName"]!.GetValue<string>());
        Assert.Equal("positive_feedback", value["actionValue"]!["reaction"]!.GetValue<string>());
        Assert.Equal("1600000000.000200", value["replyToId"]!.GetValue<string>());
        Assert.Equal("B123:T1:C200:1600000000.000200", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_FeedbackButtons_InvokesSlackFeedbackLoopRoute()
    {
        var adapter = CreateAdapter(out _);
        var app = new FeedbackLoopRouteApp(new AgentApplicationOptions((IStorage)null!));
        var payload = """
            {
              "type":"block_actions",
              "user":{"id":"U777","team_id":"T1"},
              "team":{"id":"T1"},
              "channel":{"id":"C200"},
              "message":{"ts":"1700000000.000300","thread_ts":"1600000000.000200"},
              "actions":[{
                "action_id":"feedback",
                "type":"feedback_buttons",
                "value":"positive_feedback"
              }]
            }
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        await adapter.ProcessAsync(context.Request, context.Response, app, CancellationToken.None);

        Assert.Single(app.calls);
        Assert.Equal("OnFeedback", app.calls[0]);
        Assert.Equal("positive_feedback", app.feedbackData!.ActionValue!.Reaction);
        Assert.Equal("1600000000.000200", app.feedbackData.ReplyToId);
    }

    [Fact]
    public async Task ProcessAsync_OrgInstalledBlockAction_UsesUserTeamId()
    {
        var adapter = CreateAdapter(out _);
        var payload = """
            {"type":"block_actions","user":{"id":"U777","team_id":"T2"},"team":null,"channel":{"id":"C200"},"message":{"ts":"1700000000.000300"},"actions":[{"action_id":"button_yes","value":"yes"}]}
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal("U777:T2", slack.From.Id);
        Assert.Equal("B123:T2", slack.Recipient.Id);
        Assert.Equal("B123:T2:C200:1700000000.000300", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_OrgInstalledViewSubmission_UsesInstalledTeamId()
    {
        var adapter = CreateAdapter(out _);
        var payload = """
            {"type":"view_submission","user":{"id":"U777"},"team":null,"channel":{"id":"C200"},"view":{"app_installed_team_id":"T3"}}
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        IActivity? captured = null;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((tc, _) => { captured = tc.Activity; return Task.CompletedTask; }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
        var slack = Assert.IsType<SlackActivity>(captured);
        Assert.Equal("U777:T3", slack.From.Id);
        Assert.Equal("B123:T3", slack.Recipient.Id);
        Assert.Equal("B123:T3:C200", slack.Conversation.Id);
    }

    [Fact]
    public async Task ProcessAsync_InteractivePayloadWithoutTeamId_Returns400()
    {
        var adapter = CreateAdapter(out _);
        var payload = """
            {"type":"block_actions","user":{"id":"U777"},"team":null,"channel":{"id":"C200"},"actions":[{"action_id":"button_yes","value":"yes"}]}
            """;
        var body = "payload=" + WebUtility.UrlEncode(payload);
        var context = CreateContext(body, signed: true, contentType: "application/x-www-form-urlencoded");

        var agentCalled = false;
        await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) =>
        {
            agentCalled = true;
            return Task.CompletedTask;
        }), CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.False(agentCalled);
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

    private static SlackAdapter CreateAdapter(
        out Mock<IHttpClientFactory> factory,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? sendFunc = null,
        ILogger<SlackAdapter>? logger = null,
        ILogger<SlackApi>? slackApiLogger = null)
    {
        sendFunc ??= (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"ok":true,"ts":"1"}""", Encoding.UTF8, "application/json")
        });

        factory = CreateFactory(sendFunc);
        return new SlackAdapter(
            new SlackAdapterOptions { BotToken = BotToken, SigningSecret = SigningSecret, BotId = BotId, BotUserId = BotUserId },
            factory.Object,
            logger!,
            slackApiLogger!);
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

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        private readonly Exception? _isEnabledException;
        private readonly Func<EventId, Exception?>? _logException;

        public ThrowingLogger(
            Exception? isEnabledException = null,
            Func<EventId, Exception?>? logException = null)
        {
            _isEnabledException = isEnabledException;
            _logException = logException;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            if (_isEnabledException != null)
            {
                throw _isEnabledException;
            }

            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var logException = _logException?.Invoke(eventId);
            if (logException != null)
            {
                throw logException;
            }
        }
    }
}
