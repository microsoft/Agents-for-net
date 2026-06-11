# Entra ID Auth Sidecar Provider

This namespace integrates the Microsoft 365 Agents SDK with the **Microsoft Entra ID Agent Container** (the *sidecar*). Instead of acquiring tokens directly with MSAL, the SDK delegates token acquisition to the sidecar's HTTP API, so the agent process never handles secrets, certificates, or keys.

Reference spec: [microsoft/Agents#606](https://github.com/microsoft/Agents/issues/606).

## Why a sidecar?

- **Credential-free agent code** — all credential management (Managed Identity, Workload Identity, Key Vault certs, client secret for dev) lives in the sidecar.
- **Language-agnostic** — every language SDK talks to the same simple HTTP API rather than re-implementing MSAL flows.
- **Consistent local/prod** — the same container runs locally (Docker) and in production.

## Components

| Type | Role |
|---|---|
| `SidecarHttpClient` | Reusable HTTP client for the sidecar API (internal). Builds query strings, parses `{ "authorizationHeader": "Bearer <token>" }` responses, and surfaces RFC 7807 `ProblemDetails` errors. |
| `SidecarAccessTokenProvider` | Connection-level provider implementing `IAccessTokenProvider` and `IAgenticTokenProvider`. Translates each SDK token call into a single sidecar request. Loadable from configuration via the `(IServiceProvider, IConfigurationSection)` constructor. |
| `SidecarUserAuthorization` | `IUserAuthorization` handler for per-route user sign-in on agentic requests. Derives `AgentIdentity`/`AgentUserId` from `Activity.Recipient` and calls the sidecar's unauthenticated endpoint (Phase 1). `SignOutUserAsync`/`ResetStateAsync` are intentional no-ops (the sidecar owns the token cache). |
| `SidecarServiceCollectionExtensions` | `AddSidecarConnections(...)` DI helper that registers the `SidecarHttpClient` and provider. |
| `SidecarHealthCheck` / `AddSidecarHealthCheck` | ASP.NET Core `IHealthCheck` (and `IHealthChecksBuilder` extension) that probes the sidecar `/healthz` endpoint on demand. |
| `SidecarStartupHealthCheck` / `AddSidecarStartupProbe` | Optional `IHostedService` that probes the sidecar once at startup. Warn-only by default; opt into fail-fast with `failOnUnreachable: true`. |
| `SidecarSettings` / `SidecarConnectionSettings` | Configuration models for the handler and the connection provider, respectively. |

## Sidecar endpoint mapping

The provider uses the sidecar's unauthenticated endpoint, where `{serviceName}` is a downstream API configured in the sidecar:

```
GET /AuthorizationHeaderUnauthenticated/{serviceName}
    ?AgentIdentity={agentAppInstanceId}
    &AgentUserId={agentUserObjectId}        (delegated/agentic-user flow)
    &AgentUsername={agentUserUpn}           (used instead of AgentUserId when the user is a UPN)
    &optionsOverride.Scopes={scope}         (repeatable)
    &optionsOverride.RequestAppToken=true   (app-only flow)
    &optionsOverride.AcquireTokenOptions.Tenant={tenantId}
```

| `IAgenticTokenProvider` method | Sidecar call |
|---|---|
| `GetAgenticApplicationTokenAsync` | `BlueprintServiceName` (default `agenticblueprint`) downstream API with `AgentIdentity`. Returns the Blueprint (agent application) token. |
| `GetAgenticInstanceTokenAsync` | `ServiceName` downstream API with `AgentIdentity` + `RequestAppToken=true`. App-only resource token for the autonomous agent. |
| `GetAgenticUserTokenAsync` | `ServiceName` downstream API with `AgentIdentity` + `AgentUserId`/`AgentUsername`. Resource token for the agentic user. |
| `GetAccessTokenAsync` (connection-level `IAccessTokenProvider`) | `ServiceName` downstream API with `RequestAppToken=true`. App-only connection token. |

`AgentUserId` (object id) and `AgentUsername` (UPN) are mutually exclusive; the client emits exactly one and rejects a request that sets both.

> **Phase 1 scope.** `SidecarUserAuthorization` uses the sidecar's **unauthenticated** endpoint: the sidecar resolves the full chain from the agent identity parameters. The authenticated (on-behalf-of) endpoint is out of scope for Phase 1 and will be added when that flow is specified.

### How this relates to the spec

Spec #606 (Phase 1) originally anticipated the SDK deriving the Agent Instance and Agent User tokens locally from the Blueprint token. In practice the **sidecar performs the entire agentic identity chain internally** (Blueprint → Instance → agentic User via federated identity) and returns the final resource token. The SDK therefore only needs to translate each call into a single sidecar request — there is no local `client_credentials`/`user_fic` exchange. This is required for agentic *instance* apps, which are `ServiceIdentity`-type service principals whose federated credentials are Entra-managed and cannot be exchanged by the client.

`AgentIdentity` and `AgentUserId` are **not** stored in configuration; they are extracted from the inbound activity (`Recipient.AgenticAppId`, `Recipient.AgenticUserId`) at runtime, so a single deployment can serve multiple agent identities and users.

## Base URL resolution

`SidecarHttpClient.ResolveBaseUrl` resolves the sidecar base URL in this order:

1. `SIDECAR_URL` environment variable
2. Explicit configuration (`SidecarBaseUrl`)
3. `http://localhost:5178` (default — the Entra ID Agent Container's local default port)

## Configuration

### Connection-level provider (replaces MSAL)

```json
"Connections": {
  "ServiceConnection": {
    "Assembly": "Microsoft.Agents.Builder",
    "Type": "Microsoft.Agents.Builder.UserAuth.EntraSidecar.SidecarAccessTokenProvider",
    "Settings": {
      "SidecarBaseUrl": "http://localhost:5178",
      "ServiceName": "botframework",
      "Scopes": [ "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default" ]
    }
  }
}
```

### Per-route user authorization handler

```json
"AgentApplication": {
  "UserAuthorization": {
    "Handlers": {
      "me": {
        "Type": "SidecarUserAuthorization",
        "Settings": {
          "SidecarBaseUrl": "http://localhost:5178",
          "ServiceName": "me",
          "Scopes": [ "User.Read" ]
        }
      }
    }
  }
}
```

### Code-first DI registration

```csharp
builder.Services.AddSidecarConnections(builder.Configuration);
```

### Health check

Register the sidecar reachability probe with the ASP.NET Core health-check pipeline:

```csharp
builder.Services.AddHealthChecks().AddSidecarHealthCheck();
...
app.MapHealthChecks("/health");
```

### Startup probe (optional)

Probe the sidecar once at startup. By default an unreachable sidecar logs a warning and startup continues; pass `failOnUnreachable: true` to fail fast instead:

```csharp
builder.Services.AddSidecarStartupProbe(failOnUnreachable: false);
```

## Sidecar-side requirements

The sidecar must declare matching **downstream APIs**:

- The connection's `ServiceName` (e.g. `botframework`, `me`) with the appropriate scope.
- A Blueprint downstream API named by `BlueprintServiceName` (default `agenticblueprint`), configured **app-only** (`RequestAppToken: true`) with the `api://AzureAdTokenExchange/.default` scope. This is what `GetAgenticApplicationTokenAsync` calls.

See the runnable end-to-end example in [`src/samples/Authorization/SidecarAuth`](../../../../../samples/Authorization/SidecarAuth/README.md).
