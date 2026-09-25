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
   OAuth provider and registration policy.
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

When connected to the A2AAgent sample, leave authentication in `auto` mode and
send `-me-agentcard` to test OAuth advertised by the selected Agent Card skill,
or `-me-intask` to test the optional In-Task authorization extension. Both
commands return the same Microsoft Graph profile through different token
transport mechanisms.

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
    "Providers": {
      "entra": {
        "Type": "Entra",
        "AllowedAuthorities": [
          "https://login.microsoftonline.com"
        ],
        "AdditionalScopes": [
          "offline_access"
        ],
        "Registrations": {
          "delegated": {
            "GrantTypes": [
              "DeviceCode",
              "AuthorizationCode"
            ],
            "ClientId": "<public-client-id>",
            "RedirectUri": "http://localhost:8400/callback/"
          },
          "application": {
            "GrantTypes": [
              "ClientCredentials"
            ],
            "ClientId": "<confidential-client-id>",
            "ClientSecret": "<store-outside-source-control>",
            "TokenEndpointAuthenticationMethod": "ClientSecretPost"
          }
        }
      },
      "browser-oauth": {
        "Type": "GenericOAuth2Pkce",
        "AllowedOrigins": [
          "https://identity.example.com"
        ],
        "Registrations": {
          "browser": {
            "GrantTypes": [
              "AuthorizationCode"
            ],
            "ClientId": "<public-client-id>",
            "RedirectUri": "http://localhost:8401/callback/"
          }
        }
      },
      "interactive-dcr": {
        "Type": "OAuth21PkceDcr",
        "AllowInteractiveApproval": true,
        "RedirectUri": "http://localhost:8402/callback/"
      }
    }
  }
}
```

### `A2A`

| Property | Purpose |
|---|---|
| `AgentUrl` | Base URL used to resolve the public Agent Card. Must be an absolute URI. The `--agent` option overrides it. |

### `Authentication:Providers`

`Providers` is a dictionary of local OAuth provider policies. The Agent Card
still discovers the advertised flow, scopes, metadata URL, and OAuth
endpoints. Local configuration supplies trust rules, registered clients,
optional extra scopes, and the approval policy for interactive OAuth 2.1
dynamic client registration.

Provider IDs and registration IDs are local configuration identifiers only.
Agent Card scheme names are diagnostic and never select a local provider.
Both Agent Card authorization and in-task authorization go through the same
deterministic provider resolver, which ranks candidates by provider type,
trusted authority/origin specificity, and registration compatibility. If two
local matches tie at the same rank, the client reports an explicit ambiguity
error instead of guessing.

### Provider properties

| Property | Purpose |
|---|---|
| `Type` | Provider kind: `Entra`, `GenericOAuth2`, `GenericOAuth2Pkce`, or `OAuth21PkceDcr`. |
| `AllowedAuthorities` | HTTPS issuer or server authorities that may satisfy every advertised OAuth endpoint. Use this for path-specific trust such as `https://login.microsoftonline.com/{tenant}`. |
| `AllowedOrigins` | HTTPS origins trusted for every advertised OAuth endpoint when origin-level trust is sufficient. |
| `AdditionalScopes` | Scopes added locally to the acquisition scopes advertised by the agent, such as `offline_access`. Duplicate scopes are removed. |
| `Registrations` | Named local client registrations available to the provider. |
| `AllowInteractiveApproval` | Enables operator approval and dynamic client registration for `OAuth21PkceDcr`. Must be `true` or `false`. |
| `RedirectUri` | Loopback callback used by Authorization Code or DCR flows. It must be an absolute loopback HTTP URI whose path ends in `/`, such as `http://localhost:8400/callback/`. Required for every `OAuth21PkceDcr` provider. |
| `MetadataUrl` | Optional HTTPS metadata document URL used before deriving well-known metadata from advertised endpoints. |
| `ServerUrl` | Optional HTTPS issuer or server URL fallback when metadata cannot be discovered from advertised endpoints. |

The client has no global tenant setting. Tenant selection is part of the OAuth
endpoint URLs advertised by the agent.

### Registration properties

| Property | Purpose |
|---|---|
| `GrantTypes` | Supported OAuth flows for the registration: `DeviceCode`, `AuthorizationCode`, and/or `ClientCredentials`. At least one is required. |
| `ClientId` | OAuth client registration identifier. Required for every configured registration. |
| `ClientSecret` | Confidential client credential. Required when `TokenEndpointAuthenticationMethod` is `ClientSecretBasic` or `ClientSecretPost`, and rejected when it is `None`. Keep it outside source control. |
| `RedirectUri` | Optional per-registration loopback callback. It must be an absolute loopback HTTP URI whose path ends in `/`. |
| `TokenEndpointAuthenticationMethod` | Client authentication used at the token endpoint: `None`, `ClientSecretBasic`, or `ClientSecretPost`. Defaults to `None`. `ClientCredentials` registrations must authenticate the client, so they cannot use `None`. |
| `UsePkce` | Enables Authorization Code PKCE. Must be `true` or `false`. Defaults to `true` and is forced to `true` for PKCE/DCR providers. |

### Flow-specific provider guidance

**Device Code**

- Requires a registration with `GrantTypes` including `DeviceCode`.
- Requires each advertised device authorization and token endpoint origin in
  `AllowedAuthorities` or `AllowedOrigins`.
- Usually uses `TokenEndpointAuthenticationMethod: None`.
- Can add protocol scopes such as `offline_access` through `AdditionalScopes`.

**Authorization Code**

- Requires a registration with `GrantTypes` including `AuthorizationCode`,
  `RedirectUri`, and trusted authorization and token endpoint origins.
- Uses a loopback HTTP callback.
- Uses PKCE unless the provider type allows non-PKCE registrations.
- Requires `ClientSecret` when the selected token endpoint authentication
  method uses a client secret.

**Client Credentials**

- Requires a registration with `GrantTypes` including `ClientCredentials` and
  a trusted token endpoint origin.
- Typically requires `ClientSecret` with `ClientSecretBasic` or
  `ClientSecretPost`.

**OAuth 2.1 PKCE + DCR**

- Requires `Type: OAuth21PkceDcr` and a loopback `RedirectUri`.
- Reuses a stored public client registration when one already exists for the
  discovered issuer and redirect URI.
- Discovers metadata in this order: configured `MetadataUrl`, advertised
  metadata URL, origins derived from advertised authorization and token
  endpoints, configured `ServerUrl`, and finally an interactive server-URL
  prompt when approval is enabled.
- When no stored registration exists and `AllowInteractiveApproval` is `true`,
  the client shows the discovered issuer, metadata, registration endpoint, and
  redirect URI before registering a public PKCE client.
- Successfully discovered metadata is memoized for the life of the process and
  keyed by the normalized discovery inputs, so repeated token requests do not
  rediscover metadata or re-prompt for a server URL. Discovery failures and
  approvals are never cached, and the registration store is still consulted on
  every bind.

### Pinned and open DCR providers

An `OAuth21PkceDcr` provider is *pinned* when it declares `AllowedAuthorities`
or `AllowedOrigins`. A pinned provider:

- matches only when every advertised authorization, token, and metadata
  endpoint satisfies one common authority or origin rule;
- ranks by the specificity of the matched rule, so a path-specific rule wins
  over a host-wide rule, both win over an origin-only rule, and any pinned rule
  wins over an open provider; and
- re-applies the same rule to the discovered issuer, authorization endpoint,
  token endpoint, metadata URL, and registration endpoint before approval or
  binding.

An `OAuth21PkceDcr` provider with no trust lists is *open*. Its configuration
declares no authority the client can check against, so the operator approval
prompt is the trust decision. The prompt displays the agent origin, requested
scopes, every advertised endpoint, and every discovered endpoint - issuer,
metadata URL, authorization endpoint, token endpoint, registration endpoint,
and the redirect URI to be registered - so the approval covers exactly the
endpoints that will receive registration metadata and credentials. This is
intentional: an open provider that has no stored registration and no approval
refuses to register. Declare `AllowedAuthorities` or `AllowedOrigins` when you
want the trust decision made in configuration instead of at the prompt.

Configured (non-DCR) providers are never bypassed by DCR. When a configured
provider trusts the advertised endpoints and supports the advertised flow but
has no compatible registration, the client reports that missing registration
instead of falling back to dynamic client registration.

### Dynamic client registration storage

The sample program uses a process-local in-memory registration store, so DCR
registrations last only for the current process. Applications can replace
`IOAuthClientRegistrationStore` with their own durable implementation before
constructing `OAuth21DcrCredentialProvider`.

## Keep credentials outside `appsettings.json`

The committed `appsettings.json` contains placeholders only. Store local
credentials with .NET user secrets:

```powershell
dotnet user-secrets set "Authentication:Providers:entra:Registrations:delegated:ClientId" "<client-id>" --project src\samples\A2A\A2AClient\A2AClient.csproj
dotnet user-secrets set "Authentication:Providers:entra:Registrations:application:ClientId" "<client-id>" --project src\samples\A2A\A2AClient\A2AClient.csproj
dotnet user-secrets set "Authentication:Providers:entra:Registrations:application:ClientSecret" "<client-secret>" --project src\samples\A2A\A2AClient\A2AClient.csproj
dotnet user-secrets set "Authentication:Providers:browser-oauth:Registrations:browser:ClientId" "<client-id>" --project src\samples\A2A\A2AClient\A2AClient.csproj
```

These configuration paths use local provider and registration IDs. Do not
replace `entra`, `application`, or `browser-oauth` with an Agent Card scheme
name.

Environment variables use the `A2ACLIENT_` prefix and `__` for configuration
section separators:

```powershell
$env:A2ACLIENT_A2A__AgentUrl = "https://agent.example.com/a2a"
$env:A2ACLIENT_Authentication__Providers__entra__Registrations__delegated__ClientId = "<client-id>"
$env:A2ACLIENT_Authentication__Providers__entra__AllowedAuthorities__0 = "https://login.microsoftonline.com"
```

Do not store client secrets in committed files.

## Authentication behavior

### Agent Card authorization

The client evaluates the selected skill's security requirements, falling back
to card-level requirements when appropriate. A requirement is usable when it:

- names one OAuth security scheme;
- specifies at least one acquisition scope;
- advertises a supported flow for the selected authentication mode; and
- has valid HTTPS endpoints trusted by the matching local provider policy.

If automatic selection finds multiple delegated or application alternatives,
the client reports the equal-rank ambiguity instead of guessing.

The acquired token is sent in the normal HTTP `Authorization` header.

### In-task authorization

When the Agent Card advertises the Agents SDK in-task authorization extension,
the client activates the extension with the `A2A-Extensions` request header.

If a task enters `TASK_STATE_AUTH_REQUIRED`, the client:

1. Reads the OAuth flow and required scopes from the task status metadata.
1. Resolves the request through the shared local provider catalog using the
   advertised flow, metadata URL, and endpoints.
1. Acquires the requested token.
1. Calls `resumeAuth`.
1. Sends the raw token in `x-a2a-intask-authorization`.
1. Repeats the process when the task returns another distinct authorization
   request.

The normal `Authorization` header remains available for the JWT that
authenticates the A2A request. A repeated authorization request ID is rejected
to prevent a non-progress loop.

## Token caching

Tokens are cached by resolved provider identity, provider ID, registration ID,
client ID, OAuth flow, and normalized acquisition scopes.

- A token with `expires_in` is reused until one minute before expiration.
- A refresh token is used when the cached access token expires.
- If refresh fails, the cached entry is discarded and the advertised OAuth flow
  runs once more. A failure there is reported and leaves no cached entry, so no
  later request replays the rejected refresh token.
- Without a refresh token, the advertised OAuth flow runs again.
- A token without `expires_in` is treated as non-expiring.

Access tokens and refresh tokens are never printed.

## Endpoint trust

OAuth endpoints are supplied by an external agent, so `AllowedOrigins` is a
required security boundary. The client rejects:

- malformed endpoint URLs;
- non-HTTPS OAuth endpoints; and
- endpoints whose scheme and authority do not match an allowed origin.

Scheme and host comparisons are case-insensitive. Authority *paths* are
compared case-sensitively, so `https://identity.example.com/Tenant` does not
trust endpoints under `https://identity.example.com/tenant`.

This check occurs before the client sends credentials.

Agent API access tokens are also restricted to the configured agent origin.
The client refuses to send them to another origin or over plaintext HTTP,
except for HTTP loopback addresses used during local development.

## Common configuration errors

- No trusted local provider can satisfy the advertised OAuth endpoints and
  selected flow.
- A trusted provider matches the flow, but none of its registrations is
  compatible with the advertised request. Dynamic client registration does not
  rescue this case.
- `ClientId` is missing or still contains a shipped all-zero placeholder. Every
  flow rejects the placeholder before contacting the authorization server.
- `GrantTypes` is missing or empty on a configured registration.
- A required endpoint origin is absent from `AllowedOrigins`.
- `RedirectUri` is not an absolute loopback HTTP URI ending in `/`.
- An `OAuth21PkceDcr` provider does not declare `RedirectUri`.
- `AllowInteractiveApproval` or `UsePkce` is not `true` or `false`.
- `ClientSecret` is set while `TokenEndpointAuthenticationMethod` is `None`, or
  is missing while a client-secret method is selected.
- A `ClientCredentials` registration leaves `TokenEndpointAuthenticationMethod`
  at `None`.
- The selected requirement advertises no acquisition scopes.
- The selected mode does not match any flow advertised by the agent.
- The provider rejects the advertised scopes, client registration, redirect
  URI, or tenant-specific endpoint.

OAuth HTTP failures include the provider's `error` and `error_description`
values when available, without printing tokens.
