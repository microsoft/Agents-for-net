# A2AClient

`A2AClient` is an interactive console client for connecting to an A2A agent. It
does not require prior knowledge of the agent's skills, protocol binding,
authentication provider, OAuth endpoints, or scopes.

At startup, the client:

1. Resolves the agent's public Agent Card anonymously.
1. Selects a JSON-RPC or HTTP+JSON interface on the configured agent origin.
1. Uses the Agent Card's skill examples to predict the skill for each message.
1. Evaluates the selected skill's security requirements and OAuth metadata.
1. Acquires a token when the selected requirement matches a configured local
   OAuth connection.
1. Activates the optional Agents SDK in-task authorization extension when the
   Agent Card advertises it.

The client supports these OAuth flows without provider-specific code:

- Device Code
- Authorization Code with a loopback redirect and optional PKCE
- Client Credentials

## Run the client

```powershell
dotnet run --project src\samples\A2A\A2AClient\A2AClient.csproj -- --agent http://localhost:3978/a2a
```

The startup options are:

```text
--agent <url>
--auth-mode none|delegated|app
--history
--use-push-notifications
--push-notification-receiver <url>
--help
```

`--agent` overrides `A2A:AgentUrl` from configuration.

While the client is running:

- `:auth auto|none|delegated|app` changes authentication selection for future
  requests.
- `:history on|off` changes task history display.
- `:q` or `quit` exits.

`auto` uses the selected skill's advertised security requirements. `none`
sends no OAuth token. `delegated` requires a supported Device Code or
Authorization Code flow. `app` requires a Client Credentials flow.

Changing authentication mode drops any task awaiting continuation so a task is
not resumed using a different authentication selection.

## Configuration

The client loads configuration in this order:

1. `appsettings.json`
1. Environment variables prefixed with `A2ACLIENT_`
1. .NET user secrets

Values loaded later override values loaded earlier. Command-line `--agent`
overrides the configured agent URL.

The configuration has two top-level areas:

```json
{
  "A2A": {
    "AgentUrl": "https://agent.example.com/a2a"
  },
  "Authentication": {
    "Connections": {
      "delegated": {
        "ClientId": "<public-client-id>",
        "AllowedOrigins": [
          "https://identity.example.com"
        ],
        "AdditionalScopes": [
          "offline_access"
        ]
      },
      "browser-oauth": {
        "ClientId": "<public-or-confidential-client-id>",
        "RedirectUri": "http://localhost:8400/callback/",
        "AllowedOrigins": [
          "https://identity.example.com"
        ],
        "TokenEndpointAuthenticationMethod": "None",
        "UsePkce": true
      },
      "service-oauth": {
        "ClientId": "<confidential-client-id>",
        "ClientSecret": "<store-outside-source-control>",
        "AllowedOrigins": [
          "https://identity.example.com"
        ],
        "TokenEndpointAuthenticationMethod": "ClientSecretPost"
      }
    }
  }
}
```

### `A2A`

| Property | Purpose |
|---|---|
| `AgentUrl` | Base URL used to resolve the public Agent Card. Must be an absolute URI. The `--agent` option overrides it. |

### `Authentication:Connections`

`Connections` is a dictionary of local OAuth client profiles. The client does
not choose OAuth endpoints or resource scopes from these profiles. Those values
come from the Agent Card or an in-task authorization request.

For Agent Card authentication, the connection name must exactly match the
selected OAuth security-scheme name. Names are case-sensitive. For example, an
Agent Card scheme named `browser-oauth` selects
`Authentication:Connections:browser-oauth`.

In-task authorization metadata does not define an Agent Card security-scheme
name. The client uses the connection named `delegated` for all in-task Device
Code and Authorization Code requests.

Connection names and client registrations are local policy. An agent can
advertise any standards-compliant provider and flow, but token acquisition
fails unless the client has a matching connection and trusts the advertised
endpoint origins.

### Connection properties

| Property | Purpose |
|---|---|
| `ClientId` | OAuth client registration identifier. Required for every supported flow. |
| `ClientSecret` | Confidential client credential. Required when `TokenEndpointAuthenticationMethod` is `ClientSecretBasic` or `ClientSecretPost`. Keep it outside source control. |
| `RedirectUri` | Loopback HTTP callback for Authorization Code. It must be an absolute URI whose path ends in `/`, and it must be registered with the provider. |
| `AllowedOrigins` | HTTPS origins that may receive this connection's client ID or client credentials. Every advertised authorization, device authorization, and token endpoint is checked against this list. |
| `AdditionalScopes` | Scopes added locally to the acquisition scopes advertised by the agent, such as `offline_access`. Duplicate scopes are removed. |
| `TokenEndpointAuthenticationMethod` | Client authentication used at the token endpoint: `None`, `ClientSecretBasic`, or `ClientSecretPost`. Defaults to `None`. |
| `UsePkce` | Enables Authorization Code PKCE. Defaults to `true`. |

The client has no global tenant setting. Tenant selection is part of the OAuth
endpoint URLs advertised by the agent.

### Flow-specific profiles

**Device Code**

- Requires `ClientId`.
- Requires each advertised device authorization and token endpoint origin in
  `AllowedOrigins`.
- Usually uses `TokenEndpointAuthenticationMethod: None`.
- Can add protocol scopes such as `offline_access` through `AdditionalScopes`.

**Authorization Code**

- Requires `ClientId`, `RedirectUri`, and trusted authorization and token
  endpoint origins.
- Uses a loopback HTTP callback.
- Uses PKCE unless `UsePkce` is set to `false`.
- Requires `ClientSecret` when the selected token endpoint authentication
  method uses a client secret.

**Client Credentials**

- Requires `ClientId` and a trusted token endpoint origin.
- Typically requires `ClientSecret` with `ClientSecretBasic` or
  `ClientSecretPost`.

## Keep credentials outside `appsettings.json`

The committed `appsettings.json` contains placeholders only. Store local
credentials with .NET user secrets:

```powershell
dotnet user-secrets set "Authentication:Connections:delegated:ClientId" "<client-id>" --project src\samples\A2A\A2AClient\A2AClient.csproj
dotnet user-secrets set "Authentication:Connections:service-oauth:ClientId" "<client-id>" --project src\samples\A2A\A2AClient\A2AClient.csproj
dotnet user-secrets set "Authentication:Connections:service-oauth:ClientSecret" "<client-secret>" --project src\samples\A2A\A2AClient\A2AClient.csproj
```

Environment variables use the `A2ACLIENT_` prefix and `__` for configuration
section separators:

```powershell
$env:A2ACLIENT_A2A__AgentUrl = "https://agent.example.com/a2a"
$env:A2ACLIENT_Authentication__Connections__delegated__ClientId = "<client-id>"
$env:A2ACLIENT_Authentication__Connections__delegated__AllowedOrigins__0 = "https://identity.example.com"
```

Do not store client secrets in committed files.

## Authentication behavior

### Agent Card authorization

The client evaluates the selected skill's security requirements, falling back
to card-level requirements when appropriate. A requirement is usable when it:

- names one OAuth security scheme;
- specifies at least one acquisition scope;
- advertises a supported flow for the selected authentication mode; and
- has valid HTTPS endpoints whose origins are trusted by the matching local
  connection.

If automatic selection finds multiple delegated or application alternatives,
the client reports the ambiguity instead of guessing.

The acquired token is sent in the normal HTTP `Authorization` header.

### In-task authorization

When the Agent Card advertises the Agents SDK in-task authorization extension,
the client activates the extension with the `A2A-Extensions` request header.

If a task enters `TASK_STATE_AUTH_REQUIRED`, the client:

1. Reads the OAuth flow and required scopes from the task status metadata.
1. Uses the local `delegated` connection.
1. Acquires the requested token.
1. Calls `resumeAuth`.
1. Sends the raw token in `x-a2a-intask-authorization`.
1. Repeats the process when the task returns another distinct authorization
   request.

The normal `Authorization` header remains available for the JWT that
authenticates the A2A request. A repeated authorization request ID is rejected
to prevent a non-progress loop.

## Token caching

Tokens are cached by connection name, OAuth flow, and acquisition scopes.

- A token with `expires_in` is reused until one minute before expiration.
- A refresh token is used when the cached access token expires.
- Without a refresh token, the advertised OAuth flow runs again.
- A token without `expires_in` is treated as non-expiring.

Access tokens and refresh tokens are never printed.

## Endpoint trust

OAuth endpoints are supplied by an external agent, so `AllowedOrigins` is a
required security boundary. The client rejects:

- malformed endpoint URLs;
- non-HTTPS OAuth endpoints; and
- endpoints whose scheme and authority do not match an allowed origin.

This check occurs before the client sends credentials.

Agent API access tokens are also restricted to the configured agent origin.
The client refuses to send them to another origin or over plaintext HTTP,
except for HTTP loopback addresses used during local development.

## Common configuration errors

- No local connection has the selected Agent Card security-scheme name.
- In-task authorization is requested but the `delegated` connection is absent.
- `ClientId` is missing or still contains a shipped all-zero placeholder.
- A required endpoint origin is absent from `AllowedOrigins`.
- Authorization Code is selected without a valid loopback `RedirectUri`.
- A client-secret authentication method is selected without `ClientSecret`.
- The selected requirement advertises no acquisition scopes.
- The selected mode does not match any flow advertised by the agent.
- The provider rejects the advertised scopes, client registration, redirect
  URI, or tenant-specific endpoint.

OAuth HTTP failures include the provider's `error` and `error_description`
values when available, without printing tokens.
