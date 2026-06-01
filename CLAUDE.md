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

**Microsoft.Agents.Hosting.DirectLine.NamedPipes** (`src/libraries/Hosting/DirectLine.NamedPipes/`)
- Named pipe transport for DirectLineFlex (Azure App Service sidecar)
- Extension method: `AddAgentNamedPipeTransport()`
- Enables pipe-based agent communication without HTTP roundtrips
- Uses Bot Framework wire protocol (48-byte ASCII framed headers)

**Microsoft.Agents.Authentication.Msal** (`src/libraries/Authentication/Authentication.Msal/`)
- MSAL-based authentication for Azure Bot Service and Entra ID
- Supports ClientSecret, Federated Credentials, and Managed Identity auth types
- Configuration via `appsettings.json` Connections section

### Client Libraries

**Microsoft.Agents.Client** (`src/libraries/Client/Microsoft.Agents.Client/`)
- Agent-to-Agent (Activity Protocol) communication
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
- Full Microsoft Teams extensibility: message extensions, task modules, meeting events, channel/team lifecycle, file consent, message edit/delete/read receipts, config pages
- Enable with `[TeamsExtension]` attribute on a `partial AgentApplication` subclass — source generator creates a `Teams` property of type `TeamsAgentExtension`
- Two routing styles: **fluent builders** (`TeamsExtension.MessageExtensions.OnQuery(...)`) or **declarative attributes** (`[QueryRoute("cmdId")]`)
- Feature areas exposed as properties on `TeamsAgentExtension`:
  - `TeamsExtension.MessageExtensions` — search queries, link unfurling, anonymous link unfurling, action commands, compose previews, card button clicks, settings
  - `TeamsExtension.TaskModules` — modal dialogs (fetch + submit), supports string or Regex key matching
  - `TeamsExtension.Meetings` — start/end, participants join/leave
  - `TeamsExtension.Channels` — created/deleted/renamed/restored/shared/unshared; member add/remove
  - `TeamsExtension.Teams` — archived/unarchived/renamed/deleted/hard-deleted/restored
  - `TeamsExtension.FileConsent` — file upload consent accept/decline
  - `TeamsExtension.Messages` — message edit/delete/undelete, read receipts, O365 connector card actions
  - `TeamsExtension.Config` — config fetch/submit (bot configuration UI)
- `TeamsInfo` static helper — `GetMeetingInfoAsync()`, `GetMeetingParticipantAsync()`, `GetTeamDetailsAsync()`, `GetTeamChannelsAsync()`, `GetPagedMembersAsync()`, `GetPagedTeamMembersAsync()`, `GetTeamMemberAsync()`, `GetMemberAsync()`
- `TeamsTurnContextExtensions` — `SendTargetedActivityAsync()` for sending to specific recipients
- `TeamsActivityExtensions` — `TeamsGetChannelId()`, `TeamsNotifyUser()`, `TeamsEnableFeedbackLoop()`, etc.
- Route builders accept `autoSignInHandlers` and route attributes accept `signInHandlers` parameter for per-route OAuth/SSO flows; Teams SSO and OBO via Azure Bot Token Service are supported
- Legacy `TeamsActivityHandler` in `Compat/` namespace for migration from Bot Framework SDK

```csharp
// Minimal Teams agent setup
[TeamsExtension]
public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [QueryRoute("searchCmd")]
    public Task<Microsoft.Teams.Api.MessageExtensions.Response> OnSearchAsync(
        ITurnContext ctx, ITurnState state,
        Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken ct)
        => Task.FromResult(new Microsoft.Teams.Api.MessageExtensions.Response { ComposeExtension = BuildResults(query) });
}
```

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
1. Register agent: `builder.AddAgent<MyAgent>();`
2. Register storage: `builder.Services.AddSingleton<IStorage, MemoryStorage>()`
3. Add authentication: `builder.Services.AddAgentAspNetAuthentication(builder.Configuration)`
4. Map endpoints: `app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());`
5. See [EmptyAgent startup](https://github.com/microsoft/Agents-for-net/blob/main/src/samples/EmptyAgent/Program.cs)

### Agent-to-Agent Communication
- Parent agent uses `IAgentHost` to communicate with other agents
- Child agents require no special code - they are regular agents
- Two delivery modes:
  - `DeliveryModes.Normal`: Async replies (for long-running operations)
  - `DeliveryModes.Stream`: Streaming replies using SSE.

## Configuration

### Authentication (appsettings.json)
See [Configure authentication in a .NET agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/microsoft-authentication-library-configuration-options)

See [Configure AspNet JWT authentication](src/samples/A2AAgent/AspNetExtensions.cs)

## Testing Strategy

- Tests use xUnit framework (v2.9.3 and v3.0.1)
- Test projects target both .NET 8.0 and .NET Framework 4.8
- Tests located in `src/tests/` directory
- Common test helpers in `Microsoft.Agents.Builder.Testing`
- Moq used for mocking (v4.20.72)

## Test Samples

Samples are in `src/samples/` directory. Each has its own README with setup instructions.  These are similar to the samples in Agents SDK.  They can contain additional functionality used when developing the DotNet SDK.

**Key Samples:**
- `EmptyAgent`: Basic agent template - good starting point
- `CopilotStudioClient`: Client examples for Copilot Studio integration
- `SemanticKernel/WeatherAgent`: Shows Semantic Kernel integration
- `TelemetryAgent`: OpenTelemetry instrumentation example

**Teams Samples** (`src/samples/Teams/`):
- `TeamsAgent`: Teams-specific agent with conversation update handling
- `ConversationAgent`: Proactive messaging and conversation management in Teams
- `MessageExtensions`: Search and action-based message extension examples
- `Compat/CompatConversationBot`, `CompatMessageExtensions`, `CompatTaskModule`: Legacy Bot Framework SDK migration examples

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
- Nightly builds: nuget.org with `-beta` suffix (updated overnight PT)

### Terms
- A2A is not the same as Agent-to-Agent.  A2A is the open spec: https://github.com/a2aproject/A2A, and Agent-to-Agent is communicating with another SDK agent using Activity Protocol: https://github.com/microsoft/Agents/blob/main/specs/activity/protocol-activity.md
- A2A is also not related to `DeliveryModes.Stream`.  The later is a transport mechanism to stream responses using SSE. 
