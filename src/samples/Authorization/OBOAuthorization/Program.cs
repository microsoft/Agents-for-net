// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
    const string MCSConversationPropertyName = "MCSConversationId";

    var app = new AgentApplication(sp.GetRequiredService<AgentApplicationOptions>());

    CopilotClient GetClient(AgentApplication app, ITurnContext turnContext)
    {
        var settings = new ConnectionSettings(builder.Configuration.GetSection("CopilotStudioAgent"));
        string[] scopes = [CopilotClient.ScopeFromSettings(settings)];

        return new CopilotClient(
            settings,
            sp.GetService<IHttpClientFactory>(),
            tokenProviderFunction: async (s) =>
            {
                // In this sample, the Azure Bot OAuth Connection is configured to return an 
                // exchangeable token, that can be exchange for different scopes.  This can be
                // done multiple times using different scopes.
                return await app.UserAuthorization.ExchangeTurnTokenAsync(turnContext, "mcs", exchangeScopes: scopes);
            },
            NullLogger.Instance,
            "mcs");
    }

    // Since Auto SignIn is enabled, by the time this is called the token is already available via UserAuthorization.GetTurnTokenAsync or
    // UserAuthorization.ExchangeTurnTokenAsync.
    // NOTE:  This is a slightly unusual way to handle incoming Activities (but perfectly) valid.  For this sample,
    // we just want to proxy messages to/from a Copilot Studio Agent.
    app.OnActivity((turnContext, cancellationToken) => Task.FromResult(true), async (turnContext, turnState, cancellationToken) =>
    {
        var mcsConversationId = turnState.Conversation.GetValue<string>(MCSConversationPropertyName);
        var cpsClient = GetClient(app, turnContext);

        if (string.IsNullOrEmpty(mcsConversationId))
        {
            // Regardless of the Activity  Type, start the conversation.
            await foreach (IActivity activity in cpsClient.StartConversationAsync(emitStartConversationEvent: true, cancellationToken: cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);

                    // Record the conversationId MCS is sending. It will be used this for subsequent messages.
                    turnState.Conversation.SetValue(MCSConversationPropertyName, activity.Conversation.Id);
                }
            }
        }
        else if (turnContext.Activity.IsType(ActivityTypes.Message))
        {
            // Send the Copilot Studio Agent whatever the sent and send the responses back.
            await foreach (IActivity activity in cpsClient.AskQuestionAsync(turnContext.Activity.Text, mcsConversationId, cancellationToken))
            {
                if (activity.IsType(ActivityTypes.Message))
                {
                    await turnContext.SendActivityAsync(activity.Text, cancellationToken: cancellationToken);
                }
            }
        }
    });

    app.UserAuthorization.OnUserSignInFailure(async (turnContext, turnState, handlerName, response, initiatingActivity, cancellationToken) =>
    {
        await turnContext.SendActivityAsync($"SignIn failed with '{handlerName}': {response.Cause}/{response.Error.Message}", cancellationToken: cancellationToken);
    });

    return app;
});


// Configure the HTTP request pipeline.

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");
app.MapGet("/", () => "Microsoft Agents SDK Sample");

app.Run();
