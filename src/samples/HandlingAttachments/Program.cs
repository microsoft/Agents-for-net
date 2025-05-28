// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using HandlingAttachments;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Register FileDownloaders
builder.Services.AddSingleton<IList<IInputFileDownloader>>(sp => [
    new AttachmentDownloader(sp.GetService<IHttpClientFactory>()),
    new TeamsAttachmentDownloader(new TeamsAttachmentDownloaderOptions() { TokenProviderName = "ServiceConnection" }, sp.GetService<IConnections>(), sp.GetService<IHttpClientFactory>())
]);

// Add ApplicationOptions
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<AttachmentsAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.MapGet("/", () => "Microsoft Agents SDK Sample");
app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");

app.Run();
