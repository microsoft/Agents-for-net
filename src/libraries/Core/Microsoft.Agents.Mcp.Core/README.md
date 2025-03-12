# Microsoft.Agents.Mcp.Core

## Overview

The Microsoft Agents MCP (Model Context Protocol) Core Module provides a robust framework for handling JSON-RPC based communication, payload processing, and method invocation in a structured and type-safe manner.

## Installation

Install the MCP Server SDK via NuGet:

```bash
dotnet add package Microsoft.Agents.Mcp.Core
```


## Key Components

### Payload Handlers

The module defines several abstract base classes for different types of payload handling:

1. **McpPayloadHandlerBase**
   - Base interface for payload handlers
   - Defines method for creating and executing payloads

2. **McpMethodWithoutInputsPayloadHandlerBase**
   - Handles methods that don't require input parameters
   - Provides a generic way to execute methods and post results

3. **McpMethodPayloadHandlerBase**
   - Supports methods with specific request and (optionally) response types
   - Handles JSON deserialization of method parameters
   - Provides type-safe method execution

4. **McpNotificationHandlerBase**
   - Handles notification-type payloads
   - Supports deserialization of notification parameters

### Payload Types

- **McpRequest**: Represents a method call with an ID and parameters
- **McpNotification**: Represents a notification without an ID
- **McpResult**: Represents the result of a method call

## Serialization

The module uses `System.Text.Json` for payload serialization and deserialization, with custom serialization options defined in the `Serialization` utility class.

## Example Usage

### Creating a Method Handler

```csharp
public class MyMethodHandler : McpMethodPayloadHandlerBase<MyRequestType, MyResponseType>
{
    public override string Method => "my/method";

    protected override async Task<MyResponseType> ExecuteMethodAsync(
        IMcpContext context, 
        McpRequest<MyRequestType> payload, 
        CancellationToken ct)
    {
        // Implement method logic
        var result = ProcessRequest(payload.Parameters);
        return result;
    }
}
```

### Notification Handling

```csharp
public class MyNotificationHandler : McpNotificationHandlerBase<MyNotificationType>
{
    protected override async Task ExecuteAsync(
        IMcpContext context, 
        McpNotification<MyNotificationType> payload, 
        CancellationToken ct)
    {
        // Handle notification
        await ProcessNotification(payload.Parameters);
    }
}
```

## Key Features

- Type-safe payload handling
- JSON-RPC method and notification support
- Flexible serialization
- Cancellation token support
- Context-based execution

## Dependencies

- Microsoft.Extensions.DependencyInjection

## Logging and Transport

The module provides basic infrastructure for:
- Log level configuration
- Method and notification dispatching
- Serialization and deserialization of payloads

## Best Practices

1. Always use the appropriate base handler for your method type
2. Implement proper error handling in your method implementations
3. Utilize cancellation tokens for long-running operations
4. Follow the existing serialization patterns

## License

This project is licensed under the MIT License—see the [LICENSE](LICENSE) file for details.

## Support

For issues and questions, please open a GitHub issue in the project repository.

