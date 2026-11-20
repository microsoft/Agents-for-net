// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.Models;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.Teams.Apps;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using BuilderTurnContext = Microsoft.Agents.Builder.TurnContext;
using CoreChannels = Microsoft.Agents.Core.Models.Channels;
using Entity = Microsoft.Agents.Core.Models.Entity;
using PlanInfoEntity = Microsoft.Agents.Extensions.MSTeams.Models.PlanInfoEntity;
using PlanStep = Microsoft.Teams.Apps.Schema.Entities.PlanStep;
using PlanStepStatuses = Microsoft.Teams.Apps.Schema.Entities.PlanStepStatuses;
using PlanTypes = Microsoft.Teams.Apps.Schema.Entities.PlanTypes;
using StepSource = Microsoft.Teams.Apps.Schema.Entities.StepSource;

#pragma warning disable ExperimentalTeamsPlan // Exercise the explicitly opted-in Plans surface.

namespace Microsoft.Agents.Extensions.MSTeams.Tests;

public partial class TeamsPlanTests
{
    [Fact]
    public void PlanEntity_RoundTripsAsCoreEntityWithStepsSourcesAndExtensionData()
    {
        const string json = """
            {"type":"typing","entities":[{"type":"planInfo","planType":"start","steps":[{"id":"outline","version":1,"title":"Outline","status":"active","sources":[{"title":"Reference","url":"https://example.com/reference"}]}],"future":"preserved"}]}
            """;

        var activity = ProtocolJsonSerializer.ToObject<Activity>(json);
        var entity = Assert.IsType<PlanInfoEntity>(Assert.Single(activity.Entities));
        var step = Assert.Single(entity.Steps);
        Assert.Equal("start", entity.PlanType.Value);
        Assert.Equal("outline", step.Id);
        Assert.Equal("Reference", Assert.Single(step.Sources).Title);
        Assert.Equal("https://example.com/reference", Assert.Single(step.Sources).Url.AbsoluteUri);

        using var roundTrip = JsonDocument.Parse(ProtocolJsonSerializer.ToJson(activity));
        var wireEntity = Assert.Single(roundTrip.RootElement.GetProperty("entities").EnumerateArray());
        Assert.Equal("planInfo", wireEntity.GetProperty("type").GetString());
        Assert.Equal("preserved", wireEntity.GetProperty("future").GetString());
        Assert.Equal(1, wireEntity.GetProperty("steps")[0].GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("start")]
    [InlineData("continue")]
    [InlineData("stop")]
    [InlineData("error")]
    public async Task Lifecycle_PostsPromptBoundTypingAndReportsNoMessageResult(string operation)
    {
        using var environment = new PlanEnvironment(_ => Response(HttpStatusCode.Accepted, ""));
        var plan = TeamsAgentExtension.CreatePlan(environment.Context);
        var notifications = new List<TeamsPlanSentEventArgs>();
        plan.Sent += (_, args) => notifications.Add(args);

        switch (operation)
        {
            case "start":
                await plan.StartAsync([NewStep()]);
                break;
            case "continue":
                await plan.UpdateAsync([NewStep()]);
                break;
            case "stop":
                await plan.StopAsync();
                break;
            case "error":
                await plan.ErrorAsync();
                break;
        }

        var request = Assert.Single(environment.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://regional.example/teams/v3/conversations/conversation/activities/prompt", request.Uri.AbsoluteUri);
        Assert.Equal("Bearer connector-token", request.Authorization);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("typing", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("prompt", body.RootElement.GetProperty("replyToId").GetString());
        var entity = GetPlanEntity(body.RootElement);
        Assert.Equal(operation, entity.GetProperty("planType").GetString());
        Assert.Equal(operation is "start" or "continue", entity.TryGetProperty("steps", out _));
        Assert.Null(Assert.Single(notifications).Response);
    }

    [Theory]
    [InlineData("start", false)]
    [InlineData("continue", false)]
    [InlineData("stop", false)]
    [InlineData("error", false)]
    [InlineData("complete", false)]
    [InlineData("stream", false)]
    [InlineData("start", true)]
    [InlineData("continue", true)]
    [InlineData("stop", true)]
    [InlineData("error", true)]
    [InlineData("complete", true)]
    [InlineData("stream", true)]
    public async Task PlanDelivery_RunsOutgoingHooksWithOriginalBinding(string operation, bool chatThread)
    {
        using var environment = new PlanEnvironment(incoming: Incoming("continuation"));
        var intercepted = 0;
        environment.Context.OnSendActivities(async (context, activities, next) =>
        {
            for (var index = 0; index < activities.Count; index++)
            {
                if (activities[index].Entities?.Any(entity => entity.Type == "planInfo") != true)
                {
                    continue;
                }

                activities[index] = activities[index].Clone();
                activities[index].Properties["intercepted"] = JsonSerializer.SerializeToElement(true);
                intercepted++;
            }
            var responses = await next();
            Assert.All(activities.Where(activity => activity.Entities.Any(entity => entity.Type == "planInfo")),
                activity => Assert.Equal("original-prompt", activity.ReplyToId));
            return responses;
        });
        var plan = new TeamsPlan(environment.Context, new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt",
            ReplyChain = chatThread ? new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" } : null
        });

        switch (operation)
        {
            case "start":
                await plan.StartAsync([NewStep()]);
                break;
            case "continue":
                await plan.UpdateAsync([NewStep()]);
                break;
            case "stop":
                await plan.StopAsync();
                break;
            case "error":
                await plan.ErrorAsync();
                break;
            case "complete":
                await plan.CompleteAsync("Answer.");
                break;
            case "stream":
                await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
                environment.Context.StreamingResponse.QueueTextChunk("Answer.");
                await plan.CompleteStreamingAsync();
                break;
        }

        var request = Assert.Single(environment.Requests, entry =>
            ProtocolJsonSerializer.ToObject<Activity>(entry.Body).Entities.Any(entity => entity.Type == "planInfo"));
        using var body = JsonDocument.Parse(request.Body);
        Assert.True(body.RootElement.GetProperty("intercepted").GetBoolean());
        Assert.Equal(1, intercepted);
        Assert.Equal("original-prompt", body.RootElement.GetProperty("replyToId").GetString());
        Assert.Equal(chatThread && operation == "complete"
            ? "/teams/v3/conversations/chat/replyChains/root"
            : "/teams/v3/conversations/conversation/activities/original-prompt", request.Uri.AbsolutePath);
        Assert.True(environment.Context.Responded);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanDelivery_OutgoingHookCanSuppressCompletion(bool streamed)
    {
        using var environment = new PlanEnvironment();
        var plan = new TeamsPlan(environment.Context);
        var notifications = 0;
        plan.Sent += (_, _) => notifications++;
        environment.Context.OnSendActivities((context, activities, next) =>
            activities.Any(activity => activity.Entities?.Any(entity => entity.Type == "planInfo") == true)
                ? Task.FromResult(Array.Empty<ResourceResponse>())
                : next());

        if (streamed)
        {
            await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
            environment.Context.StreamingResponse.QueueTextChunk("Answer.");
            await plan.CompleteStreamingAsync();
        }
        else
        {
            await plan.CompleteAsync("Answer.");
            Assert.False(environment.Context.Responded);
        }

        Assert.DoesNotContain(environment.Requests, IsMessagePost);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanDelivery_IsRecordedByTranscriptLogger(bool streamed)
    {
        using var environment = new PlanEnvironment();
        var logger = new RecordingTranscriptLogger();
        var middleware = new TranscriptLoggerMiddleware(logger);

        await middleware.OnTurnAsync(environment.Context, async cancellationToken =>
        {
            var plan = new TeamsPlan(environment.Context);
            await plan.StartAsync([NewStep()], cancellationToken);
            if (streamed)
            {
                await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.", cancellationToken);
                environment.Context.StreamingResponse.QueueTextChunk("Answer.");
                await plan.CompleteStreamingAsync(cancellationToken);
            }
            else
            {
                await plan.CompleteAsync("Answer.", cancellationToken);
            }
        }, CancellationToken.None);

        var completion = Assert.Single(logger.Activities, activity =>
            activity.Type == ActivityTypes.Message && activity.Entities?.Any(entity => entity.Type == "planInfo") == true);
        Assert.Equal("Answer.", completion.Text);
        Assert.Equal("prompt", completion.ReplyToId);
        Assert.Equal("service-message", completion.Id);
        Assert.Contains(logger.Activities, activity => activity.Type == ActivityTypes.Typing
            && activity.Entities?.Any(entity => entity.Type == "planInfo") == true);
    }

    [Fact]
    public async Task PlanDelivery_NestedAndAddedMessagesKeepDefaultRouting()
    {
        using var environment = new PlanEnvironment(request =>
            Response(HttpStatusCode.OK, IsMessagePost(request)
                && ProtocolJsonSerializer.ToObject<Activity>(request.Body).Entities.Any(entity => entity.Type == "planInfo")
                    ? """{"id":"plan-message"}"""
                    : """{"id":"ordinary-message"}"""));
        environment.Context.OnSendActivities(async (context, activities, next) =>
        {
            if (activities.Any(activity => activity.Entities?.Any(entity => entity.Type == "planInfo") == true))
            {
                await context.SendActivityAsync("Nested.");
                var additional = activities[0].Clone();
                additional.Entities.Clear();
                additional.Text = "Added.";
                activities.Add(additional);
            }
            return await next();
        });

        var response = await new TeamsPlan(environment.Context).CompleteAsync("Answer.");

        Assert.Equal("plan-message", response.Id);
        Assert.Equal(3, environment.Requests.Length);
        var planRequest = Assert.Single(environment.Requests, request =>
            ProtocolJsonSerializer.ToObject<Activity>(request.Body).Text == "Answer.");
        Assert.Equal("/teams/v3/conversations/conversation/activities/prompt", planRequest.Uri.AbsolutePath);
        Assert.All(environment.Requests.Where(request => request != planRequest), request =>
            Assert.Equal("/teams/v3/conversations/conversation/activities/prompt", request.Uri.AbsolutePath));
    }

    [Theory]
    [InlineData(DeliveryModes.ExpectReplies, false)]
    [InlineData(DeliveryModes.ExpectReplies, true)]
    [InlineData(DeliveryModes.Stream, false)]
    [InlineData(DeliveryModes.Stream, true)]
    public async Task PlanDelivery_HonorsHostedAdapterRouting(string deliveryMode, bool streamed)
    {
        var hosted = new List<IActivity>();
        var incoming = Incoming();
        incoming.DeliveryMode = deliveryMode;
        using var environment = new PlanEnvironment(incoming: incoming, adapter: new HostedConnectorAdapter(hosted));
        var plan = new TeamsPlan(environment.Context);

        await plan.StartAsync([NewStep()]);
        if (streamed)
        {
            await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
            environment.Context.StreamingResponse.QueueTextChunk("Answer.");
            await plan.CompleteStreamingAsync();
        }
        else
        {
            await plan.CompleteAsync("Answer.");
        }

        Assert.Empty(environment.Requests);
        Assert.Equal(2, hosted.Count(activity => activity.Entities.Any(entity => entity.Type == "planInfo")));
        Assert.Contains(hosted, activity => activity.Type == ActivityTypes.Message && activity.Text == "Answer.");
    }

    [Theory]
    [InlineData("start")]
    [InlineData("continue")]
    [InlineData("stop")]
    [InlineData("error")]
    [InlineData("complete")]
    [InlineData("stream")]
    public async Task PlanDelivery_OrdinaryOperationsUseAdapter(string operation)
    {
        var adapter = new TrackingConnectorAdapter();
        using var environment = new PlanEnvironment(adapter: adapter);
        var plan = new TeamsTurnContext(environment.Context).CreatePlan();

        switch (operation)
        {
            case "start":
                await plan.StartAsync([NewStep()]);
                break;
            case "continue":
                await plan.UpdateAsync([NewStep()]);
                break;
            case "stop":
                await plan.StopAsync();
                break;
            case "error":
                await plan.ErrorAsync();
                break;
            case "complete":
                await plan.CompleteAsync("Answer.");
                break;
            case "stream":
                await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
                environment.Context.StreamingResponse.QueueTextChunk("Answer.");
                await plan.CompleteStreamingAsync();
                break;
        }

        Assert.Single(adapter.Activities, activity => activity.Entities.Any(entity => entity.Type == "planInfo"));
    }

    [Fact]
    public async Task PlanDelivery_ConcurrentBindingsRemainIsolated()
    {
        using var environment = new PlanEnvironment();
        var context = new TeamsTurnContext(environment.Context);
        var first = context.CreatePlan(new PlanOptions { ConversationId = "conversation", ReplyToId = "first-prompt" });
        var second = context.CreatePlan(new PlanOptions { ConversationId = "conversation", ReplyToId = "second-prompt" });
        var firstReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.OnSendActivities(async (turn, activities, next) =>
        {
            if (activities[0].Text == "First.")
            {
                firstReached.TrySetResult(true);
                await releaseFirst.Task;
            }
            return await next();
        });

        var pendingFirst = first.CompleteAsync("First.");
        await firstReached.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        try
        {
            await second.CompleteAsync("Second.");
        }
        finally
        {
            releaseFirst.TrySetResult(true);
        }
        await pendingFirst;

        var sent = environment.Requests.Select(request => ProtocolJsonSerializer.ToObject<Activity>(request.Body)).ToArray();
        Assert.Equal("first-prompt", Assert.Single(sent, activity => activity.Text == "First.").ReplyToId);
        Assert.Equal("second-prompt", Assert.Single(sent, activity => activity.Text == "Second.").ReplyToId);
    }

    [Fact]
    public async Task PlanDelivery_ReplyChainRunsLateHooksAndDoesNotCaptureLaterSends()
    {
        var adapter = new TrackingConnectorAdapter();
        using var environment = new PlanEnvironment(adapter: adapter);
        var context = new TeamsTurnContext(environment.Context);
        var plan = context.CreatePlan(new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" }
        });
        await plan.StartAsync([NewStep()]);
        context.OnSendActivities((turn, activities, next) =>
        {
            foreach (var activity in activities.Where(activity => activity.Type == ActivityTypes.Message))
            {
                activity.Text += " Modified.";
            }
            return next();
        });

        var response = await plan.CompleteAsync("Answer.");
        await context.SendActivityAsync("Ordinary.");

        Assert.Equal("service-message", response.Id);
        var completion = Assert.Single(environment.Requests, request => request.Uri.AbsolutePath.Contains("/replyChains/"));
        Assert.Equal("Answer. Modified.", ProtocolJsonSerializer.ToObject<Activity>(completion.Body).Text);
        Assert.Equal("original-prompt", ProtocolJsonSerializer.ToObject<Activity>(completion.Body).ReplyToId);
        Assert.DoesNotContain(adapter.Activities, activity => activity.Text == "Answer. Modified.");
        Assert.Contains(adapter.Activities, activity => activity.Text == "Ordinary. Modified.");
    }

    [Fact]
    public async Task PlanDelivery_ReplyChainCompletionUpdatesResponseState()
    {
        using var environment = new PlanEnvironment();
        var context = new TeamsTurnContext(environment.Context);
        var plan = context.CreatePlan(new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" }
        });
        var logger = new RecordingTranscriptLogger();

        await new TranscriptLoggerMiddleware(logger).OnTurnAsync(context, async cancellationToken =>
        {
            await plan.CompleteAsync("Answer.", cancellationToken);
        }, CancellationToken.None);

        Assert.True(environment.Context.Responded);
        Assert.Equal("service-message", Assert.Single(logger.Activities,
            activity => activity.Type == ActivityTypes.Message && activity.Text == "Answer.").Id);
    }

    [Fact]
    public async Task PlanDelivery_ExpiredChatStreamRecoversThroughReplyChainHook()
    {
        var completions = 0;
        using var environment = new PlanEnvironment(request =>
            IsMessagePost(request) && ++completions == 1
                ? StreamTimeoutResponse()
                : Response(HttpStatusCode.OK, """{"id":"service-message"}"""));
        var plan = new TeamsTurnContext(environment.Context).CreatePlan(new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" }
        });
        await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        environment.Context.StreamingResponse.QueueTextChunk("Answer.");

        Assert.Equal(StreamingResponseResult.Success, await plan.CompleteStreamingAsync());

        var attempts = environment.Requests.Where(IsMessagePost).ToArray();
        Assert.Equal(2, attempts.Length);
        Assert.Equal("/teams/v3/conversations/conversation/activities/prompt", attempts[0].Uri.AbsolutePath);
        Assert.Equal("/teams/v3/conversations/chat/replyChains/root", attempts[1].Uri.AbsolutePath);
        var recovered = ProtocolJsonSerializer.ToObject<Activity>(attempts[1].Body);
        Assert.Null(recovered.GetStreamingEntity());
        Assert.Equal("Answer.", recovered.Text);
        Assert.Equal("complete", Assert.Single(recovered.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
    }

    [Fact]
    public async Task CustomContext_DefaultFactoryUsesItsCoreActivityAndTransport()
    {
        using var environment = new PlanEnvironment();
        var customContext = new Mock<ITeamsTurnContext> { CallBase = true };
        var coreContext = customContext.As<ITurnContext>();
        coreContext.SetupGet(context => context.Activity).Returns(environment.Context.Activity);
        coreContext.SetupGet(context => context.Services).Returns(environment.Context.Services);
        coreContext.Setup(context => context.OnSendActivities(It.IsAny<SendActivitiesHandler>()))
            .Returns((SendActivitiesHandler handler) => environment.Context.OnSendActivities(handler));
        coreContext.Setup(context => context.SendActivitiesAsync(It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
            .Returns((IActivity[] activities, CancellationToken token) =>
            {
                foreach (var activity in activities)
                {
                    activity.Properties["customContext"] = JsonSerializer.SerializeToElement(true);
                }
                return environment.Context.SendActivitiesAsync(activities, token);
            });

        var response = await customContext.Object.CreatePlan().CompleteAsync("Answer.");

        Assert.Equal("service-message", response.Id);
        var request = Assert.Single(environment.Requests);
        Assert.Equal("/teams/v3/conversations/conversation/activities/prompt", request.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(request.Body);
        Assert.True(body.RootElement.GetProperty("customContext").GetBoolean());
        Assert.Equal("prompt", body.RootElement.GetProperty("replyToId").GetString());
        Assert.Equal("complete", GetPlanEntity(body.RootElement).GetProperty("planType").GetString());
    }

    [Fact]
    public async Task StreamingCompletion_UsesCustomWriterFinalDeliveryContract()
    {
        using var environment = new PlanEnvironment();
        using var cancellation = new CancellationTokenSource();
        var writer = new Mock<IStreamingResponse>();
        writer.Setup(response => response.EndStreamAsync(
            It.IsAny<Func<IActivity, CancellationToken, Task<ResourceResponse>>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<IActivity, CancellationToken, Task<ResourceResponse>> sender, CancellationToken token) =>
            {
                Assert.Equal(cancellation.Token, token);
                await sender(new Activity
                {
                    Type = ActivityTypes.Message,
                    Text = "Custom writer answer.",
                    Attachments = [new Attachment { ContentType = "text/plain", Content = "attachment" }]
                }, token);
                return StreamingResponseResult.Success;
            });
        var customContext = new Mock<ITeamsTurnContext> { CallBase = true };
        var coreContext = customContext.As<ITurnContext>();
        coreContext.SetupGet(context => context.Activity).Returns(environment.Context.Activity);
        coreContext.SetupGet(context => context.Services).Returns(environment.Context.Services);
        coreContext.SetupGet(context => context.StreamingResponse).Returns(writer.Object);
        coreContext.Setup(context => context.OnSendActivities(It.IsAny<SendActivitiesHandler>()))
            .Returns((SendActivitiesHandler handler) => environment.Context.OnSendActivities(handler));
        coreContext.Setup(context => context.SendActivitiesAsync(It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
            .Returns((IActivity[] activities, CancellationToken token) =>
                environment.Context.SendActivitiesAsync(activities, token));
        var plan = new TeamsPlan(customContext.Object, new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt"
        });

        var result = await plan.CompleteStreamingAsync(cancellation.Token);

        Assert.Equal(StreamingResponseResult.Success, result);
        var final = ProtocolJsonSerializer.ToObject<Activity>(Assert.Single(environment.Requests).Body);
        Assert.Equal("Custom writer answer.", final.Text);
        Assert.Equal("original-prompt", final.ReplyToId);
        Assert.Single(final.Attachments);
        Assert.Equal("complete", Assert.Single(final.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
    }

    [Fact]
    public async Task Update_SendsOnlySuppliedStepAndKeepsApplicationVersion()
    {
        using var environment = new PlanEnvironment();
        var plan = new TeamsPlan(environment.Context);
        await plan.StartAsync([NewStep(), NewStep("answer")]);
        var changed = NewStep("answer");
        changed.Version = 7;
        changed.Status = PlanStepStatuses.Completed;

        await plan.UpdateAsync([changed]);

        using var body = JsonDocument.Parse(environment.Requests.Last().Body);
        var step = Assert.Single(GetPlanEntity(body.RootElement).GetProperty("steps").EnumerateArray());
        Assert.Equal("answer", step.GetProperty("id").GetString());
        Assert.Equal(7, step.GetProperty("version").GetInt32());
        Assert.Equal("completed", step.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Start_SnapshotsMutableStepsBeforeAcquiringTransportCredentials()
    {
        var tokenRequested = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var token = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var environment = new PlanEnvironment(tokenProvider: () =>
        {
            tokenRequested.TrySetResult(true);
            return token.Task;
        });
        var plan = new TeamsPlan(environment.Context);
        var step = NewStep();
        step.Sources = [new StepSource { Title = "Original source", Url = new Uri("https://example.com/original") }];

        var sending = plan.StartAsync([step]);
        await tokenRequested.Task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System);
        step.Title = "Changed";
        step.Version = 2;
        step.Sources[0].Title = "Changed source";
        token.SetResult("connector-token");
        await sending;

        using var body = JsonDocument.Parse(Assert.Single(environment.Requests).Body);
        var snapshot = GetPlanEntity(body.RootElement).GetProperty("steps")[0];
        Assert.Equal("Outline", snapshot.GetProperty("title").GetString());
        Assert.Equal(1, snapshot.GetProperty("version").GetInt32());
        Assert.Equal("Original source", snapshot.GetProperty("sources")[0].GetProperty("title").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Complete_PostsCoreMessagePreservingContentAndCallerActivity(bool existingCompletion)
    {
        using var environment = new PlanEnvironment();
        var plan = new TeamsPlan(environment.Context);
        var message = new Activity
        {
            Type = ActivityTypes.Message,
            Text = "Answer.",
            ReplyToId = "not-the-prompt",
            Attachments = [new Attachment { ContentType = "text/plain", Content = "attachment" }],
            Entities = [new AIEntity { UsageInfo = new SensitivityUsageInfo { Name = "Internal" } }]
        };
        if (existingCompletion)
        {
            message.Entities.Add(new PlanInfoEntity { PlanType = PlanTypes.Complete });
        }
        var original = ProtocolJsonSerializer.ToJson(message);
        ResourceResponse notification = null;
        plan.Sent += (_, args) => notification = args.Response;

        var response = await plan.CompleteAsync(message);

        Assert.Equal("service-message", response.Id);
        Assert.Same(response, notification);
        Assert.Equal(original, ProtocolJsonSerializer.ToJson(message));
        var request = Assert.Single(environment.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/conversations/conversation/activities/prompt", request.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("prompt", body.RootElement.GetProperty("replyToId").GetString());
        Assert.Equal("Answer.", body.RootElement.GetProperty("text").GetString());
        Assert.Single(body.RootElement.GetProperty("attachments").EnumerateArray());
        Assert.Equal("complete", GetPlanEntity(body.RootElement).GetProperty("planType").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("entities").GetArrayLength());
    }

    [Fact]
    public async Task TopLevelChatBinding_UsesAdapterRatherThanReplyChainRoute()
    {
        var incoming = Incoming("100");
        incoming.ChannelData = JsonSerializer.Deserialize<JsonElement>(
            """{"chat":{"id":"chat"},"replyChain":{"id":"100"}}""");
        using var environment = new PlanEnvironment(incoming: incoming);

        await new TeamsPlan(environment.Context).CompleteAsync("Top-level answer.");

        Assert.Equal("/teams/v3/conversations/conversation/activities/100",
            Assert.Single(environment.Requests).Uri.AbsolutePath);
    }

    [Theory]
    [InlineData("message", "200", "card", "100", null, false)]
    [InlineData("message", "200", "card", "150", null, true)]
    [InlineData("messageReaction", "reaction", null, "opaque", null, false)]
    [InlineData("messageReaction", "reaction", null, "150", null, true)]
    [InlineData("messageUpdate", "100", null, null, null, false)]
    [InlineData("messageUpdate", "150", null, null, null, true)]
    [InlineData("invoke", "invoke", null, "100", null, false)]
    [InlineData("invoke", "invoke", null, "150", """{"messagePayload":{"id":"100"}}""", false)]
    [InlineData("invoke", "invoke", null, "100", """{"messagePayload":{"id":"150"}}""", true)]
    [InlineData("invoke", "invoke", null, null, null, true)]
    [InlineData("event", "event", null, null, null, false)]
    public async Task ChatBinding_DistinguishesInThreadActionsFromTopLevelMessages(
        string activityType, string activityId, string sourceName, string actedOnId, string valueJson, bool inThread)
    {
        var incoming = Incoming(activityId);
        incoming.Type = activityType;
        incoming.ReplyToId = actedOnId;
        incoming.Value = valueJson == null ? null : JsonSerializer.Deserialize<JsonElement>(valueJson);
        incoming.ChannelData = JsonSerializer.SerializeToElement(new
        {
            chat = new { id = "chat" },
            replyChain = new { id = "100" },
            source = sourceName == null ? null : new { name = sourceName }
        });
        using var environment = new PlanEnvironment(incoming: incoming);

        await new TeamsPlan(environment.Context).CompleteAsync("Answer.");

        var request = Assert.Single(environment.Requests);
        Assert.Equal(inThread
            ? "/teams/v3/conversations/chat/replyChains/100"
            : $"/teams/v3/conversations/conversation/activities/{activityId}", request.Uri.AbsolutePath);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal(activityId, body.RootElement.GetProperty("replyToId").GetString());
    }

    [Fact]
    public async Task RestoredChatBinding_UsesOriginalPromptAndSavedReplyChainWithFreshTransport()
    {
        var incoming = Incoming();
        incoming.Conversation.Id = "full;messageid=opaque";
        incoming.ReplyToId = "not-the-original-prompt";
        incoming.ChannelData = JsonSerializer.Deserialize<JsonElement>(
            """{"chat":{"id":"chat/id"},"replyChain":{"id":"root/id"}}""");
        using var original = new PlanEnvironment(incoming: incoming);
        var originalContext = new TeamsTurnContext(original.Context);
        var plan = originalContext.CreatePlan();
        await plan.StartAsync([NewStep()]);
        var savedBinding = ProtocolJsonSerializer.ToJson(plan.Options);
        using var resumed = new PlanEnvironment(incoming: Incoming("later-event", "full;messageid=opaque"));
        ITeamsTurnContext resumedContext = new TeamsTurnContext(resumed.Context);
        var restored = resumedContext.CreatePlan(ProtocolJsonSerializer.ToObject<PlanOptions>(savedBinding));

        await restored.CompleteAsync("Delayed answer.");

        var progress = Assert.Single(original.Requests);
        Assert.Equal("/teams/v3/conversations/full;messageid=opaque/activities/prompt", Uri.UnescapeDataString(progress.Uri.AbsolutePath));
        var completion = Assert.Single(resumed.Requests);
        Assert.Equal(HttpMethod.Post, completion.Method);
        Assert.Equal("https://regional.example/teams/v3/conversations/chat%2Fid/replyChains/root%2Fid", completion.Uri.AbsoluteUri);
        using var body = JsonDocument.Parse(completion.Body);
        Assert.Equal("prompt", body.RootElement.GetProperty("replyToId").GetString());
        Assert.Equal("full;messageid=opaque", body.RootElement.GetProperty("conversation").GetProperty("id").GetString());
    }

    [Fact]
    public async Task ChannelBinding_KeepsFullConversationRatherThanUsingChatReplyChain()
    {
        var incoming = Incoming(conversationId: "channel;messageid=thread");
        incoming.ChannelData = JsonSerializer.Deserialize<JsonElement>(
            """{"channel":{"id":"channel"},"chat":{"id":"not-a-chat-destination"},"replyChain":{"id":"root"}}""");
        using var environment = new PlanEnvironment(incoming: incoming);

        await new TeamsPlan(environment.Context).CompleteAsync("Channel answer.");

        var request = Assert.Single(environment.Requests);
        Assert.Equal("/teams/v3/conversations/channel;messageid=thread/activities/prompt", Uri.UnescapeDataString(request.Uri.AbsolutePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlanConflict_PreservesSdkErrorAndDoesNotRetry(bool chatThread)
    {
        var incoming = Incoming();
        if (chatThread)
        {
            incoming.ChannelData = JsonSerializer.Deserialize<JsonElement>(
                """{"chat":{"id":"chat"},"replyChain":{"id":"root"}}""");
        }
        using var environment = new PlanEnvironment(
            _ => Response(HttpStatusCode.Conflict, """{"error":{"code":"PlanConflict","message":"Already completed."}}"""),
            incoming: incoming);

        var exception = await Assert.ThrowsAsync<ErrorResponseException>(() =>
            new TeamsPlan(environment.Context).CompleteAsync("Answer."));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("PlanConflict", exception.Body.Error.Code);
        Assert.Equal("Already completed.", exception.Body.Error.Message);
        Assert.Single(environment.Requests);
    }

    [Theory]
    [InlineData("connector", -100009)]
    [InlineData("transport", -100010)]
    [InlineData("endpoint", -100010)]
    public async Task ReplyChainTransport_PrerequisiteFailureHasMSTeamsDiagnostics(string missing, int expectedCode)
    {
        using var environment = new PlanEnvironment();
        if (missing == "connector")
        {
            environment.Context.Services.TryRemove(typeof(IConnectorClient).FullName, out _);
        }
        else
        {
            var connector = new Mock<IConnectorClient>();
            if (missing == "endpoint")
            {
                connector.As<IRestTransport>().SetupGet(transport => transport.Endpoint).Returns((Uri)null);
            }
            environment.Context.Services.Set<IConnectorClient>(connector.Object);
        }
        var plan = new TeamsPlan(environment.Context, new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" }
        });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => plan.CompleteAsync("Answer."));

        Assert.Equal(expectedCode, error.HResult);
        Assert.Equal($"https://aka.ms/M365AgentsErrorCodes/#{expectedCode}", error.HelpLink);
        Assert.Empty(environment.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplyChainTransport_HttpFailurePreservesServiceErrorAndAddsMSTeamsDiagnostics(bool malformedBody)
    {
        using var environment = new PlanEnvironment(_ => Response(HttpStatusCode.BadGateway,
            malformedBody ? "not-json" : """{"error":{"code":"UpstreamFailure","message":"Unavailable."}}"""));
        var plan = new TeamsPlan(environment.Context, new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" }
        });

        var error = await Assert.ThrowsAsync<ErrorResponseException>(() => plan.CompleteAsync("Answer."));

        Assert.Equal(-100011, error.HResult);
        Assert.Equal("https://aka.ms/M365AgentsErrorCodes/#-100011", error.HelpLink);
        Assert.Equal(502, error.StatusCode);
        if (malformedBody)
        {
            Assert.Null(error.Body);
        }
        else
        {
            Assert.Equal("UpstreamFailure", error.Body.Error.Code);
            Assert.Equal("Unavailable.", error.Body.Error.Message);
        }
        Assert.Single(environment.Requests);
    }

    [Fact]
    public void PlanEntity_InvalidLifecycleOperationHasMSTeamsDiagnostics()
    {
        var error = Assert.Throws<JsonException>(() => new PlanInfoEntity { PlanType = null });

        Assert.Equal(-100012, error.HResult);
        Assert.Equal("https://aka.ms/M365AgentsErrorCodes/#-100012", error.HelpLink);
    }

    [Fact]
    public void PlanCreation_NonTeamsTurnHasMSTeamsDiagnostics()
    {
        var incoming = Incoming();
        incoming.ChannelId = CoreChannels.Webchat;
        using var environment = new PlanEnvironment(incoming: incoming);

        var error = Assert.Throws<ArgumentException>(() => new TeamsPlan(environment.Context));

        Assert.Equal("turnContext", error.ParamName);
        Assert.Equal(-100002, error.HResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TargetedPrompt_IsRejectedBeforeAnySend(bool activityTreatment)
    {
        var incoming = Incoming();
        if (activityTreatment)
        {
            incoming.MakeTargetedActivity();
        }
        else
        {
            incoming.Recipient.Properties["isTargeted"] = JsonSerializer.SerializeToElement(true);
        }
        using var environment = new PlanEnvironment(incoming: incoming);

        var error = Assert.Throws<ArgumentException>(() => new TeamsPlan(environment.Context));

        Assert.Equal("turnContext", error.ParamName);
        Assert.Equal(-100003, error.HResult);
        Assert.Empty(environment.Requests);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TargetedCompletion_IsRejectedBeforeAnySend(bool untypedTreatment, bool chatThread)
    {
        var incoming = Incoming();
        if (chatThread)
        {
            incoming.ChannelData = JsonSerializer.Deserialize<JsonElement>(
                """{"chat":{"id":"chat"},"replyChain":{"id":"root"}}""");
        }
        using var environment = new PlanEnvironment(incoming: incoming);
        var message = Activity.CreateMessageActivity().WithText("Private.").WithTargetedRecipient(new ChannelAccount("other"));
        if (untypedTreatment)
        {
            message.Entities =
            [
                new Entity("activityTreatment")
                {
                    Properties = { ["treatment"] = JsonSerializer.SerializeToElement("targeted") }
                }
            ];
            Assert.False(message.IsTargetedActivity());
        }
        var original = ProtocolJsonSerializer.ToJson(message);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => new TeamsPlan(environment.Context).CompleteAsync(message));

        Assert.Equal("message", error.ParamName);
        Assert.Equal(-100007, error.HResult);
        Assert.Empty(environment.Requests);
        Assert.Equal(original, ProtocolJsonSerializer.ToJson(message));
    }

    [Theory]
    [InlineData("typing", "complete", -100006)]
    [InlineData("message", "start", -100008)]
    public async Task InvalidCompletion_DoesNotSendNonMessageOrNonCompleteOperation(string activityType, string operation, int expectedCode)
    {
        using var environment = new PlanEnvironment();
        var message = new Activity
        {
            Type = activityType,
            Text = "Answer.",
            Entities = [new Entity("planInfo") { Properties = { ["planType"] = JsonSerializer.SerializeToElement(operation) } }]
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => new TeamsPlan(environment.Context).CompleteAsync(message));

        Assert.Equal("message", error.ParamName);
        Assert.Equal(expectedCode, error.HResult);
        Assert.Empty(environment.Requests);
    }

    [Theory]
    [InlineData("", "root")]
    [InlineData("chat", "")]
    [InlineData(".", "root")]
    [InlineData("chat", "..")]
    public void InvalidSavedReplyChain_IsRejected(string chatId, string rootId)
    {
        using var environment = new PlanEnvironment();
        var options = new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "prompt",
            ReplyChain = new PlanReplyChain { ChatOrChannelId = chatId, ReplyChainId = rootId }
        };

        Assert.Throws<ArgumentException>(() => new TeamsPlan(environment.Context, options));

        Assert.Empty(environment.Requests);
    }

    [Fact]
    public void SavedBindingForAnotherConversation_IsRejected()
    {
        using var environment = new PlanEnvironment();

        var error = Assert.Throws<ArgumentException>(() => new TeamsPlan(environment.Context, new PlanOptions
        {
            ConversationId = "another-conversation",
            ReplyToId = "prompt"
        }));

        Assert.Equal("options", error.ParamName);
        Assert.Equal(-100004, error.HResult);
        Assert.Empty(environment.Requests);
    }

    [Fact]
    public async Task CanceledLifecycle_DoesNotPost()
    {
        using var environment = new PlanEnvironment();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new TeamsPlan(environment.Context).StartAsync([NewStep()], cancellation.Token));

        Assert.Empty(environment.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamingCompletion_PostsComposedCoreOutputWithOriginalBinding(bool restoredChat)
    {
        using var environment = new PlanEnvironment();
        var binding = new PlanOptions
        {
            ConversationId = "conversation",
            ReplyToId = "original-prompt",
            ReplyChain = restoredChat ? new PlanReplyChain { ChatOrChannelId = "chat", ReplyChainId = "root" } : null
        };
        var plan = new TeamsPlan(environment.Context, binding);
        ResourceResponse notification = null;
        plan.Sent += (_, args) => notification = args.Response;
        var stream = environment.Context.StreamingResponse;
        stream.EnableGeneratedByAILabel = true;
        stream.SensitivityLabel = new SensitivityUsageInfo { Name = "Internal" };
        stream.FeedbackLoopEnabled = true;
        stream.AddAttachment(new Attachment { ContentType = "text/plain", Content = "attachment" });
        stream.AddCitation(new Citation("Reference text", "Reference", "https://example.com/reference"), 1);
        await stream.QueueInformativeUpdateAsync("Working.");
        stream.QueueTextChunk("Answer [doc1].");

        var result = await plan.CompleteStreamingAsync();

        Assert.Equal(StreamingResponseResult.Success, result);
        var request = Assert.Single(environment.Requests, IsMessagePost);
        Assert.Equal("/teams/v3/conversations/conversation/activities/original-prompt", request.Uri.AbsolutePath);
        var final = ProtocolJsonSerializer.ToObject<Activity>(request.Body);
        Assert.Equal("original-prompt", final.ReplyToId);
        Assert.Equal("Answer [1].", final.Text);
        Assert.Single(final.Attachments);
        Assert.Equal("complete", Assert.Single(final.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
        var ai = Assert.Single(final.Entities.OfType<AIEntity>());
        Assert.Equal("Internal", ai.UsageInfo.Name);
        Assert.Equal("Reference", Assert.Single(ai.Citation).Appearance.Name);
        Assert.Contains("AIGeneratedContent", ai.AdditionalType);
        Assert.Equal("default", final.GetChannelData<JsonElement>().GetProperty("feedbackLoop").GetProperty("type").GetString());
        Assert.Equal(StreamTypes.Final, final.GetStreamingEntity().StreamType);
        Assert.Equal("service-message", final.GetStreamingEntity().StreamId);
        Assert.Equal("service-message", notification.Id);
    }

    [Fact]
    public async Task StreamingFinalTimeout_RecoversWithOneOrdinaryCompletionPostAndRetainsMetadata()
    {
        var completionAttempts = 0;
        using var environment = new PlanEnvironment(request =>
            IsMessagePost(request) && ++completionAttempts == 1
                ? StreamTimeoutResponse()
                : Response(HttpStatusCode.OK, """{"id":"completed-message"}"""));
        var plan = new TeamsPlan(environment.Context);
        var stream = environment.Context.StreamingResponse;
        stream.SensitivityLabel = new SensitivityUsageInfo { Name = "Internal" };
        stream.AddAttachment(new Attachment { ContentType = "text/plain", Content = "attachment" });
        await stream.QueueInformativeUpdateAsync("Working.");
        stream.QueueTextChunk("Complete answer.");

        var result = await plan.CompleteStreamingAsync();

        Assert.Equal(StreamingResponseResult.Success, result);
        var completions = environment.Requests.Where(IsMessagePost).ToArray();
        Assert.Equal(2, completions.Length);
        Assert.NotNull(ProtocolJsonSerializer.ToObject<Activity>(completions[0].Body).GetStreamingEntity());
        var recovered = ProtocolJsonSerializer.ToObject<Activity>(completions[1].Body);
        Assert.Null(recovered.Id);
        Assert.Null(recovered.GetStreamingEntity());
        Assert.Equal("prompt", recovered.ReplyToId);
        Assert.Equal("Complete answer.", recovered.Text);
        Assert.Single(recovered.Attachments);
        Assert.Equal("Internal", Assert.Single(recovered.Entities.OfType<AIEntity>()).UsageInfo.Name);
        Assert.Equal("complete", Assert.Single(recovered.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
        Assert.DoesNotContain(environment.Requests, request => request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task EarlierStreamingTimeout_PostsCompletionRatherThanUsingFinalPutFallback()
    {
        var streamSends = 0;
        using var environment = new PlanEnvironment(request =>
        {
            using var body = JsonDocument.Parse(request.Body);
            return body.RootElement.GetProperty("type").GetString() == "typing" && ++streamSends > 1
                ? StreamTimeoutResponse()
                : Response(HttpStatusCode.OK, """{"id":"service-message"}""");
        });
        var plan = new TeamsPlan(environment.Context);
        var stream = environment.Context.StreamingResponse;
        await stream.QueueInformativeUpdateAsync("Working.");
        stream.QueueTextChunk("Complete answer.");
        await stream.QueueInformativeUpdateAsync("Finishing.");

        var result = await plan.CompleteStreamingAsync();

        Assert.Equal(StreamingResponseResult.Success, result);
        var request = Assert.Single(environment.Requests, IsMessagePost);
        var final = ProtocolJsonSerializer.ToObject<Activity>(request.Body);
        Assert.Equal("Complete answer.", final.Text);
        Assert.Null(final.GetStreamingEntity());
        Assert.Equal("complete", Assert.Single(final.Entities.OfType<PlanInfoEntity>()).PlanType.Value);
        var checkpoint = Assert.Single(environment.Requests, entry => entry.Method == HttpMethod.Put);
        Assert.DoesNotContain(ProtocolJsonSerializer.ToObject<Activity>(checkpoint.Body).Entities,
            entity => entity.Type == "planInfo");
        Assert.Same(request, environment.Requests.Last());
    }

    [Theory]
    [InlineData(409, "PlanConflict")]
    [InlineData(400, "ContentStreamNotAllowed")]
    [InlineData(401, "Unauthorized")]
    public async Task StreamingCompletionRejection_PropagatesWithoutRetry(int status, string errorCode)
    {
        using var environment = new PlanEnvironment(request => IsMessagePost(request)
            ? Response((HttpStatusCode)status, $$$"""{"error":{"code":"{{{errorCode}}}","message":"Rejected."}}""")
            : Response(HttpStatusCode.OK, """{"id":"service-message"}"""));
        var plan = new TeamsPlan(environment.Context);
        await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        environment.Context.StreamingResponse.QueueTextChunk("Answer.");

        var failure = await Record.ExceptionAsync(() => plan.CompleteStreamingAsync());
        var exception = Assert.IsType<ErrorResponseException>(status == 401
            ? Assert.IsType<OperationCanceledException>(failure).InnerException
            : failure);

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(errorCode, exception.Body.Error.Code);
        Assert.Single(environment.Requests, IsMessagePost);
    }

    [Fact]
    public async Task AmbiguousStreamingCompletionFailure_DoesNotRetry()
    {
        using var environment = new PlanEnvironment(request => IsMessagePost(request)
            ? throw new HttpRequestException("Connection lost after sending.")
            : Response(HttpStatusCode.OK, """{"id":"service-message"}"""));
        var plan = new TeamsPlan(environment.Context);
        await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        environment.Context.StreamingResponse.QueueTextChunk("Answer.");

        await Assert.ThrowsAsync<HttpRequestException>(() => plan.CompleteStreamingAsync());

        Assert.Single(environment.Requests, IsMessagePost);
    }

    [Fact]
    public async Task FailedCompletionRecovery_PropagatesConflictWithoutAThirdPost()
    {
        var completionAttempts = 0;
        using var environment = new PlanEnvironment(request => !IsMessagePost(request)
            ? Response(HttpStatusCode.OK, """{"id":"service-message"}""")
            : ++completionAttempts == 1
                ? StreamTimeoutResponse()
                : Response(HttpStatusCode.Conflict, """{"error":{"code":"PlanConflict","message":"Already completed."}}"""));
        var plan = new TeamsPlan(environment.Context);
        await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        environment.Context.StreamingResponse.QueueTextChunk("Answer.");

        var exception = await Assert.ThrowsAsync<ErrorResponseException>(() => plan.CompleteStreamingAsync());

        Assert.Equal("PlanConflict", exception.Body.Error.Code);
        Assert.Equal(2, environment.Requests.Count(IsMessagePost));
    }

    [Fact]
    public async Task RepeatedStreamingCompletion_DoesNotPostAgain()
    {
        using var environment = new PlanEnvironment();
        var plan = new TeamsPlan(environment.Context);
        await environment.Context.StreamingResponse.QueueInformativeUpdateAsync("Working.");
        environment.Context.StreamingResponse.QueueTextChunk("Answer.");

        Assert.Equal(StreamingResponseResult.Success, await plan.CompleteStreamingAsync());
        Assert.Equal(StreamingResponseResult.AlreadyEnded, await plan.CompleteStreamingAsync());

        Assert.Single(environment.Requests, IsMessagePost);
    }

    private static bool IsMessagePost(RecordedRequest request)
    {
        using var body = JsonDocument.Parse(request.Body);
        return request.Method == HttpMethod.Post && body.RootElement.GetProperty("type").GetString() == "message";
    }

    private static HttpResponseMessage StreamTimeoutResponse() => Response(HttpStatusCode.BadRequest,
        """{"error":{"code":"ContentStreamNotAllowed","message":"Content stream finished due to exceeded streaming time."}}""");

    private static PlanStep NewStep(string id = "outline") => new()
    {
        Id = id,
        Title = "Outline",
        Version = 1,
        Status = PlanStepStatuses.Active
    };

    private static Activity Incoming(string activityId = "prompt", string conversationId = "conversation") => new()
    {
        Type = ActivityTypes.Message,
        ChannelId = CoreChannels.Msteams,
        Id = activityId,
        ServiceUrl = "https://regional.example/teams/",
        Conversation = new ConversationAccount(id: conversationId),
        From = new ChannelAccount("user"),
        Recipient = new ChannelAccount("agent")
    };

    private static JsonElement GetPlanEntity(JsonElement activity) => Assert.Single(
        activity.GetProperty("entities").EnumerateArray(), entity => entity.GetProperty("type").GetString() == "planInfo");

    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string Authorization);

    private sealed class RecordingTranscriptLogger : ITranscriptLogger
    {
        public ConcurrentQueue<IActivity> Activities { get; } = new();

        public Task LogActivityAsync(IActivity activity)
        {
            Activities.Enqueue(activity);
            return Task.CompletedTask;
        }
    }

    private sealed class PlanEnvironment : IDisposable
    {
        private readonly RecordingHandler _handler;

        public PlanEnvironment(
            Func<RecordedRequest, HttpResponseMessage> respond = null,
            Activity incoming = null,
            Func<Task<string>> tokenProvider = null,
            ChannelServiceAdapterBase adapter = null)
        {
            _handler = new RecordingHandler(respond ?? (_ => Response(HttpStatusCode.OK, """{"id":"service-message"}""")));
            var factory = new RecordingClientFactory(_handler);
            var connector = new RestConnectorClient(
                new Uri("https://regional.example/teams/"),
                factory,
                tokenProvider ?? (() => Task.FromResult("connector-token")));
            Context = new BuilderTurnContext(adapter ?? new ConnectorAdapter(), incoming ?? Incoming(), new ClaimsIdentity());
            Context.Services.Set<IConnectorClient>(connector);
            Context.Services.Set<IUserTokenClient>(new RestUserTokenClient(
                "agent", new Uri("https://token.example/"), factory, null));
            Context.StreamingResponse.InitialDelay = 1;
            Context.StreamingResponse.Interval = 1;
            Context.StreamingResponse.EndStreamTimeout = 5000;
        }

        public BuilderTurnContext Context { get; }
        public RecordedRequest[] Requests => _handler.Requests.ToArray();
        public HttpClient CreateHttpClient() => new(_handler, disposeHandler: false);

        public void Dispose()
        {
            Context.Dispose();
            _handler.Dispose();
        }
    }

    private sealed class ConnectorAdapter() : ChannelServiceAdapterBase(new Mock<IChannelServiceClientFactory>().Object);

    private sealed class TrackingConnectorAdapter()
        : ChannelServiceAdapterBase(new Mock<IChannelServiceClientFactory>().Object)
    {
        public ConcurrentQueue<IActivity> Activities { get; } = new();

        public override Task<ResourceResponse[]> SendActivitiesAsync(
            ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
        {
            foreach (var activity in activities)
            {
                Activities.Enqueue(activity.Clone());
            }
            return base.SendActivitiesAsync(turnContext, activities, cancellationToken);
        }
    }

    private sealed class HostedConnectorAdapter(List<IActivity> hosted)
        : ChannelServiceAdapterBase(new Mock<IChannelServiceClientFactory>().Object)
    {
        protected override Task<bool> HostResponseAsync(
            IActivity incomingActivity, IActivity outActivity, CancellationToken cancellationToken)
        {
            hosted.Add(outActivity.Clone());
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri,
                await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.ToString());
            Requests.Enqueue(recorded);
            return respond(recorded);
        }
    }
}

#pragma warning restore ExperimentalTeamsPlan
