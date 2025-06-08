// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using EmptyAgent;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Agents.Hosting.A2A;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using ModelContextProtocol.Protocol;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Schema.Generation;
using A2AAgent;
using Microsoft.Extensions.AI;
using System;
using System.Linq;
using System.Threading.Tasks;

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

// Add the A2A adapter to handles A2A requests
builder.Services.AddA2AAdapter();

WebApplication app = builder.Build();


// Configure the HTTP request pipeline.

app.MapPost("/api/messages", async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
{
    await adapter.ProcessAsync(request, response, agent, cancellationToken);
})
    .AllowAnonymous();

// Map A2A endpoints.  By default A2A will respond on '/a2a'.
app.MapA2A(requireAuth: false);

// Map MCP endpoints.  By default MCP will respond on '/mcp'.
app.MapPost(
    "/mcp",
    async (HttpRequest request, HttpResponse response, IAgentHttpAdapter adapter, IAgent agent, CancellationToken cancellationToken) =>
    {
        var jsonRpcRequest = await MCPProtocolConverter.ReadRequestAsync<JsonRpcRequest>(request);

        if (jsonRpcRequest.Method.Equals("initialize"))
        {
            System.Diagnostics.Trace.WriteLine("MCP: initialize");
            await WriteInitializeResponse(jsonRpcRequest, request, response, cancellationToken);
        }
        else if (jsonRpcRequest.Method.Equals("notifications/initialized"))
        {
            System.Diagnostics.Trace.WriteLine("MCP: notifications/initialized");
        }
        else if (jsonRpcRequest.Method.Equals("tools/list"))
        {
            System.Diagnostics.Trace.WriteLine("MCP: tools/list");

            JSchemaGenerator generator = new();
            var inputSchema = JsonSerializer.SerializeToElement(JsonSerializer.Deserialize<object>(generator.Generate(typeof(ChatMessage)).ToString()));

            var tools = new ListToolsResult()
            {
                Tools = [
                    new Tool()
                    {
                        Name = "message",
                        InputSchema = inputSchema,
                    }
                ]
            };

            var rpcResponse = new JsonRpcResponse()
            {
                Id = jsonRpcRequest.Id,
                Result = JsonSerializer.SerializeToNode(tools)
            };

            response.ContentType = "application/json";
            var json = MCPProtocolConverter.ToJson(rpcResponse);
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
        else if (jsonRpcRequest.Method.Equals("tools/call"))
        {
            System.Diagnostics.Trace.WriteLine("MCP: tools/call");

            //TODO: verify request.Id

            if( !request.Headers.TryGetValue("mcp-session-id", out var sessionId))
            {
                sessionId = Guid.NewGuid().ToString("N");
            }

            var activity = MCPProtocolConverter.CreateActivityFromRequest(jsonRpcRequest, sessionId);

            await adapter.ProcessAsync(activity, HttpHelper.GetIdentity(request), response, agent, new MCPStreamedResponseWriter(), cancellationToken);
        }
        else
        {
            System.Diagnostics.Trace.WriteLine($"MCP: {jsonRpcRequest.Method}");
        }
    })
        .WithMetadata(new AcceptsMetadata(["application/json"]))
        .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, contentTypes: ["text/event-stream"]))
        .WithMetadata(new ProducesResponseTypeMetadata(StatusCodes.Status202Accepted));

if (app.Environment.IsDevelopment())
{
    // Hardcoded for brevity and ease of testing. 
    // In production, this should be set in configuration.
    app.Urls.Add($"http://localhost:3978");
}

app.Run();


static async Task WriteInitializeResponse(JsonRpcRequest rpcRequest, HttpRequest httpRequest, HttpResponse httpResponse, CancellationToken cancellationToken = default)
{
    var result = new InitializeResult()
    {
        ProtocolVersion = "2024-11-05",
        Capabilities = new ServerCapabilities()
        {

        },
        ServerInfo = new Implementation()
        {
            Name = "EmptyAgent",
            Version = "1.0.0",
        }
    };

    var rpcResponse = new JsonRpcResponse()
    {
        Id = rpcRequest.Id,
        Result = JsonSerializer.SerializeToNode(result)
    };

    var json = MCPProtocolConverter.ToJson(rpcResponse);

    if (httpRequest.Headers.Accept.Contains("text/event-stream"))
    {
        httpResponse.ContentType = "text/event-stream";
        json = string.Format(MCPStreamedResponseWriter.MessageTemplate, json);
    }
    else
    {
        httpResponse.ContentType = "application/json";
    }

    await httpResponse.Body.WriteAsync(Encoding.UTF8.GetBytes(json), cancellationToken);
    await httpResponse.Body.FlushAsync(cancellationToken);
}