// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using MessageExtensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<MessageExtensionsAgent>()
    .AddAgentM365AttachmentDownloader()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

app.UseAgents();

// Use Microsoft.Agents.Core.HeaderPropagation
app.UseHeaderPropagation();

// Map agent endpoints for "/" and "/api/messages".
app.MapDefaultAgentEndpoints();

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

app.Run();
