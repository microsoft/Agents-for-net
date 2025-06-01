// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading;
using ModelContextProtocol.Protocol;
using A2AAgent;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Logging.AddConsole();

// Add AgentApplicationOptions from config.
builder.AddAgentApplicationOptions();

// Add the Agent
builder.AddAgent<MyAgent>();

// Register IStorage.  For development, MemoryStorage is suitable.
// For production Agents, persisted storage should be used so
// that state survives Agent restarts, and operate correctly
// in a cluster of Agent instances.
builder.Services.AddSingleton<IStorage, MemoryStorage>();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRouting();

app.MapGet("/", () => "Microsoft Agents SDK Sample");

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();


app.MapPost("/a2a", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    var jsonRpcRequest = await A2AProtocolConverter.ReadRequestAsync<JsonRpcRequest>(request);

    if (jsonRpcRequest.Method.Equals("message/stream"))
    {
        var (activity, contextId, taskId) = A2AProtocolConverter.CreateActivityFromRequest(jsonRpcRequest);
        await adapter.ProcessAsync(activity, HttpHelper.GetIdentity(request), response, agent, new A2AStreamedResponseWriter(jsonRpcRequest.Id.ToString(), contextId, taskId), cancellationToken);
    }
})
    .AllowAnonymous();

app.MapGet("/.well-known/agent.json", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    System.Diagnostics.Trace.WriteLine("/.well-known/agent.json");
    var agentCard = "{\"name\":\"EmptyAgent\",\"description\":\"Simple Echo Agent\",\"version\":\"0.1.0\", \"url\":\"http://localhost:3978/a2a\", \"defaultInputModes\": [], \"defaultOutputModes\": [], \"skills\": [], \"capabilities\":{\"streaming\":true,\"pushNotifications\":false,\"stateTransitionHistory\":false}}";
    response.ContentType = "application/json";
    await response.Body.WriteAsync(Encoding.UTF8.GetBytes(agentCard), cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
})
    .AllowAnonymous();


// Hardcoded for brevity and ease of testing. 
// In production, this should be set in configuration.
app.Urls.Add($"http://localhost:3978");

app.Run();
