// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SlackAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Register the Slack adapter, which receives Slack traffic directly (Events API + Interactivity)
// and sends replies directly to Slack, bypassing Azure Bot Service.  Configuration (bot token,
// signing secret, bot user id) is bound from the "Slack" section of appsettings.json.
builder.Services.AddSlack(builder.Configuration);

// Register the agent and its options.
builder.Services.AddAgentApplicationOptions();
builder.Services.AddTransient<IAgent, MyAgent>();

WebApplication app = builder.Build();

// Simple landing page.
app.MapGet("/", () => "Microsoft Agents SDK - SlackAgent");

// Map the Slack ingestion endpoints declared via [AgentInterface(SlackAdapterExtensions.SlackProtocol, ...)]
// on the agent (defaults to POST /api/slack).  Configure each URL as the Request URL for both Event
// Subscriptions and Interactivity in your Slack app.
app.MapSlackEndpoints();

app.Run();
