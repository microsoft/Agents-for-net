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

## API organization

| Area | Namespace | Use |
| --- | --- | --- |
| Root authoring and startup | `Microsoft.Agents.Extensions.A2A` | Agent extension, route attributes and builders, turn-context interfaces, the same-turn `A2AClient`, message helpers, transport protocol, and endpoint registration. |
| Pipeline | `Microsoft.Agents.Extensions.A2A.Pipeline` | Internal implementation for concrete adapters, activities, turn contexts, and HTTP/JSON-RPC processors. |
| Authorization | `Microsoft.Agents.Extensions.A2A.Authorization` | Request-token consumption, OBO configuration, and Agent Card OAuth metadata binding. |
| Agent Card | `Microsoft.Agents.Extensions.A2A.AgentCard` | The public `IAgentCardHandler` final-card customization contract; configuration binding is internal. |
| Integration | `Microsoft.Agents.Extensions.A2A.Integration` | Required Agent SDK registration and serialization-initialization infrastructure. `A2AServiceRegistrar` is public only for Agent SDK discovery. |
| Storage | `Microsoft.Agents.Extensions.A2A.Storage` | A2A task-store and encrypted push-registration store implementations. |

Add a responsibility-specific namespace import only when code directly uses an API
from that public area. For example, use `Microsoft.Agents.Extensions.A2A.AgentCard`,
`Microsoft.Agents.Extensions.A2A.Authorization`,
`Microsoft.Agents.Extensions.A2A.Storage` for the corresponding APIs.

## Encrypted persistent push registrations

`BlobPushNotificationStore` implements the upstream `A2A.IPushNotificationStore`
using Azure Blob Storage and ASP.NET Core Data Protection. It stores an entire
task's registrations as one authenticated, encrypted document. Callback URLs,
tokens, credentials, configuration IDs, and generations are encrypted; blob
names contain hashes of the trusted scope and task ID, not their raw values.
Conditional ETag writes reject duplicate creation and prevent lost updates.

> [!IMPORTANT]
> This is an opt-in storage component, not push enablement. The Agents SDK A2A
> adapter does not currently consume this registration or enable an upstream
> push pipeline. Applications composing an enabled upstream pipeline can supply
> this store as its `IPushNotificationStore`. Registering the store alone does
> not enable push endpoints, advertise push capability, or make delivery durable.

Provision a **private container** first. Supply a `BlobContainerClient` using
managed identity or another least-privilege credential. The store does not
create containers. HTTPS is required; Azurite HTTP loopback endpoints require
`BlobPushNotificationStoreOptions.AllowInsecureLoopback = true`.

The host must already register an `IDataProtectionProvider` with a **durable,
protected key ring**, a stable application name, and keys kept separately from
registration records. For example, use a dedicated key-ring store protected
with Azure Key Vault. `AddDataProtection()` alone is not a production key
management policy. Ephemeral keys cannot recover registrations after restart.

```csharp
using A2A;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Agents.Extensions.A2A.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

// Configure the host's durable, protected Data Protection key ring first.
builder.Services.AddSingleton<IPushNotificationStore>(services =>
    new BlobPushNotificationStore(
        new BlobContainerClient(
            new Uri("https://<account>.blob.core.windows.net/a2a-push"),
            new DefaultAzureCredential()),
        services.GetRequiredService<IDataProtectionProvider>(),
        scope: "trusted-agent-scope"));
```

**Authorization and topology:** Use one live singleton store per authorized
scope in one active host. Derive `scope` from trusted host configuration, never
the request's `Tenant`, task ID, or configuration ID. Authorize every operation
before accessing the store and keep task and push storage in the same
authorization boundary. Background delivery must use that same fixed scope
without retaining an inbound `HttpContext`. Incoming tenant fields do not
change the storage scope.

Deletion signals snapshots in this store instance before returning; stale
version-conditional deletion cannot remove a recreated registration. Local
signals and the upstream execution, subscription, and delivery queues do not
provide cross-host coordination. Shared blobs and keys **do not make multiple
active store owners safe**. No distributed invalidation or ownership lease is
provided. See
[a2aproject/a2a-dotnet#463](https://github.com/a2aproject/a2a-dotnet/issues/463).
Deletion cannot recall an HTTP request already transmitted.

**Limits and failures:** Defaults are 100 registrations per task, 64 KiB per
serialized configuration, 8 MiB per encrypted task document, and five
conditional write attempts. Options are captured at construction. Reads are
bounded, and pagination cursors are protected and bound to the task, scope,
and document revision; registration changes invalidate existing cursors.
Missing containers, outages, decryption failures, and contention exhaustion
fail explicitly rather than appearing as empty storage. Errors do not include
registration secrets. Do not log store results or capture decrypted payloads
in application diagnostics.

**Operations:** Enforce per-scope admission quotas and retention in the host.
Before removing a task, delete its registrations using `GetAllAsync` and
version-conditional `DeleteAsync`; keep an authorized administrative cleanup
path for tasks already removed. Do not expire active registrations silently.
Empty task documents retain their revision until authorized maintenance
removes them.

Retain old Data Protection keys while their registrations or backups remain
needed. Plan key rotation, denied-decrypt recovery, and restricted backup and
replica access. Never restore old registrations into service without
reconciling deletion and credential revocation. Registration deletion does not
revoke credentials at their issuer or erase backups. Renew credentials through
new registration generations and delete old generations; the store does not
refresh credentials. Persisting registrations does not persist the upstream
sender's accepted work or retries, and is not a transactional outbox.
