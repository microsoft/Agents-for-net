# Microsoft.Agents.Mcp.Server

## Table of Contents
- [Overview](#overview)
- [Installation](#installation)
- [Initialization](#initialization)
- [Tools System](#tools-system)
  - [Creating a Tool](#creating-a-tool)
  - [Registering Tools](#registering-tools)
  - [Invoking Tools](#invoking-tools)
- [Notifications](#notifications)

## Overview

The MCP (Message Communication Protocol) Server SDK provides a robust framework for building extensible, event-driven server applications with strong typing, tool execution, and notification capabilities.

## Installation

Install the MCP Server SDK via NuGet:

```bash
dotnet add package Microsoft.Agents.Mcp.Server
```

## Initialization

### Startup Configuration

In your `Program.cs` or `Startup.cs`:

```csharp
// Example configuration in ASP.NET Core startup
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
builder.Services.AddToolExecutor<WeatherOperationExecutor>();
builder.Services.AddLogging();
builder.Logging.AddConsole();
```

## Tools System

### Creating a Tool

Implement a custom tool by inheriting from `McpToolExecutorBase<InputSchema, OutputSchema>`:

```csharp
public class CalculatorTool : McpToolExecutorBase<CalculatorInput, CalculatorOutput>
{
    public override string Id => "calculator";
    public override string Description => "Performs basic arithmetic operations";

    public override async Task<CalculatorOutput> ExecuteAsync(
        McpRequest<CalculatorInput> payload, 
        IMcpContext context, 
        CancellationToken ct)
    {
        var input = payload.Parameters;
        return input.Operation switch 
        {
            "add" => new CalculatorOutput { Result = input.A + input.B },
            "subtract" => new CalculatorOutput { Result = input.A - input.B },
            _ => throw new NotSupportedException("Operation not supported")
        };
    }
}

// Input and output schema definitions
public class CalculatorInput
{
    [Description("the first referenced number")]
    public double A { get; set; }
    [Description("the second referenced number")]
    public double B { get; set; }

    [Description("the operation to execute. 'add' or 'substract'")]
    public string Operation { get; set; }
}

public class CalculatorOutput
{
    public double Result { get; set; }
}
```

### Registering Tools

```csharp
builder.Services.AddToolExecutor<CalculatorTool>();
```

### Invoking Tools

Clients can invoke tools using the `tools/call` method:

```json
{
    "method": "tools/call",
    "params": {
        "name": "calculator",
        "arguments": {
            "a": 10,
            "b": 5,
            "operation": "add"
        }
    }
}
```

### Listing Available Tools

Clients can retrieve available tools using the `tools/list` method.

## Notifications

### Creating a Notification Handler

```csharp
public class UserNotificationHandler : IMcpNotificationHandler
{
    public string EventType => "notifications/initialized";

    public Task HandleAsync(McpNotification notification, IMcpContext context, CancellationToken ct)
    {
        var userData = notification.Payload.ToObject<UserData>();
        // Process user creation notification
        return Task.CompletedTask;
    }
}
```

### Registering Notification Handlers

```csharp
builder.Services.AddPayloadExecutor<UserNotificationHandler>();
```


## Best Practices

1. Keep tools singletons
2. Honor cancellation tokens for long-running operations


## Troubleshooting

- Ensure all required services are registered
- Check serialization compatibility
- Verify tool input and output schemas
- Enable detailed logging for diagnostics


## Documentation

- [Model Context Protocol documentation](https://modelcontextprotocol.io)
- [MCP Specification](https://spec.modelcontextprotocol.io)

## License

This project is licensed under the MIT License—see the [LICENSE](LICENSE) file for details.

## Support

For issues and questions, please open a GitHub issue in the project repository.
