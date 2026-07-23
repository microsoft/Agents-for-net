// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using TaskModules;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddAgentDefaults()
    .AddAgent<TaskModulesAgent>()
    .AddAgentM365AttachmentDownloader()
    .AddAgentAuthorization(b => b.AddAgentAspNetAuthentication());

WebApplication app = builder.Build();

app.UseAgents();

// Map agent endpoints for "/" and "/api/messages".
app.MapDefaultAgentEndpoints();

app.UseStaticFiles();
app.MapGet("/dialog-form", async context =>
{
    var filePath = Path.Combine(app.Environment.WebRootPath, "dialog-form/index.html");

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
