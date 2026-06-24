# Copilot Instructions for Agents-for-net

## Project Overview

This is the Microsoft 365 Agents SDK for .NET — a framework for building enterprise-grade conversational agents that work across M365, Teams, Copilot Studio, and other platforms.

**Current State:** Generally Available (GA)  
**Documentation:** https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/

## Build and Test

```bash
# Build entire solution
dotnet build src/Microsoft.Agents.SDK.sln

# Build via top-level project (includes all libraries)
dotnet build AgentSdk.proj

# Restore dependencies
dotnet restore AgentSdk.proj

# Run all tests
dotnet test --no-build -c Debug ./src/

# Run tests for a single project
dotnet test src/tests/Microsoft.Agents.Core.Tests/

# Run a single test
dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"

# Create NuGet packages
dotnet pack --no-build -c Debug src/Microsoft.Agents.SDK.sln
```

## Architecture

The SDK is organized into layered libraries under `src/libraries/`. See `doc/architecture.md` for a architecture overview.

### Agent Pattern

```csharp
public class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }
}
```

### ASP.NET Core Startup

```csharp
builder.AddAgent<MyAgent>();
builder.Services.AddSingleton<IStorage, MemoryStorage>();
builder.Services.AddAgentAspNetAuthentication(builder.Configuration);
app.MapAgentApplicationEndpoints(requireAuth: !app.Environment.IsDevelopment());
```

See `src/samples/EmptyAgent/Program.cs` for the canonical minimal example.

### Agent-to-Agent Communication

Parent agents use `IAgentHost` to talk to child agents. Child agents need no special code. Two delivery modes: `DeliveryModes.Normal` (async replies) and `DeliveryModes.Stream` (SSE streaming).

## Key Conventions

### Build System
- **Central Package Management** via `Directory.Packages.props` — all package versions are declared there.
- Core libraries **multi-target** `net8.0` and `netstandard2.0` (set in `src/Build.Common.core.props`).
- **TreatWarningsAsErrors** is enabled globally (`src/Build.Shared.props`).
- Package versioning uses **Nerdbank.GitVersioning** (`src/libraries/version.json`). Release branches follow the pattern `rel/v{version}`.
- .NET SDK version pinned in `global.json` (8.0.x with `rollForward: latestMajor`).

### Serialization
- Uses **`System.Text.Json`** exclusively (not Newtonsoft). The central serializer is `ProtocolJsonSerializer` in `Microsoft.Agents.Core.Serialization`.
- Custom converters live in `Core/Microsoft.Agents.Core/Serialization/Converters/`.

### Nullability
- Libraries use `<Nullable>annotations</Nullable>` (annotations only, warnings not enforced).

### Testing
- **xUnit** (v2.9.3 and v3.0.1), **Moq** for mocking.
- Test projects are under `src/tests/` and may target both .NET 8.0 and .NET Framework 4.8.
- Test helpers in `Microsoft.Agents.Builder.Testing`.
- Telemetry tests use `[Collection("TelemetryTests")]` to disable parallel execution (avoids `ActivitySource` listener conflicts).
- Do not use Task.Delay in tests as they cause flakiness. Use syncronization primitives or test-specific hooks instead.

### Authentication
- MSAL-based auth (`Authentication.Msal`) supports ClientSecret, Federated Credentials, and Managed Identity.
- `AddAgentAspNetAuthentication` is defined in sample-local `AspNetExtensions.cs` files, not in the hosting library.
- Local dev port: `http://localhost:3978`.

### Terminology
- **Agent-to-Agent** refers to SDK agents communicating via the Activity Protocol — not the A2A open spec (`github.com/a2aproject/A2A`).
- **`DeliveryModes.Stream`** is an SSE transport mechanism, unrelated to A2A.

## Configuration

### Authentication (appsettings.json)
- See [Configure authentication in a .NET agent](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/microsoft-authentication-library-configuration-options)
- See [Configure AspNet JWT authentication](src/samples/A2AAgent/AspNetExtensions.cs)
- **ClientSecret/Certificate**: Works with dev tunnels for local debugging.
- **Federated Credentials/Managed Identity**: Requires deployment to App Service or container (cannot use dev tunnel).

## Samples

Samples are in `src/samples/`. Each has its own README with setup instructions.

**Key Samples:**
- `EmptyAgent`: Basic agent template — good starting point
- `CopilotStudioClient`: Client examples for Copilot Studio integration
- `SemanticKernel/WeatherAgent`: Shows Semantic Kernel integration
- `TelemetryAgent`: OpenTelemetry instrumentation example

## Local Development

- Default agent port: `http://localhost:3978`
- Use `devtunnel` for external client connections (Teams, Bot Service)
- Use Agents Playground for local testing without tunnels

## Package Publishing

- Public packages: nuget.org (prefix: `Microsoft.Agents.*`)
- Nightly builds: nuget.org with `-beta` suffix (updated overnight PT)
