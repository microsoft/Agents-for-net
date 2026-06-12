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
| `SidecarAuth` | Connection-level provider implementing `IAccessTokenProvider` and `IAgenticTokenProvider`. Translates each SDK token call into a sidecar request and serves repeat requests from an in-memory token cache (see [Token caching](#token-caching)). Loadable from configuration via the `(IServiceProvider, IConfigurationSection)` constructor. |
| `SidecarServiceCollectionExtensions` | `AddSidecarConnections(...)` DI helper that registers the `SidecarHttpClient` and provider. |
| `SidecarHealthCheck` / `AddSidecarHealthCheck` | ASP.NET Core `IHealthCheck` (and `IHealthChecksBuilder` extension) that probes the sidecar `/healthz` endpoint on demand. |
| `SidecarStartupHealthCheck` / `AddSidecarStartupProbe` | Optional `IHostedService` that probes the sidecar once at startup. Warn-only by default; opt into fail-fast with `failOnUnreachable: true`. |
| `SidecarConnectionSettings` | Configuration model (bound from the connection `Settings`) carrying `ServiceName`, `BlueprintServiceName`, `Scopes`, `SidecarBaseUrl`, `RequestTimeout`, `RetryCount`, plus inherited connection settings (`ClientId`, `TenantId`). |

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

### How this relates to the spec

Spec #606 (Phase 1) originally anticipated the SDK deriving the Agent Instance and Agent User tokens locally from the Blueprint token. In practice the **sidecar performs the entire agentic identity chain internally** (Blueprint → Instance → agentic User via federated identity) and returns the final resource token. The SDK therefore only needs to translate each call into a single sidecar request — there is no local `client_credentials`/`user_fic` exchange. This is required for agentic *instance* apps, which are `ServiceIdentity`-type service principals whose federated credentials are Entra-managed and cannot be exchanged by the client.

`AgentIdentity` and `AgentUserId` are **not** stored in configuration; they are extracted from the inbound activity (`Recipient.AgenticAppId`, `Recipient.AgenticUserId`) at runtime, so a single deployment can serve multiple agent identities and users.

## Token caching

`SidecarAuth` keeps a lightweight in-memory token cache (mirroring the MSAL provider's SDK-side cache) so repeated calls for the same identity don't hit the sidecar on every turn:

- **Key** — the agent identity (`AgentIdentity` client id) plus the other request parameters that change the issued token: downstream `serviceName`, `AgentUsername`/`AgentUserId`, tenant, app-only vs. user, and scopes. Distinct flows, users, tenants, and scope sets never share an entry.
- **Lifetime** — the token's own JWT `exp` claim when parseable, otherwise a conservative 5-minute fallback for opaque tokens. An entry is evicted once it is within **30 seconds** of expiry, so callers never receive a token that expires mid-flight.
- **Force refresh** — `GetAccessTokenAsync(..., forceRefresh: true)` evicts the entry and re-acquires from the sidecar (also setting `optionsOverride.AcquireTokenOptions.ForceRefresh=true` so the sidecar bypasses its own cache).

This is in addition to the sidecar's own server-side token cache and the expiry hint surfaced to Azure.Core via `GetTokenCredential()`.

## Base URL resolution

`SidecarHttpClient.ResolveBaseUrl` resolves the sidecar base URL in this order:

1. `SIDECAR_URL` environment variable
2. Explicit configuration (`SidecarBaseUrl`)
3. `http://localhost:5178` (default — the Entra ID Agent Container's local default port)

### Loopback/private-address safety check (SSRF)

Because the sidecar issues tokens for the agent's identity, the provider refuses to send requests to an arbitrary host. After resolution, the base URL **must** point to a loopback (`localhost`, `127.0.0.0/8`, `::1`) or private address (RFC 1918 / RFC 4193 / link-local). A public/routable address is rejected with an `InvalidOperationException` at construction time. This applies regardless of whether the URL came from `SIDECAR_URL`, the `SidecarBaseUrl` setting, or the default.

To intentionally target a non-private address (e.g. a sidecar reachable at a routable address inside a carefully validated private network), set **`BypassLocalNetworkRestriction: true`** in the connection `Settings`. This is **UNSAFE** and disables the SSRF guard entirely — only enable it for a network configuration you have explicitly validated, never in a default or untrusted deployment.

## Configuration

### Connection-level provider (replaces MSAL)

```json
"Connections": {
  "ServiceConnection": {
    "Assembly": "Microsoft.Agents.Authentication.EntraAuthSidecar",
    "Type": "Microsoft.Agents.Authentication.EntraAuthSidecar.SidecarAuth",
    "Settings": {
      "SidecarBaseUrl": "http://localhost:5178",
      "ServiceName": "botframework",
      "Scopes": [ "5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default" ],
      "RequestTimeout": "00:00:30",
      "RetryCount": 3
    }
  }
}
```

| Setting | Required | Default | Description |
|---|---|---|---|
| `ServiceName` | No | `default` | Downstream API name configured in the sidecar. |
| `BlueprintServiceName` | No | `agenticblueprint` | Downstream API name for the Blueprint (agent application) token-exchange step. Used by `GetAgenticApplicationTokenAsync`; must be configured app-only with the `api://AzureAdTokenExchange/.default` scope on the sidecar. |
| `Scopes` | No | — | Scope overrides forwarded as `optionsOverride.Scopes`. |
| `SidecarBaseUrl` | No | `http://localhost:5178` | Sidecar endpoint. Resolution: `SIDECAR_URL` env var > this > default. The resolved host must be loopback/private unless `BypassLocalNetworkRestriction` is set. |
| `BypassLocalNetworkRestriction` | No | `false` | **UNSAFE.** Disables the loopback/private-address SSRF safety check. Only enable for a carefully validated private-network configuration (see above). |
| `RequestTimeout` | No | `00:00:30` | Per-attempt HTTP timeout for sidecar calls. |
| `RetryCount` | No | `3` | Retry attempts for transient failures (HTTP 408/429/5xx, network errors, timeouts) using exponential backoff. `0` disables retries. |
| `ClientId` / `TenantId` | No | — | Inherited from `ConnectionSettingsBase`; surfaced via `ConnectionSettings`. Not used for token acquisition (the sidecar owns the credential). |

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

- The connection's `ServiceName` (e.g. `botframework`) with the appropriate scope.
- A Blueprint downstream API named by `BlueprintServiceName` (default `agenticblueprint`), configured **app-only** (`RequestAppToken: true`) with the `api://AzureAdTokenExchange/.default` scope. This is what `GetAgenticApplicationTokenAsync` calls.

See the runnable end-to-end example in [`src/samples/Authorization/SidecarAuth`](../../../samples/Authorization/SidecarAuth/README.md).
