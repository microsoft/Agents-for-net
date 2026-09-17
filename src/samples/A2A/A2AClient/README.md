# A2AClient

`A2AClient` is an interactive console client for an A2A agent. It resolves the public Agent Card anonymously, selects a JSON-RPC or HTTP+JSON interface on the configured origin, predicts the intended skill, and chooses the matching authentication requirement from the Agent Card before starting a task.

For the current `A2AAgent` sample, automatic mode recognizes two delegated device-code schemes:

- `-me` -> Microsoft Entra Device Code for `api://<agent-client-id>/access_as_user`
- `-issues` -> GitHub Device Flow for `repo`

`--auth-mode` and `:auth` remain explicit testing overrides. Discovery is always anonymous.

## Run the client

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

The current startup options are:

```text
--agent <url>
--auth-mode none|delegated|app
--history
--use-push-notifications
--push-notification-receiver <url>
--help
```

While the client is running, use these interactive commands:

- `:auth auto|none|delegated|app` switches the authentication mode for future requests. `auto` restores Agent Card-driven selection.
- `:history on|off` turns task history display on or off.
- `:q` or `quit` exits the console.

Changing the mode drops any task the agent is waiting on so one caller's continuation is never resumed with another caller's credential.

## Configuration keys

`src\samples\A2A\A2AClient\appsettings.json` contains placeholders for these local client keys:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "TenantId": "<tenant-id>",
    "PublicClientId": "<agent-client-id>",
    "GitHubClientId": "<github-client-id>",
    "ConfidentialClientId": "<confidential-client-id>",
    "ConfidentialClientSecret": ""
  }
}
```

When `--auth-mode` is omitted, the client matches the input against advertised skill examples first, then falls back to a deterministic term score over the skill name, description, tags, and examples. That heuristic only chooses which Agent Card requirement to use; it does not constrain the server's own routing.

A selected requirement must advertise a supported OAuth flow with absolute HTTPS endpoints and non-empty acquisition scopes. The client then chooses the provider-specific device-code implementation:

- Microsoft Entra -> uses `Authentication:TenantId` and `Authentication:PublicClientId`
- GitHub -> uses `Authentication:GitHubClientId`

The selected card security requirement provides the Agent API acquisition scopes, while the selected OAuth scheme provides the token endpoint and device authorization endpoint.

Entra acquisition re-enters MSAL for every protected request so MSAL can use its account cache and silently renew tokens when needed. GitHub tokens are cached separately by Agent Card security scheme: a token with `expires_in` is reused only until one minute before expiration, while a token whose response omits `expires_in` is treated as non-expiring and retained.

## Provider-specific delegated setup

### `-me`

`PublicClientId` is used for delegated Microsoft Entra device-code authentication. For the sample's `-me` route, it must be the **A2A Agent API app registration client ID** so the inbound token is exchangeable through OBO. That is the same ID used in the sample agent's `TokenValidation:Audiences` setting and in the advertised scope `api://<agent-client-id>/access_as_user`.

Do not acquire a Microsoft Graph token in the client and send it directly to the agent. The agent expects an Agent API token and performs the Graph `User.Read` exchange itself.

### `-issues`

`GitHubClientId` is used for delegated GitHub device-flow authentication when the selected Agent Card scheme points at `https://github.com/login/device/code` and `https://github.com/login/oauth/access_token`.

The client acquires the opaque GitHub bearer token, sends it to the agent, and the agent validates and reuses that same token for the assigned-issues call.

## Application mode remains a client capability

`ConfidentialClientId` and `ConfidentialClientSecret` still support application-token testing for other agents whose cards advertise a Client Credentials flow. The current `A2AAgent` sample does not expose an application-protected route, so app mode is a client capability rather than current sample behavior.

Keep real secrets out of source control. The committed `appsettings.json` should stay on placeholders only.

## Expected failures and how to interpret them

- `-me` with `:auth none` fails because the route requires a validated inbound Agent API token.
- `-me` with `:auth app` fails because Microsoft Graph OBO requires a delegated user token.
- `-issues` with `:auth none` fails because the route requires a validated GitHub token.
- `-issues` with a GitHub token that lacks the exact `repo` scope fails during authentication.
- An Agent Card whose interface URL is not on the configured agent origin fails before any credential is sent.

Access tokens are attached to requests but are never printed by the sample, including in the error text shown after a failed request.
