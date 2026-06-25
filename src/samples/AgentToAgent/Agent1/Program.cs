// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


using Agent1;
using Microsoft.Agents.Client;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<HostAgent>()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

// Add the Agent-to-Agent handling. This manages communication with other agents
// and is configured in the appsettings.json "Agent" section.
builder.AddAgentHost();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

app.MapControllers();
app.Run();
