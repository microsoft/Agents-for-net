// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading;
using TeamsConversationSsoQuickstart;
using TeamsConversationSsoQuickstart.Bots;
using TeamsConversationSsoQuickstart.Dialogs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add the AgentApplication, which contains the logic for responding to
// user messages. In this sample, the Agent is a TeamsActivityHandler based
// agent using Dialogs.
builder.AddAgent<TeamsBot<MainDialog>, AdapterWithErrorHandler>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add Conversation and User state.
builder.Services.AddSingleton<ConversationState>();
builder.Services.AddSingleton<UserState>();

// The Dialog that will be run by the bot.
builder.Services.AddSingleton<MainDialog>();


// Configure the HTTP request pipeline.

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();

