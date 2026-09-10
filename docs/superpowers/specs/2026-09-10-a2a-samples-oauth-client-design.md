# A2A Samples and OAuth Client Design

## Purpose

Organize the A2A samples under one folder and add a console client that can
manually exercise anonymous, delegated-user, delegated-user-with-OBO, and
application-token behavior against `Microsoft.Agents.Extensions.A2A`.

The sample must demonstrate the request-token OAuth behavior tracked by #981
without introducing provider-backed A2A authorization interruption or callback
flows.

## Sample Layout

Move the existing projects and add the client under:

```text
src/samples/A2A/
  A2AAgent/
  A2ATCKAgent/
  A2AClient/
  README.md
```

The projects retain their existing assembly and namespace names. Moving them
must update:

- project references relative to the new paths;
- `src/Microsoft.Agents.SDK.sln`;
- sample README links and commands;
- repository build or sample-discovery files that name the old paths.

The parent README describes the available A2A samples and links to each
project-specific README.

## A2AClient

### Base behavior

`A2AClient` adapts the interaction model from the upstream
[`a2a-dotnet` A2ACli sample](https://github.com/a2aproject/a2a-dotnet/tree/main/samples/A2ACli):

- resolve and display the Agent Card;
- select the advertised JSON-RPC or HTTP+JSON interface;
- send text messages and optional file attachments;
- support task continuation when the agent requests more input;
- optionally display task history;
- support streaming when advertised by the Agent Card;
- retain the existing push-notification options where the upstream client
  supports them.

The client uses the repository's centrally managed `A2A` package version
instead of copying protocol client code.

### Authentication modes

The client supports three runtime authentication modes:

| Mode | Acquisition | Request credential | Intended route |
|---|---|---|---|
| `none` | None | No `Authorization` header | Existing anonymous routes |
| `delegated` | MSAL device-code flow | Agent API delegated access token | `-delegated`, `-me` |
| `app` | MSAL client-credentials flow | Agent API application access token | `-app` |

Interactive commands switch modes without restarting:

```text
:auth none
:auth delegated
:auth app
```

The selected mode applies to subsequent A2A requests, including Agent Card
resolution and task operations. Changing mode clears the active request
credential but does not bypass MSAL's normal token cache.

The client never prints, serializes, or persists access-token values itself.
MSAL may use its standard in-memory cache for the process lifetime.

### Client configuration

Configuration uses placeholders and environment/user-secret overrides:

```json
{
  "A2A": {
    "AgentUrl": "http://localhost:3978/a2a"
  },
  "Authentication": {
    "TenantId": "{{TenantId}}",
    "PublicClientId": "{{PublicClientId}}",
    "ConfidentialClientId": "{{ConfidentialClientId}}",
    "ConfidentialClientSecret": "",
    "AgentDelegatedScope": "api://{{AgentClientId}}/access_as_user",
    "AgentApplicationScope": "api://{{AgentClientId}}/.default"
  }
}
```

The committed configuration contains no real credentials. The confidential
client secret is supplied through user secrets or an environment variable.

### Client boundaries

Token acquisition and HTTP credential injection are separate units:

- `IA2AAccessTokenProvider` obtains a token for the current mode.
- A delegating handler adds the bearer header only when the provider returns a
  token.
- The command loop controls the mode and uses the authenticated `HttpClient`
  with the `A2A` client APIs.

This separation allows tests to verify mode selection and header injection with
fake tokens and no Entra dependency.

## A2AAgent Authorization

### Global behavior

Global user auto-sign-in remains disabled. Existing echo, streaming,
multi-turn, and direct A2A routes remain anonymous.

Three `A2AUserAuthorization` handler definitions demonstrate distinct
request-token behavior:

| Handler | Inbound token | OBO | Route |
|---|---|---|---|
| `delegated` | Agent API delegated token | No | `-delegated` |
| `graph` | Agent API delegated token | Graph `User.Read` | `-me` |
| `app` | Agent API application token | No | `-app` |

Each protected route declares only its own handler through
`autoSignInHandlers`.

### Delegated passthrough route

`-delegated` resolves the validated request token with
`UserAuthorization.GetTurnTokenAsync(turnContext, "delegated")`. It does not
return or log the token. The response summarizes non-sensitive caller identity
claims, such as tenant ID, object ID, subject, and authentication type.

This route proves that a delegated token accepted by ASP.NET Core is available
through the standard `UserAuthorization` route lifecycle without OBO.

### Graph OBO route

`-me` resolves the `graph` handler token. Its handler exchanges the inbound
Agent API delegated token through the configured connection for Microsoft Graph
`User.Read`. The route calls `https://graph.microsoft.com/v1.0/me` and returns
the signed-in user's display name and user principal name.

The route uses an injected `HttpClient` and propagates cancellation. Graph
errors produce a concise failure response without including token values or
response secrets.

### Application-token route

`-app` resolves the `app` handler token without OBO. It reports non-sensitive
application identity claims such as tenant ID, application/client ID, and
subject. It does not attempt `/me`, create user state from the application
identity, or imply that an application token represents a user.

This route also provides a manual test for the no-user-identity behavior being
tracked separately in #664.

## Entra Registration Model

The sample documentation supports either two registrations with multiple roles
or three registrations for clearer separation:

1. **Agent API registration**
   - exposes a delegated scope such as `access_as_user`;
   - exposes an application role for the app-only client;
   - accepts the configured API audience;
   - has permission to request Graph `User.Read` for OBO.
2. **Public console client registration**
   - enables public client/device-code flows;
   - receives delegated permission to the Agent API scope.
3. **Confidential console client registration** (may be combined with the
   public client when appropriate)
   - has a client secret supplied outside committed configuration;
   - receives the Agent API application permission and admin consent.

The agent's OBO connection uses the Agent API registration identity and the
Graph delegated permission. The client never sends a Graph access token
directly to the agent because Graph-issued credentials are not tokens for the
Agent API and must not be treated as such.

## Request Flow

### Delegated passthrough

1. The operator selects `:auth delegated`.
2. MSAL device-code flow obtains an Agent API delegated token.
3. The client sends `-delegated` with the bearer token.
4. ASP.NET Core validates the token for the Agent API.
5. `A2ARequestAuthentication` captures the validated request token.
6. The route-level `delegated` handler resolves it.
7. The route returns a non-sensitive identity summary.

### Delegated OBO to Graph

1. The operator remains in `delegated` mode and sends `-me`.
2. ASP.NET Core validates the Agent API token.
3. The route-level `graph` handler exchanges the token for Graph `User.Read`.
4. The route calls Graph `/me`.
5. The agent returns selected profile fields.

### Application token

1. The operator selects `:auth app`.
2. MSAL client credentials obtains an Agent API application token.
3. The client sends `-app` with the bearer token.
4. ASP.NET Core validates the Agent API token.
5. The route-level `app` handler resolves it without OBO.
6. The route returns a non-sensitive application identity summary.

## Error Handling

- Missing client configuration fails startup with the specific missing key.
- Device-code instructions are written to the console without token contents.
- MSAL failures identify the selected mode and preserve the original error.
- An anonymous request to a protected route follows the existing
  `A2AUserAuthorization` failure behavior; it does not begin a provider-backed
  sign-in flow.
- A delegated token sent to `-app`, or an application token sent to `-me`,
  fails through normal validation/OBO handling rather than silently changing
  modes.
- Graph failures include status and a safe message, not the access token.
- Cancellation propagates through token acquisition, A2A operations, and Graph.

## Testing

### Client tests

- mode parsing and switching;
- no header in `none` mode;
- delegated token header injection;
- application token header injection;
- token provider failures propagate;
- no token appears in formatted console output;
- task continuation and response formatting where extracted from the upstream
  loop.

Token acquisition tests use fakes around `IA2AAccessTokenProvider`; they do not
contact Entra.

### Agent tests

- protected route metadata selects the expected handler;
- anonymous routes remain unprotected;
- delegated route resolves the request token without exposing it;
- Graph route requests the `graph` handler and calls the injected Graph client;
- application route reports application claims without accessing user state.

### Build and manual validation

- build all three projects after the move;
- run existing A2A extension and authorization tests;
- manually run the agent and client through `none`, `delegated`, and `app`
  modes using the documented setup.

## Out of Scope

- Provider-backed `TASK_STATE_AUTH_REQUIRED` responses.
- OAuth callbacks or continuation replay.
- Directly accepting Microsoft Graph access tokens at the A2A endpoint.
- Persisting console tokens across process restarts.
- Solving the general A2A `UserState` identity design tracked by #664.
