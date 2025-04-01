// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();


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
    const string MCSConversationPropertyName = "conversation.MCSConversationId";

    var app = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());

    CopilotClient GetClient(AgentApplication app)
    {
        return new CopilotClient(
            new ConnectionSettings(builder.Configuration.GetSection("CopilotStudioAgent")),
            sp.GetService<IHttpClientFactory>(),
            tokenProviderFunction: (s) =>
            {
                // The result of a sign in is cached in Authorization (for the duration of the turn).
                return Task.FromResult(app.Authorization.GetTurnToken("mcs"));
            },
            NullLogger.Instance,
            "mcs");
    }

    // Since Auto SignIn is enabled, by the time this is called the sign in has already happened.
    app.OnConversationUpdate(ConversationUpdateEvents.MembersAdded, async (turnContext, turnState, cancellationToken) =>
    {
        var mcsConversationId = turnState.GetValue<string>(MCSConversationPropertyName);
        if (string.IsNullOrEmpty(mcsConversationId))
        {
            var cpsClient = GetClient(app);

            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);

                    // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                    turnState.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                }
            }
        }
    });

    app.OnActivity(ActivityTypes.Message, async (turnContext, turnState, cancellationToken) =>
    {
        var mcsConversationId = turnState.GetValue<string>(MCSConversationPropertyName);
        var cpsClient = GetClient(app);

        if (string.IsNullOrEmpty(mcsConversationId))
        {
            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);

                    // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                    turnState.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                }
            }
        }
        else
        {
            await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                }
            }
        }
    });

    app.Authorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"Auto SignIn: Failed with '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
    });

    return app;
});


// Configure the HTTP request pipeline.

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

var route = app.MapPost(
    "/api/messages",
    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
    {
        await adapter.ProcessAsync(request, response, agent, cancellationToken);
    })
    .RequireAuthorization(new AuthorizeAttribute("AllowedCallers"));


// Setup port and listening address.

if (app.Environment.IsDevelopment())
{
    route.AllowAnonymous();
    var port = args.Length > 0 ? args[0] : "3978";
    app.Urls.Add($"http://localhost:{port}");
}

// Start listening. 
await app.RunAsync();
