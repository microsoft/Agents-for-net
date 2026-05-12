# Copilot Instructions for Agents-for-net

## Build and Test

```bash
# Build entire solution
dotnet build src/Microsoft.Agents.SDK.sln

# Build via top-level project (includes all libraries)
dotnet build AgentSdk.proj

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

The SDK is organized into layered libraries under `src/libraries/`:

- **Core** (`Core/Microsoft.Agents.Core`) — Activity Protocol models, serialization (`ProtocolJsonSerializer` using `System.Text.Json`), validation, and telemetry primitives. Foundation for everything else.
- **Builder** (`Builder/Microsoft.Agents.Builder`) — Agent construction framework. Key types: `AgentApplication` (route-based handler registration), `IAgent`, `ITurnContext`, `ITurnState`. Agents subclass `AgentApplication` and register handlers in the constructor via `OnActivity()`, `OnConversationUpdate()`, etc.
- **Hosting** (`Hosting/AspNetCore`) — ASP.NET Core integration. Extension methods `AddAgent<T>()`, `AddAgentAspNetAuthentication()`, `MapAgentApplicationEndpoints()`. Default endpoint: `/api/messages`.
- **Client** (`Client/`) — Agent-to-Agent communication (`IAgentHost`), Azure Bot Service connectivity (`Connector`), Copilot Studio client.
- **Extensions** (`Extensions/`) — Platform-specific: Teams, SharePoint.
- **Storage** (`Storage/`) — `IStorage` abstraction with `MemoryStorage` (dev), Blob, and CosmosDb implementations.

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
- Output goes to `bin/{Configuration}/{ProjectName}/`.

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

### Terminology
- **Agent-to-Agent** refers to SDK agents communicating via the Activity Protocol — not the A2A open spec (`github.com/a2aproject/A2A`).
- **`DeliveryModes.Stream`** is an SSE transport mechanism, unrelated to A2A.

### Authentication
- MSAL-based auth (`Authentication.Msal`) supports ClientSecret, Federated Credentials, and Managed Identity.
- ClientSecret/Certificate works with dev tunnels for local debugging; Federated/Managed Identity requires deployed infrastructure.
- Local dev port: `http://localhost:3978`.
