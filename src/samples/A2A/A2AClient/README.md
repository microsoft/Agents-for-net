# A2AClient

`A2AClient` is an interactive console client for an A2A agent. It resolves the public Agent Card anonymously, selects a JSON-RPC or HTTP+JSON interface on the configured origin, predicts the intended skill, and chooses the matching authentication requirement before starting a task. It also activates the optional Agents SDK in-task authorization extension when advertised and handles `TASK_STATE_AUTH_REQUIRED` by acquiring the requested OAuth credential and calling `resumeAuth`.

OAuth acquisition is driven by either the selected Agent Card security scheme or an in-task authorization request. The client supports these A2A OAuth flows without provider-specific token clients:

- Device Code
- Authorization Code, with a loopback callback and optional PKCE
- Client Credentials

For the current `A2AAgent` sample, automatic mode recognizes:

- `-me` -> Device Code for `api://<agent-client-id>/access_as_user`
- `-issues` -> Device Code for `repo`

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

While the client is running:

- `:auth auto|none|delegated|app` changes authentication mode for future requests.
- `:history on|off` changes task history display.
- `:q` or `quit` exits.

Changing authentication mode drops any task awaiting continuation so one caller's task is never resumed with another credential.

## OAuth connection profiles

The Agent Card supplies the OAuth flow, endpoints, and required scopes. Local connection profiles supply the registered OAuth client and restrict which endpoint origins may receive that client's credentials.

Connection names match Agent Card security-scheme names. In-task authorization
uses the `delegated` connection because its runtime metadata intentionally does
not create an Agent Card security scheme:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "Connections": {
      "delegated": {
        "ClientId": "<agent-api-public-client-id>",
        "AllowedOrigins": [
          "https://login.microsoftonline.com"
        ],
        "AdditionalScopes": [
          "offline_access"
        ]
      },
      "github": {
        "ClientId": "<github-client-id>",
        "AllowedOrigins": [
          "https://github.com"
        ]
      }
    }
  }
}
```

Supported connection properties:

| Property | Purpose |
|---|---|
| `ClientId` | OAuth client registration identifier. Required for all supported flows. |
| `ClientSecret` | Confidential client credential. Keep it in user secrets or environment configuration. |
| `RedirectUri` | HTTP loopback callback registered for Authorization Code. The path must end in `/`. |
| `AllowedOrigins` | HTTPS origins allowed for Agent Card authorization, device authorization, and token endpoints. |
| `AdditionalScopes` | Local protocol scopes added to Agent Card acquisition scopes, such as `offline_access`. |
| `TokenEndpointAuthenticationMethod` | `None`, `ClientSecretBasic`, or `ClientSecretPost`. |
| `UsePkce` | Enables Authorization Code PKCE. Defaults to `true`. |

The client rejects an advertised OAuth endpoint unless its origin appears in the selected connection's `AllowedOrigins`. This prevents an untrusted Agent Card from directing a configured client secret to another host.

## Generic provider examples

### Microsoft Entra Device Code

The `delegated` connection's `ClientId` must be the A2A Agent API public-client registration. The Agent Card supplies the `api://<agent-client-id>/access_as_user` scope and Entra endpoints. `offline_access` is configured locally so an expiring access token can be refreshed without repeating device sign-in.

Do not acquire a Microsoft Graph token and send it directly to the agent. The agent expects an Agent API token and performs the Graph `User.Read` exchange itself.

### GitHub Device Code

The `github` connection contains only the GitHub OAuth App client ID and trusted GitHub origin. The same generic Device Code executor handles GitHub; there is no GitHub-specific token acquisition implementation.

The acquired opaque token is sent to the agent. The sample agent validates it, requires `repo`, and reuses the validated token for the assigned-issues request.

### LinkedIn Authorization Code

An Agent Card can advertise LinkedIn's Authorization Code endpoints and scopes. A matching local connection can be configured without adding LinkedIn source code:

```json
{
  "Authentication": {
    "Connections": {
      "linkedin": {
        "ClientId": "<linkedin-client-id>",
        "ClientSecret": "<linkedin-client-secret>",
        "RedirectUri": "http://localhost:8400/callback/",
        "AllowedOrigins": [
          "https://www.linkedin.com"
        ],
        "TokenEndpointAuthenticationMethod": "ClientSecretPost",
        "UsePkce": true
      }
    }
  }
}
```

Register the same redirect URI with the provider. When the flow starts, the client opens the authorization URL in the default browser, validates the callback state, exchanges the code, and caches or refreshes the resulting token.

## Token lifetime

Tokens are cached by security scheme, flow, and acquisition scopes. A token with `expires_in` is reused until one minute before expiration. If the response includes a refresh token, the client uses it at expiration; otherwise it runs the advertised flow again. A token without `expires_in` is retained as non-expiring.

Keep real secrets out of source control. The committed `appsettings.json` contains placeholders only.

## Expected failures

- A protected request fails when no connection matches the selected Agent Card security scheme.
- An endpoint outside `AllowedOrigins` is rejected before credentials are sent.
- Authorization Code fails when `RedirectUri` is absent or is not an HTTP loopback URI.
- A connection using `ClientSecretBasic` or `ClientSecretPost` fails when `ClientSecret` is absent.
- `-me` with `:auth app` fails because the current Agent Card does not advertise Client Credentials for that skill.
- `-issues` with `:auth none` fails because the route requires a validated GitHub token.

Access tokens are attached to requests but are never printed, including in failed-request output.
