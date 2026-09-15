# Microsoft.Agents.Extensions.A2A

## Preview package for A2A endpoints for Agents SDK

> [!IMPORTANT]
> This package is preview. The namespace organization is intentionally breaking, and
> compatibility wrappers for the previous namespace layout are not provided.

Normal agent authoring and application startup require only the root namespace:

```csharp
using Microsoft.Agents.Extensions.A2A;
```

## API organization

| Area | Namespace | Use |
| --- | --- | --- |
| Root authoring and startup | `Microsoft.Agents.Extensions.A2A` | Agent extension, route attributes and builders, turn-context interfaces, the same-turn `A2AClient`, message helpers, transport protocol, and endpoint registration. |
| Pipeline | `Microsoft.Agents.Extensions.A2A.Pipeline` | Internal implementation for concrete adapters, activities, turn contexts, and HTTP/JSON-RPC processors. |
| Authorization | `Microsoft.Agents.Extensions.A2A.Authorization` | Request-token consumption, OBO configuration, and Agent Card OAuth metadata binding. |
| Agent Card | `Microsoft.Agents.Extensions.A2A.AgentCard` | The public `IAgentCardHandler` final-card customization contract; configuration binding is internal. |
| Integration | `Microsoft.Agents.Extensions.A2A.Integration` | Required Agent SDK registration and serialization-initialization infrastructure. `A2AServiceRegistrar` is public only for Agent SDK discovery. |
| Storage | `Microsoft.Agents.Extensions.A2A.Storage` | A2A task-store implementations. |

Add a responsibility-specific namespace import only when code directly uses an API
from that public area. For example, use `Microsoft.Agents.Extensions.A2A.AgentCard`,
`Microsoft.Agents.Extensions.A2A.Authorization`,
`Microsoft.Agents.Extensions.A2A.Storage` for the corresponding APIs.
