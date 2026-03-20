# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is the Microsoft 365 Agents SDK for .NET - a comprehensive framework for building enterprise-grade conversational agents that work across M365, Teams, Copilot Studio, and other platforms. The SDK provides building blocks for agents that handle user interactions, orchestrate requests, reason responses, and collaborate with other agents.

**Current State:** Generally Available (GA)

**Documentation:** https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/

## Build and Test Commands

### Build
```bash
# Build the entire solution
dotnet build src/Microsoft.Agents.SDK.sln

# Build in Debug configuration (default)
dotnet build -c Debug AgentSdk.proj

# Build in Release configuration
dotnet build -c Release AgentSdk.proj

# Restore dependencies
dotnet restore AgentSdk.proj
```

### Testing
```bash
# Run all tests
dotnet test --no-build -c Debug ./src/

# Run tests for a specific project
dotnet test src/tests/Microsoft.Agents.Core.Tests/

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"

# Run tests with specific category/trait
dotnet test --filter "Priority=1"
```

### Package
```bash
# Create NuGet packages
dotnet pack --no-build -c Debug src/Microsoft.Agents.SDK.sln
```

### Development Workflow
1. Open `src/Microsoft.Agents.SDK.sln` in Visual Studio 2022
2. Build the solution (Ctrl+Shift+B)
3. Run tests via Test Explorer
4. For local agent testing, use the Agents Playground

## Architecture

### Core Components

**Microsoft.Agents.Core** (`src/libraries/Core/Microsoft.Agents.Core/`)
- Contains Activity Protocol models and core SDK interfaces
- Foundation for all agent communication
- Defines serialization contracts

**Microsoft.Agents.Builder** (`src/libraries/Builder/Microsoft.Agents.Builder/`)
- Main agent construction framework
- Key types: `AgentApplication`, `IAgent`, `ITurnContext`, `ITurnState`
- Route-based message handling with `OnActivity()`, `OnConversationUpdate()`, etc.
- Supports middleware pipeline pattern

**Microsoft.Agents.Hosting.AspNetCore** (`src/libraries/Hosting/AspNetCore/`)
- ASP.NET Core integration for hosting agents
- Extension methods: `AddAgent<T>()`, `AddAgentApplicationOptions()`, `AddAgentAspNetAuthentication()`
- Provides `IAgentHttpAdapter` for processing HTTP requests
- Default endpoint: `/api/messages`

**Microsoft.Agents.Authentication.Msal** (`src/libraries/Authentication/Authentication.Msal/`)
- MSAL-based authentication for Azure Bot Service and Entra ID
- Supports ClientSecret, Federated Credentials, and Managed Identity auth types
- Configuration via `appsettings.json` Connections section

### Client Libraries

**Microsoft.Agents.Client** (`src/libraries/Client/Microsoft.Agents.Client/`)
- Agent-to-Agent (A2A) communication
- `IAgentHost` for managing conversations with other agents
- Extension: `AddAgentHost()`
- Supports both Normal and Streaming delivery modes

**Microsoft.Agents.Connector** (`src/libraries/Client/Microsoft.Agents.Connector/`)
- Azure Bot Service channel connectivity

**Microsoft.Agents.CopilotStudio.Client** (`src/libraries/Client/Microsoft.Agents.CopilotStudio.Client/`)
- Client for interacting with Copilot Studio agents
- Configured via `ConnectionSettings` (EnvironmentId + SchemaName, or DirectConnectUrl)
- Requires `CopilotStudio.Copilots.Invoke` API permission
- Supports user-based auth flows and OBO (On-Behalf-Of) flows

### Extensions

**Microsoft.Agents.Extensions.Teams** (`src/libraries/Extensions/Microsoft.Agents.Extensions.Teams/`)
- Microsoft Teams-specific functionality

**Microsoft.Agents.Extensions.Teams.AI** (`src/libraries/Extensions/Microsoft.Agents.Extensions.Teams.AI/`)
- Teams AI capabilities integration

**Microsoft.Agents.Extensions.SharePoint** (`src/libraries/Extensions/Microsoft.Agents.Extensions.SharePoint/`)
- SharePoint integration

### Storage

**Microsoft.Agents.Storage** (`src/libraries/Storage/Microsoft.Agents.Storage/`)
- Base storage abstractions
- `MemoryStorage` for development (non-persistent)

**Microsoft.Agents.Storage.Blobs** and **Microsoft.Agents.Storage.CosmosDb**
- Production-ready persistent storage implementations
- Required for multi-instance agent deployments

## Agent Patterns

### Basic Agent Structure
```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // Register handlers with optional RouteRank for ordering
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }
}
```

### ASP.NET Core Program Setup
1. Add services: `builder.AddAgentApplicationOptions()`, `builder.AddAgent<TAgent>()`
2. Register storage: `builder.Services.AddSingleton<IStorage, MemoryStorage>()`
3. Add authentication: `builder.Services.AddAgentAspNetAuthentication(builder.Configuration)`
4. Map endpoint: `app.MapPost("/api/messages", async (request, response, adapter, agent, ct) => ...)`

### Agent-to-Agent Communication
- Parent agent uses `IAgentHost` to communicate with other agents
- Child agents require no special code - they are regular agents
- Two delivery modes:
  - `DeliveryModes.Normal`: Async replies (for long-running operations)
  - `DeliveryModes.Stream`: Streaming replies in HTTP response (for chat-style interactions)

## Configuration

### Authentication (appsettings.json)
```json
"Connections": {
  "ServiceConnection": {
    "Settings": {
      "AuthType": "ClientSecret",
      "AuthorityEndpoint": "https://login.microsoftonline.com/{TenantId}",
      "ClientId": "{ClientId}",
      "ClientSecret": "{ClientSecret}",
      "Scopes": ["https://api.botframework.com/.default"]
    }
  }
}
```

### Token Validation
```json
"TokenValidation": {
  "Enabled": false,  // Set true for production
  "Audiences": ["{ClientId}"],
  "TenantId": "{TenantId}"
}
```

## Testing Strategy

- Tests use xUnit framework (v2.9.3 and v3.0.1)
- Test projects target both .NET 8.0 and .NET Framework 4.8
- Tests located in `src/tests/` directory
- Common test helpers in `Microsoft.Agents.Builder.Testing`
- Moq used for mocking (v4.20.72)

## Samples

Samples are in `src/samples/` directory. Each has its own README with setup instructions.

**Key Samples:**
- `EmptyAgent`: Basic agent template - good starting point
- `AgentToAgent`: Demonstrates A2A communication with streaming and normal modes
- `CopilotStudioClient`: Client examples for Copilot Studio integration
- `SemanticKernel/WeatherAgent`: Shows Semantic Kernel integration
- `OTelAgent`: OpenTelemetry instrumentation example

## Important Notes

### Authentication Types
- **ClientSecret/Certificate**: Works with dev tunnels for local debugging
- **Federated Credentials/Managed Identity**: Requires deployment to App Service or container (cannot use dev tunnel)

### Local Development
- Default agent port: `http://localhost:3978`
- Use `devtunnel` for external client connections (Teams, Bot Service)
- BotFramework Emulator or Agents Playground for local testing without tunnels

### Build System
- Uses Central Package Management (`Directory.Packages.props`)
- Multi-targeting: Projects may target both .NET 8.0 and .NET Framework 4.8
- Custom build properties in `Directory.Build.props`, `Build.Shared.props`
- Output directory: `bin/{Configuration}/{ProjectName}/`
- Package versioning via Nerdbank.GitVersioning

### Package Publishing
- Public packages: nuget.org (prefix: `Microsoft.Agents.*`)
- Nightly builds: nuget.org with `-alpha` suffix (updated overnight PT)
