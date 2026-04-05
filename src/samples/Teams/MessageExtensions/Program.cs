// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using MessageExtensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add AgentApplicationOptions from appsettings section "AgentApplication".
builder.AddAgentApplicationOptions();

// Add the AgentApplication, which contains the logic for responding to
// user messages.
builder.AddAgent<MessageExtensionsAgent>();

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

// Enable AspNet authentication and authorization
app.UseAuthentication();
app.UseAuthorization();

// Map GET "/"
app.MapAgentRootEndpoint();

// Map the endpoints for all agents using the [AgentInterface] attribute.
// If there is a single IAgent/AgentApplication, the endpoints will be mapped to (e.g. "/api/message").
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());

// Map GET "/settings" to return the HTML for the settings page, which is defined in MessageExtensionsAgent.GetSettingsHtml().
app.UseStaticFiles();

app.MapGet("/settings", async context =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "settings.html");

    if (!File.Exists(filePath))
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Page not found.");
        return;
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(filePath);
});

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();
