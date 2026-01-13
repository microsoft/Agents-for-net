// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add File downloaders
builder.Services.AddSingleton<IList<IInputFileDownloader>>(sp =>
{
    return 
    [
        new Microsoft.Agents.Builder.App.M365AttachmentDownloader(
            sp.GetService<IConnections>()!,
            sp.GetService<IHttpClientFactory>()!),
        // new AttachmentDownloader(sp.GetService<IHttpClientFactory>()!)
    ];
});

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<TeamsAgent.TeamsAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operates correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Configure the HTTP request pipeline.

// Add AspNet token validation for Azure Bot Service and Entra.  Authentication is
// configured in the appsettings.json "TokenValidation" section.
builder.Services.AddControllers();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

WebApplication app = builder.Build();

// Map Agent endpoints.  By default the agent will respond on '/api/messages'.
app.MapAgentEndpoints(requireAuth: !app.Environment.IsDevelopment());

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
