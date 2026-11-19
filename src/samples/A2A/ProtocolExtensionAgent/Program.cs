// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Extensions.A2A;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;
using ProtocolExtensionAgent;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults().AddAgent<DebateAgent>();

WebApplication app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Map the A2At endpoints only for this sample
app.MapA2AApplicationEndpoints(requireAuth: false);

app.Run();
