// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Agents.Storage;
using MultiAgentHost;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Logging.AddConsole();
builder.Logging.AddDebug();


// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// This adds the "core" Agent services: ICollections, CloudAdapter, and IChannelServiceClientFactory
builder.AddAgentCore();

// For this sample, both Agents will use the same config.
builder.AddAgentApplicationOptions();

// Add the first Agent
builder.Services.AddTransient<Echo1>();

// Add the second Agent
builder.Services.AddTransient<Echo2>();

// Configure the HTTP request pipeline.

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Route to first agent
var route1 = app.MapPost(
    "/agent1/api/messages",
    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, Echo1 agent, CancellationToken cancellationToken) =>
    {
        await adapter.ProcessAsync(request, response, agent, cancellationToken);
    })
    .RequireAuthorization(new AuthorizeAttribute("AllowedCallers"));

// Route to second agent
var route2 = app.MapPost(
    "/agent2/api/messages",
    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, Echo2 agent, CancellationToken cancellationToken) =>
    {
        await adapter.ProcessAsync(request, response, agent, cancellationToken);
    })
    .RequireAuthorization(new AuthorizeAttribute("AllowedCallers"));

// Setup port and listening address.

if (app.Environment.IsDevelopment())
{
    route1.AllowAnonymous();
    route2.AllowAnonymous();
    var port = args.Length > 0 ? args[0] : "3978";
    app.Urls.Add($"http://localhost:{port}");
}

// Start listening. 
await app.RunAsync();
