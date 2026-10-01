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

This branch automatically references the reviewed sibling
`a2aproject\a2a-dotnet` checkout when present. `src\A2A.Local.props` shares this
selection with the combined sample test project; `A2ADotNetRepoRoot` can select
another checkout explicitly. Normal package references remain in place when
no local checkout is selected, but the currently pinned package predates the
server operation APIs and cannot build this POC until a compatible release is
adopted.

Validate the server integration from the repository root:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --framework net8.0
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
```

The unchanged A2AClient sample still builds separately against the package API.
The combined `Microsoft.Agents.Samples.A2A.Tests` project must not load that
client alongside this POC's local A2A assembly: the client calls
`SendMessageConfiguration.PushNotificationConfig`, which the local API replaced
with `TaskPushNotificationConfig`. Recompiling the unchanged client against
the local project also fails. With local references selected, the existing test
project builds its unchanged client tests in a separate `obj\client` directory
and `CplTests.Samples.A2A.Client` output directory, and runs them in a separate
test process against the package. Server tests keep the local project graph.
No additional project or client source changes are needed:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --framework net10.0
```

This command reports both partitions (248 client tests and 17 server tests),
and a failing partition fails the command. Test filters are inherited by both
partitions. Without local references, the ordinary combined package graph is
retained. The split can be removed when client and server dependencies share a
compatible API; disabling local references alone cannot build this POC.

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
