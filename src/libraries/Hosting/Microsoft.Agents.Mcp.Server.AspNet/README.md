# Microsoft.Agents.Mcp.Server.AspNet

## Overview

The Microsoft Agents MCP (Model Context Protocol) Transport Module provides robust communication transport mechanisms for distributed systems, supporting different communication paradigms and protocols.

## Installation

Install the MCP Server SDK via NuGet:

```bash
dotnet add package Microsoft.Agents.Mcp.Server.AspNet
```

## Key Features

- Multiple transport implementations
- Flexible message handling
- Support for various communication patterns
- Logging and error handling
- Cancellation and resource management

## Transport Implementations

### 1. StdioServerTransport

#### Description
A transport mechanism using standard input/output streams for communication.

#### Key Characteristics
- Reads and writes messages using UTF-8 encoded streams
- Supports JSON-RPC payload processing
- Provides thread-safe message handling
- Implements `IDisposable` for proper resource management

#### Usage Example
```csharp
var transport = new StdioServerTransport(inputStream, outputStream);
await transport.Connect(sessionId, 
    ingestMessage: ProcessIncomingMessage, 
    close: HandleTransportClosure);
```

### 2. HttpSseServerTransport

#### Description
A Server-Sent Events (SSE) based HTTP transport for real-time, server-to-client communication.

#### Key Characteristics
- Implements Server-Sent Events (SSE)
- Supports session-based communication
- Provides endpoint discovery
- Integrated with ASP.NET Core
- Comprehensive logging
- Supports graceful connection closure

#### Usage Example
```csharp
var transport = new HttpSseServerTransport(
    transportManager, 
    getMessageEndpoint, 
    httpResponse, 
    cancellationToken, 
    logger);

await transport.Connect(sessionId, 
    ingestMessage: ProcessIncomingMessage, 
    close: HandleTransportClosure);
```

### 3. HttpCallbackServerTransport

#### Description
A Bi-directional HTTP based transport for long-running, server-to-client communication, focussed on Server to Server application patterns

#### Key Characteristics
- Implements a Bi-directional HTTP (webhook) based protocol
- Supports long-running sessions
- Integrated with ASP.NET Core
- Comprehensive logging
- Supports graceful connection closure

#### Usage Example
```csharp
var transport = new HttpSseServerTransport(
    transportManager, 
    httpClientFactory, 
    callbackUri);

await transport.Connect(sessionId, 
    ingestMessage: ProcessIncomingMessage, 
    close: HandleTransportClosure);
```

## Core Interfaces

### IMcpTransport

The primary interface for transport implementations, defining core methods:

- `Connect`: Establish a transport connection
- `SendOutgoingAsync`: Send messages to the client
- `ProcessPayloadAsync`: Process incoming messages
- `CloseAsync`: Close the transport connection

## Serialization

Uses `System.Text.Json` with custom serialization options for consistent JSON handling across transports.

## Error Handling and Logging

- Comprehensive logging using `Microsoft.Extensions.Logging`
- Detailed error tracking
- Graceful error and connection management

## Configuration

### Serialization Options

Custom serialization is managed through `Serialization.GetDefaultMcpSerializationOptions()`, which provides consistent JSON serialization across the module.

## Best Practices

1. Always use cancellation tokens
2. Handle potential exceptions
3. Properly dispose and close transport resources


## Extensibility

The modular design allows for easy addition of new transport mechanisms by implementing the `IMcpTransport` interface.

## License

This project is licensed under the MIT License—see the [LICENSE](LICENSE) file for details.


## Sample Configuration

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
