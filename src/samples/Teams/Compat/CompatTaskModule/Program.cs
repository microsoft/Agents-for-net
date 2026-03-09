// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CompatTaskModule.Bots;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddMvc();
builder.Services.AddRazorPages();

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

// Add basic bot functionality
builder.AddAgent<TeamsTaskModuleBot>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");

    app.MapGet("/", () => "Microsoft Agents SDK Sample");
    app.UseDeveloperExceptionPage();
    app.MapControllers().AllowAnonymous();
}
else
{
    app.MapControllers();
}

app.MapRazorPages();
app.UseStaticFiles();
app.Run();

