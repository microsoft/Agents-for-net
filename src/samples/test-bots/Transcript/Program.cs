// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.Agents.Storage.Transcript;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the Agent
builder.AddAgent(sp =>
{
    var app = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());
    var transcriptLogger = new FileTranscriptLogger("./transcripts");
    var traceTranscript = new TraceTranscriptLogger();
    var transcript = new ConcurrentQueue<IActivity>();

    app.OnBeforeTurn(async (turnContext, turnState, cancellationToken) =>
    {
        static void LogActivity(ConcurrentQueue<IActivity> transcript, IActivity activity)
        {
            activity.Timestamp ??= DateTime.UtcNow;
            transcript.Enqueue(activity);
        }

        // log incoming activity at beginning of turn
        if (turnContext.Activity != null)
        {
            turnContext.Activity.From ??= new ChannelAccount();

            if (string.IsNullOrEmpty(turnContext.Activity.From.Role))
            {
                turnContext.Activity.From.Role = RoleTypes.User;
            }

            // We should not log ContinueConversation events used by Agents to initialize the middleware.
            if (!(turnContext.Activity.Type == ActivityTypes.Event && turnContext.Activity.Name == ActivityEventNames.ContinueConversation))
            {
                LogActivity(transcript,turnContext.Activity.Clone());
                await traceTranscript.LogActivityAsync(turnContext.Activity);
            }
        }

        // hook up onSend pipeline
        turnContext.OnSendActivities(async (ctx, activities, nextSend) =>
        {
            // run full pipeline
            var responses = await nextSend().ConfigureAwait(false);

            foreach (var activity in activities)
            {
                LogActivity(transcript, activity.Clone());
                await traceTranscript.LogActivityAsync(activity);
            }

            return responses;
        });

        // hook up update activity pipeline
        turnContext.OnUpdateActivity(async (ctx, activity, nextUpdate) =>
        {
            // run full pipeline
            var response = await nextUpdate().ConfigureAwait(false);

            // add Message Update activity
            var updateActivity = activity.Clone();
            updateActivity.Type = ActivityTypes.MessageUpdate;
            LogActivity(transcript, updateActivity);
            await traceTranscript.LogActivityAsync(updateActivity);
            return response;
        });

        // hook up delete activity pipeline
        turnContext.OnDeleteActivity(async (ctx, reference, nextDelete) =>
        {
            await nextDelete().ConfigureAwait(false);

            // add MessageDelete activity
            // log as MessageDelete activity
            var deleteActivity = new Activity
            {
                Type = ActivityTypes.MessageDelete,
                Id = reference.ActivityId,
            }
                .ApplyConversationReference(reference, isIncoming: false);

            LogActivity(transcript, deleteActivity);
            await traceTranscript.LogActivityAsync(deleteActivity);
        });

        return true;
    });

    // FLush the transcript logger
    app.OnAfterTurn(async (turnContext, turnState, cancellationToken) =>
    {
        try
        {
            while (!transcript.IsEmpty)
            {
                if (transcript.TryDequeue(out var activity))
                {
                    // Process the queue and log all the activities in parallel.
                    await transcriptLogger.LogActivityAsync(activity).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"Transcript logActivity failed with {ex}");
        }

        return true;
    });

    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"Received: Activity '{turnContext.Activity.Type}', Text '{turnContext.Activity.Text}'", cancellationToken: cancellationToken);
    });

    return app;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

// Setup port and listening address.
var port = args.Length > 0 ? args[0] : "3978";
app.Urls.Add($"http://localhost:{port}");

// Start listening. 
await app.RunAsync();
