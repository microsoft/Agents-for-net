# Microsoft.Agents.Mcp.Server

## Table of Contents
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Creating a Custom Server](#creating-a-custom-server)
  - [Step 1: Create a New ASP.NET Core Project](#step-1-create-a-new-aspnet-core-project)
  - [Step 2: Install Required NuGet Packages](#step-2-install-required-nuget-packages)
  - [Step 3: Create the Operations Directory](#step-3-create-the-operations-directory)
  - [Step 4: Implement the MathAdd Operation](#step-4-implement-the-mathadd-operation)
  - [Step 5: Configure the MCP Server](#step-5-configure-the-mcp-server)
  - [Step 6: Create Controller](#step-6-create-controller)
  - [Step 7: Build and Run](#step-7-build-and-run)
- [Testing Your Custom Server](#testing-your-custom-server)
  - [Getting a Session ID](#getting-a-session-id)
  - [Testing the Math Adder](#testing-the-math-adder)
  - [Listing Available Operations](#listing-available-operations)
- [Notifications](#notifications)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [Documentation](#documentation)
- [License](#license)
- [Support](#support)

## Overview

The MCP (Model Context Protocol) Server SDK provides a robust framework for building extensible, event-driven server applications with strong typing, tool execution, and notification capabilities.

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or Visual Studio Code
- Basic knowledge of C# and ASP.NET Core

## Creating a Custom Server

This section provides step-by-step instructions for creating a custom server implementation with Microsoft Agents MCP framework.

### Step 1: Create a New ASP.NET Core Project

1. Open Visual Studio and select "Create a new project"
2. Choose "ASP.NET Core Web API" and click Next
3. Name your project (e.g., `Microsoft.Agents.Mcp.CustomServer`)
4. Select .NET 8.0 or later as the target framework
5. Click Create

### Step 2: Install Required NuGet Packages

Add the required NuGet packages to your project:

```bash
dotnet add package Microsoft.Agents.Mcp.Core
dotnet add package Microsoft.Agents.Mcp.Server
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
```

### Step 3: Create the Operations Directory

1. Create a new directory called `Operations` in your project root
2. This directory will contain your custom tool executors

### Step 4: Implement the MathAdd Operation

Create a new file `MathAddOperationExecutor.cs` in the Operations directory with the following code:

```csharp
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Handlers.Contracts.ClientMethods.Logging;
using Microsoft.Agents.Mcp.Core.Payloads;
using Microsoft.Agents.Mcp.Server.Methods.Tools.ToolsCall.Handlers;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Mcp.CustomServer.Operations;

public struct MathAddInput
{
    [Description("the first referenced number")]
    public required string Number1 { get; init; }
    [Description("the second referenced number")]
    public required string Number2 { get; init; }
}

public struct MathAddOutput
{
    public required int Total { get; init; }
}

public class MathAddOperationExecutor : McpToolExecutorBase<MathAddInput, MathAddOutput>
{
    public override string Id => "Math_Adder";
    public override string Description => "Adds two numbers";
    public override async Task<MathAddOutput> ExecuteAsync(McpRequest<MathAddInput> payload, IMcpContext context, CancellationToken ct)
    {
        var n1 = int.Parse(payload.Parameters.Number1);
        var n2 = int.Parse(payload.Parameters.Number2);
        var result = new MathAddOutput() { Total = n1 + n2 };
        await context.PostNotificationAsync(new McpLogNotification<string>(
             new NotificationParameters<string>()
             {
                 Level = "notice",
                 Logger = "echo",
                 Data = $"Adding {n1} and {n2}"
             }), ct);
        return result;
    }
}
```

### Step 5: Configure the MCP Server

Update `Program.cs` to configure the MCP server:

```csharp
using Microsoft.Agents.Mcp.CustomServer.Operations;
using Microsoft.Agents.Mcp.Core.DependencyInjection;
using Microsoft.Agents.Mcp.Server.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHttpClient();

builder.Services.AddModelContextProtocolHandlers();
builder.Services.AddDefaultOperationFactory();
builder.Services.AddDefaultPayloadExecutionFactory();
builder.Services.AddDefaultPayloadResolver();
builder.Services.AddDefaultServerExecutors();
builder.Services.AddMemorySessionManager();
builder.Services.AddTransportManager();

builder.Services.AddToolExecutor<MathAddOperationExecutor>();

builder.Services.AddLogging();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapControllers();
app.Run();
```

### Step 6: Create Controller

Create a new file `ToolsController.cs` in the Controllers directory with the following code:

```csharp   
using Microsoft.AspNetCore.Mvc;
using Microsoft.Agents.Mcp.Core.JsonRpc;
using System.Text.Json;
using Microsoft.Agents.Mcp.Core.Abstractions;
using Microsoft.Agents.Mcp.Core.Transport;
using Microsoft.Agents.Mcp.Server.AspNet;

namespace Microsoft.Agents.Mcp.CustomServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ToolsController : ControllerBase
    {
        private readonly IHttpClientFactory httpClientFactory;
        private readonly IMcpProcessor mcpProcessor;
        private readonly ITransportManager transportManager;
        private readonly ILogger<ToolsController> logger;

        public ToolsController(
            IHttpClientFactory httpClientFactory,
            IMcpProcessor mcpProcessor,
            ITransportManager transportManager,
            ILogger<ToolsController> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.mcpProcessor = mcpProcessor;
            this.transportManager = transportManager;
            this.logger = logger;
        }

        [HttpGet("/Mcp/sse")]
        public async Task SseGet(CancellationToken ct)
        {
            logger.LogInformation("Starting SSE connection.");
            var transport = new HttpSseServerTransport(transportManager, (string session) => $"/Mcp/sse/message?sessionId={session}", Response, ct, logger);
            var session = await mcpProcessor.CreateSessionAsync(transport, ct);
            await transport.WaitTillCloseAsync(ct);
            logger.LogInformation("SSE connection closed.");
        }

        [HttpPost("/Mcp/sse/message")]
        public Task<IActionResult> SsePost(JsonRpcPayload request, [FromQuery] string sessionId, CancellationToken ct)
        {
            logger.LogInformation($"Received SSE POST request for session {sessionId}.");
            return DispatchRequest(request, sessionId, ct);
        }
    
        private async Task<IActionResult> DispatchRequest(JsonRpcPayload request, string sessionId, CancellationToken ct)
        {
            if (transportManager.TryGetTransport(sessionId, out var transport))
            {
                await transport.ProcessPayloadAsync(request, ct);
                return Ok();
            }

            logger.LogWarning($"Transport not found for session {sessionId}.");
            return NotFound();
        }
    }
}
```

### Step 7: Build and Run

1. Build your project:
```bash
dotnet build
```

2. Run your project:
```bash
dotnet run
```
3. The MCP server will start at http://localhost:5000 (or the endpoint specified in your configuration)

## Testing Your Custom Server

### Getting a Session ID

First, establish an SSE connection to get a session ID:

```http
GET https://localhost:5000/Mcp/sse
```

This endpoint will establish a Server-Sent Events connection and return a *session ID* which you'll need for subsequent API calls.

### Listing Available Tools

Clients can retrieve available tools using the `tools/list` method:

```http
POST https://localhost:5000/Mcp/sse/message?sessionId=[session_id]
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": "123",
  "method": "tools/list",
  "params": {
    "cursor": ""
  }
}
```

### Testing the Math Adder

Send a POST request to invoke the Math Adder tool (using the session ID obtained from the previous step):

```http
POST https://localhost:5000/Mcp/sse/message?sessionId=[session_id]
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "id": "2",
  "method": "tools/call",
  "params": {
    "name": "Math_Adder",
    "arguments": {
      "number1": "123",
      "number2": "124"
    }
  }
}
```

## Notifications

The MCP framework supports sending notifications from tools to clients:

```csharp
await context.PostNotificationAsync(new McpLogNotification<string>(
    new NotificationParameters<string>()
    {
        Level = "notice",
        Logger = "echo",
        Data = "Notification message"
    }), ct);
```

## Best Practices

1. Keep tools as singletons
2. Honor cancellation tokens for long-running operations
3. Use dependency injection for service references
4. Implement proper error handling
5. Use the notification system for progress updates

## Troubleshooting

- Ensure all required services are registered
- Check serialization compatibility
- Verify tool input and output schemas
- Enable detailed logging for diagnostics
- Verify network connectivity for server endpoints

## Documentation

- [Model Context Protocol documentation](https://modelcontextprotocol.io)
- [MCP Specification](https://spec.modelcontextprotocol.io)

## License

This project is licensed under the MIT License—see the [LICENSE](LICENSE) file for details.

## Support

For issues and questions, please open a GitHub issue in the project repository.
