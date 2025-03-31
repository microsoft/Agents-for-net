// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HandlingCards;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Samples;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

// Add the Agent
builder.AddAgent<CardsAgent>();

// Configure the HTTP request pipeline.

// Add AspNet token validation
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

var route = app.MapPost(
    "/api/messages",
    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
    {
        await adapter.ProcessAsync(request, response, agent, cancellationToken);
    })
    .RequireAuthorization(new AuthorizeAttribute("AllowedCallers"));


// Setup port and listening address.

if (app.Environment.IsDevelopment())
{
    route.AllowAnonymous();
    var port = args.Length > 0 ? args[0] : "3978";
    app.Urls.Add($"http://localhost:{port}");
}

// Start listening. 
await app.RunAsync();
