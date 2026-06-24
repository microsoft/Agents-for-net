// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Hosting.A2A;
using Microsoft.Extensions.Hosting;
using A2AAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

builder.AddAgentDefaults()
    .AddAgent<MyAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the A2A adapter to handle A2A requests
builder.Services.AddA2AAdapter();

WebApplication app = builder.Build();

app.UseAgents()
    .MapDefaultAgentEndpoints();

// Add A2A endpoints.  By default A2A will respond on '/a2a'.
app.MapA2AEndpoints(requireAuth: !app.Environment.IsDevelopment());

app.Run();
