// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ConversationAgent;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<TeamsConversationAgent>()
    .AddAgentM365AttachmentDownloader()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

// Add the authentication and authorization middleware to the request pipeline.
app.UseAgents();

// Use Microsoft.Agents.Core.HeaderPropagation
app.UseHeaderPropagation();

// Map the default agent endpoints: GET "/" and the agent message endpoints.
app.MapDefaultAgentEndpoints();

app.Run();
