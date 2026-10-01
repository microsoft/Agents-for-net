# Microsoft.Agents.Extensions.A2A

## Preview package for A2A endpoints for Agents SDK

> [!IMPORTANT]
> This package is preview. The namespace organization is intentionally breaking, and
> compatibility wrappers for the previous namespace layout are not provided.

Normal agent authoring and application startup require only the root namespace:

```csharp
using Microsoft.Agents.Extensions.A2A;
```

For setup, Agent Card generation, skills, protocol mapping, and OAuth
configuration patterns, see the
[A2A developer guide](../../../samples/A2A/A2AAgent/A2A-DEVELOPER-GUIDE.md).

## Local unified-operations POC

This branch consumes the unpublished `A2A.AspNetCore`
`1.0.0-design1-poc.1` package, which transitively consumes `A2A`
`1.0.0-design1-poc.1`. Before restoring, provide a NuGet configuration whose
package source mapping resolves `A2A` and `A2A.AspNetCore` from a local feed
containing both POC packages. Do not add the feed path to the repository.

Validate the server integration from the repository root:

```powershell
$nugetConfig = "<path-to-local-poc-nuget-config>"
# RESTORESOURCES overrides --configfile when set.
Remove-Item Env:RESTORESOURCES -ErrorAction SilentlyContinue
dotnet restore src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --configfile $nugetConfig
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --framework net8.0 --no-restore
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj --no-restore
```

The unchanged A2AClient sample continues to consume `A2A`
`1.0.0-preview2`. The combined `Microsoft.Agents.Samples.A2A.Tests` project
must not load that client alongside the Design 1 server package: the client calls
`SendMessageConfiguration.PushNotificationConfig`, which the local API replaced
with `TaskPushNotificationConfig`. The test project therefore restores and
builds its unchanged client tests in a separate `obj\client` directory and
`CplTests.Samples.A2A.Client` output directory, then runs them in a separate
test process against preview2. The outer Restore phase prepares both partitions;
subsequent Build and VSTest phases do not restore:

```powershell
dotnet restore src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --configfile $nugetConfig
dotnet build src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --framework net10.0 --no-restore
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --framework net10.0 --no-build --no-restore
```

The final test command reports both partitions (248 client tests and 17 server
tests), and a failing partition fails the command. Test filters are inherited by
both partitions. The split can be removed when client and server dependencies
share a compatible API.

## API organization

| Area | Namespace | Use |
| --- | --- | --- |
| Root authoring and startup | `Microsoft.Agents.Extensions.A2A` | Agent extension, route attributes and builders, turn-context interfaces, the same-turn `A2AClient`, message helpers, transport protocol, and endpoint registration. |
| Pipeline | `Microsoft.Agents.Extensions.A2A.Pipeline` | Internal implementation for concrete adapters, activities, and turn contexts; transport processing uses upstream A2A processors. |
| Authorization | `Microsoft.Agents.Extensions.A2A.Authorization` | Request-token consumption, OBO configuration, and Agent Card OAuth metadata binding. |
| Agent Card | `Microsoft.Agents.Extensions.A2A.AgentCard` | The public `IAgentCardHandler` final-card customization contract; configuration binding is internal. |
| Integration | `Microsoft.Agents.Extensions.A2A.Integration` | Required Agent SDK registration and serialization-initialization infrastructure. `A2AServiceRegistrar` is public only for Agent SDK discovery. |
| Storage | `Microsoft.Agents.Extensions.A2A.Storage` | A2A task-store implementations. |

Add a responsibility-specific namespace import only when code directly uses an API
from that public area. For example, use `Microsoft.Agents.Extensions.A2A.AgentCard`,
`Microsoft.Agents.Extensions.A2A.Authorization`,
`Microsoft.Agents.Extensions.A2A.Storage` for the corresponding APIs.
