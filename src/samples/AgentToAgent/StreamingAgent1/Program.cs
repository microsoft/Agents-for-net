// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Client;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StreamingAgent1;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<StreamingHostAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Add the Agent-to-Agent handling
builder.AddAgentHost();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

app.UseAgents()
    .MapDefaultAgentEndpoints();

app.Run();
