// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System;
using System.Net.Http;
using Microsoft.Agents.Core.Models.Activities; // Added for outbound HTTP call

namespace OTelAgent;

public class MyAgent : AgentApplication
{
    private static readonly ActivitySource ActivitySource = new("OTelAgent.MyAgent");
    private readonly IHttpClientFactory _httpClientFactory; // Injected factory

    public MyAgent(AgentApplicationOptions options, IHttpClientFactory httpClientFactory) : base(options)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity<IMessageActivity>(OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext<IConversationUpdateActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("agent.welcome_message");

        try
        {
            activity?.SetTag("conversation.id", turnContext.Activity.Conversation?.Id);
            activity?.SetTag("channel.id", turnContext.Activity.ChannelId?.ToString());
            activity?.SetTag("members.added.count", turnContext.Activity.MembersAdded?.Count ?? 0);

            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    activity?.AddEvent(new ActivityEvent("member.added", DateTimeOffset.UtcNow, new()
                    {
                        ["member.id"] = member.Id,
                        ["member.name"] = member.Name
                    }));

                    await turnContext.SendActivityAsync(new MessageActivity("Hello and Welcome!"), cancellationToken);
                }
            }

            AgentTelemetry.RouteExecutedCounter.Add(1,
                new("route.type", "welcome_message"),
                new("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown"));

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));
            throw;
        }
    }

    private async Task OnMessageAsync(ITurnContext<IMessageActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("agent.message_handler");
        var routeStopwatch = Stopwatch.StartNew();

        try
        {
            activity?.SetTag("conversation.id", turnContext.Activity.Conversation?.Id);
            activity?.SetTag("channel.id", turnContext.Activity.ChannelId?.ToString());
            activity?.SetTag("message.text.length", turnContext.Activity.Text?.Length ?? 0);
            activity?.SetTag("user.id", turnContext.Activity.From?.Id);

            activity?.AddEvent(new ActivityEvent("message.received", DateTimeOffset.UtcNow, new()
            {
                ["message.id"] = turnContext.Activity.Id,
                ["message.text"] = turnContext.Activity.Text,
                ["user.id"] = turnContext.Activity.From?.Id,
                ["channel.id"] = turnContext.Activity.ChannelId?.ToString()
            }));

            // OUTBOUND HTTP CALL replacing blocking sleeps
            activity?.AddEvent(new ActivityEvent("external_call.started", DateTimeOffset.UtcNow, new()
            {
                ["http.target"] = "https://www.bing.com"
            }));

            var httpClient = _httpClientFactory.CreateClient();
            var httpStopwatch = Stopwatch.StartNew();
            using var response = await httpClient.GetAsync("https://www.bing.com", cancellationToken);
            httpStopwatch.Stop();

            activity?.AddEvent(new ActivityEvent("external_call.completed", DateTimeOffset.UtcNow, new()
            {
                ["http.status_code"] = (int)response.StatusCode,
                ["http.elapsed_ms"] = httpStopwatch.ElapsedMilliseconds
            }));

            activity?.SetTag("processing.type", "http_request");
            activity?.SetTag("processing.duration_ms", httpStopwatch.ElapsedMilliseconds);
            activity?.SetTag("external.status_code", (int)response.StatusCode);

            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
            activity?.AddEvent(new ActivityEvent("response.sent", DateTimeOffset.UtcNow));

            routeStopwatch.Stop();

            AgentTelemetry.MessageProcessingDuration.Record(routeStopwatch.ElapsedMilliseconds,
                new("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown"),
                new("channel.id", turnContext.Activity.ChannelId?.ToString() ?? "unknown"));

            AgentTelemetry.RouteExecutedCounter.Add(1,
                new("route.type", "message_handler"),
                new("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown"));

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            routeStopwatch.Stop();

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", DateTimeOffset.UtcNow, new()
            {
                ["exception.type"] = ex.GetType().FullName,
                ["exception.message"] = ex.Message,
                ["exception.stacktrace"] = ex.StackTrace
            }));

            AgentTelemetry.MessageProcessingDuration.Record(routeStopwatch.ElapsedMilliseconds,
                new("conversation.id", turnContext.Activity.Conversation?.Id ?? "unknown"),
                new("channel.id", turnContext.Activity.ChannelId?.ToString() ?? "unknown"),
                new("status", "error"));

            throw;
        }
    }
}