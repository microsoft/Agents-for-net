// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Agents.Hosting.A2A;
using Microsoft.Extensions.Hosting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<MyAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/", () => "Microsoft Agents SDK Sample");

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Map A2A endpoints.  By default A2A will respond on '/a2a'.
app.MapA2A();

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
if (app.Environment.IsDevelopment())
{
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
