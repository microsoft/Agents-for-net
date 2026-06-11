# SidecarAuth

This sample shows how to authenticate a Microsoft 365 Agent using the **Microsoft Entra ID Agent Container** (the *sidecar*) instead of MSAL. Token acquisition is delegated to the sidecar over its local HTTP API, so the agent never handles secrets or certificates. It is intended for **Agent 365 / agentic** scenarios where the inbound activity carries an agent identity (`Recipient.AgenticAppId`, `Recipient.AgenticUserId`).

The provider and handler used here live in `Microsoft.Agents.Builder.UserAuth.EntraSidecar`. See the [library README](../../../libraries/Builder/Microsoft.Agents.Builder/UserAuth/EntraSidecar/README.md) for the full design and endpoint mapping, and spec [microsoft/Agents#606](https://github.com/microsoft/Agents/issues/606) for background.

This sample demonstrates:

- **Connection-level token acquisition** via `SidecarAccessTokenProvider` (configured as the `ServiceConnection` token provider) — used by the SDK to call back to the channel/Bot Framework as the agentic identity.
- **Per-route user authorization** via `SidecarUserAuthorization` — the `me` handler acquires a `User.Read` token for the agentic user.
- **Sidecar health check** — `SidecarHealthCheck` is exposed at `/health` to report whether the sidecar is reachable, and an optional startup probe verifies reachability once at startup.

## How it works

```
Agent  ──►  Sidecar (http://localhost:5178)  ──►  Microsoft Entra ID
       GET /AuthorizationHeaderUnauthenticated/{serviceName}
           ?AgentIdentity={agentAppInstanceId}&AgentUserId={agenticUserId}
       ◄── { "authorizationHeader": "Bearer <token>" }
```

The sidecar performs the entire agentic identity chain internally (Blueprint → Instance → agentic User via federated identity) and returns the final token. The SDK only translates each token request into a single sidecar call; `AgentIdentity`/`AgentUserId` come from the inbound activity at runtime.

## Prerequisites

- [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- The **Entra ID Agent Container** running locally and reachable. The container listens on `http://localhost:5178` by default (its local default port); override with the `SIDECAR_URL` environment variable or the `SidecarBaseUrl` setting.
- An [agentic-enabled](https://aka.ms/agent365enable) agent (Blueprint) registered in your tenant.
- A [dev tunnel](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started?tabs=windows) for testing from Teams / M365.

## Configure the sidecar (downstream APIs)

The sidecar's configuration must declare the downstream APIs this sample references:

| Downstream API name | Used by | Configuration |
|---|---|---|
| `botframework` | `ServiceConnection` (channel callbacks) | Scope `5a807f24-c9de-44ee-a3a7-329e88a00ffc/.default` |
| `me` | `SidecarUserAuthorization` (`-me` route) | Scope `User.Read` |
| `agenticblueprint` | `GetAgenticApplicationTokenAsync` | App-only (`RequestAppToken: true`), scope `api://AzureAdTokenExchange/.default` |

The sidecar holds the agent (Blueprint) credential. If the sidecar base URL differs from the default, set the `SIDECAR_URL` environment variable or the `SidecarBaseUrl` setting.

## Configure the agent

Edit `appsettings.json` (or, for local development, `appsettings.Development.json`).

1. Configure the connection to use the sidecar provider:

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

2. The `me` user-authorization handler is already configured to use the sidecar:

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

3. Set `TokenValidation` for your tenant. For local debugging, the audience is the agent Blueprint app ID:

   ```json
   "TokenValidation": {
     "Enabled": true,
     "Audiences": [ "{{BlueprintAppId}}" ],
     "TenantId": "{{TenantId}}"
   }
   ```

> Do not commit secrets or real tenant/app IDs. `appsettings.Development.json` is git-ignored — keep environment-specific values there.

## Run

1. Ensure the sidecar is running and healthy:

   ```bash
   curl http://localhost:5178/healthz
   ```

2. Start a dev tunnel and point your agent's messaging endpoint at it:

   ```bash
   devtunnel host -p 3978 --allow-anonymous
   ```

3. Run the agent:

   ```bash
   dotnet run --project src/samples/Authorization/SidecarAuth
   ```

   The agent listens on `http://localhost:3978`; the messaging endpoint is `/api/messages`. The sidecar reachability probe is at `http://localhost:3978/health`. An optional startup probe (`AddSidecarStartupProbe`) also checks the sidecar once at startup — warn-only by default, or pass `failOnUnreachable: true` to fail fast.

## Test in Teams / M365

1. Update `manifest/manifest.json` with your agent app ID and tunnel domain, then zip the `manifest` folder contents (`manifest.json`, `color.png`, `outline.png`).
2. Upload the custom app and start a chat with the agent.
3. Send **`-me`** to sign in via the sidecar and have the agent read your profile from Microsoft Graph. Send any other message to see it echoed back.

> **Sign-out:** the sidecar owns the token cache and exposes no revoke endpoint in this phase, so `SignOutUserAsync`/`ResetStateAsync` are intentional no-ops and there is no `-signout` command.

If a token request fails, the sidecar returns an RFC 7807 error which the SDK surfaces internally; the underlying AADSTS detail is logged at the `Microsoft.Agents` debug level.

## Further reading

- [EntraSidecar provider library README](../../../libraries/Builder/Microsoft.Agents.Builder/UserAuth/EntraSidecar/README.md)
- [Microsoft 365 Agents SDK](https://github.com/microsoft/agents-for-net)
