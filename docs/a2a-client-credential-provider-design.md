# A2A Client Credential Provider Design

## Summary

The A2A client sample currently selects local OAuth configuration by requiring an
Agent Card security-scheme name to exactly match an
`Authentication:Connections` key. This couples an agent-local identifier to the
client's OAuth registration and requires advance knowledge of every agent's
chosen scheme names.

Replace that lookup with a credential-provider catalog and deterministic
resolver. Agent Cards and in-task authorization continue to advertise the OAuth
flow, endpoints, and scopes required by the agent. The client independently
selects a trusted provider and compatible client registration. Agent Card
security-scheme names remain diagnostic protocol identifiers and have no local
configuration meaning.

The first implementation supports:

- Microsoft Entra
- Generic OAuth 2.0
- Generic OAuth 2.0 with PKCE
- OAuth 2.1 with PKCE and Dynamic Client Registration (DCR)

This is an intentionally breaking redesign. The existing
`Authentication:Connections` configuration is removed without a compatibility
alias.

## Goals

- Allow a configured provider and client registration to work with previously
  unknown agents that advertise compatible OAuth metadata.
- Separate agent-advertised authentication requirements from local credential
  policy and client registrations.
- Use the same provider-resolution path for Agent Card and in-task
  authorization.
- Select providers deterministically and fail on ambiguous matches.
- Support interactive approval and DCR for otherwise unknown OAuth 2.1
  providers.
- Keep DCR registration persistence pluggable, with an in-memory default in the
  sample.
- Preserve endpoint-origin validation before transmitting client IDs, client
  secrets, authorization codes, refresh tokens, or access tokens.

## Non-goals

- Backward compatibility with `Authentication:Connections`.
- Automatic trust of arbitrary OAuth servers without user approval.
- A durable or operating-system-backed registration store in the sample.
- Confidential-client DCR or dynamically registered client secrets.
- A general production credential manager for the Agents SDK libraries. This
  design applies to the A2A client sample.
- Changes to the A2A Agent Card protocol models.

## Current Design and Problem

`A2AAgentCardAuthentication` selects an OAuth security requirement and
normalizes its flow, endpoints, and scopes. It also carries the selected Agent
Card security-scheme name.

`A2AAccessTokenProvider` currently calls:

```csharp
_options.GetRequiredConnection(authentication.SecuritySchemeName)
```

The selected scheme name therefore must match a local
`Authentication:Connections` key. Scheme names are scoped to an individual
Agent Card and do not identify an OAuth provider or client registration. Two
agents can use different names for the same provider, and unrelated agents can
use the same name for different providers.

In-task authorization has the same coupling in a different form: it always uses
the hard-coded local connection name `delegated`.

## Conceptual Model

The new model separates four concerns:

1. **Advertised authentication** describes what the agent requires: flow,
   endpoints, scopes, and the Agent Card scheme name when one exists.
2. **Credential provider** describes local protocol behavior and trust policy
   for an authorization server.
3. **Client registration** supplies a compatible client ID, redirect URI,
   optional client secret, and token-endpoint authentication method.
4. **Runtime connection** is the cached token state for one resolved provider,
   registration, flow, and scope set.

This follows the provider/connection separation used by Azure API Management
without making the sample dependent on API Management.

## Configuration

Configuration moves from `Authentication:Connections` to
`Authentication:Providers`.

```json
{
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
            "ClientId": "00000000-0000-0000-0000-000000000000",
            "RedirectUri": "http://localhost:8400/callback/"
          },
          "application": {
            "GrantTypes": [
              "ClientCredentials"
            ],
            "ClientId": "00000000-0000-0000-0000-000000000000",
            "ClientSecret": "",
            "TokenEndpointAuthenticationMethod": "ClientSecretPost"
          }
        }
      },
      "generic-browser": {
        "Type": "GenericOAuth2Pkce",
        "AllowedOrigins": [
          "https://identity.example.com"
        ],
        "Registrations": {
          "browser": {
            "GrantTypes": [
              "AuthorizationCode"
            ],
            "ClientId": "example-client",
            "RedirectUri": "http://localhost:8400/callback/"
          }
        }
      },
      "unknown-oauth21": {
        "Type": "OAuth21PkceDcr",
        "AllowInteractiveApproval": true,
        "RedirectUri": "http://localhost:8400/callback/"
      }
    }
  }
}
```

Provider and registration names are local identifiers. They are never matched
to Agent Card scheme names.

### Provider settings

Common provider settings:

- `Type`
- `AllowedAuthorities`
- `AllowedOrigins`
- `AdditionalScopes`
- `Registrations`

The Entra provider defaults its allowed authority host to
`login.microsoftonline.com` when no explicit authority is supplied. Explicit
authority rules can restrict tenants or authority paths.

Generic providers require explicit allowed origins or authorities. Generic
OAuth 2.0 can support Device Code, Authorization Code, and Client Credentials
when compatible registrations exist. `GenericOAuth2Pkce` requires PKCE for
Authorization Code.

`OAuth21PkceDcr` supports Authorization Code with PKCE and public dynamic
client registration. It requires a loopback `RedirectUri`. It can optionally
define a known issuer, server URL, or metadata URL, and optional
`AllowedAuthorities`/`AllowedOrigins`. When those trust lists are non-empty the
provider is *pinned*: it matches only when every advertised
authorization/token/metadata endpoint satisfies one common rule, the matched
rule's specificity participates in ranking, and the same rule is re-applied to
the discovered issuer, authorization endpoint, token endpoint, metadata URL,
and registration endpoint before approval or binding. When both trust lists are
empty the provider is *open*: its configuration declares no checkable
authority, so explicit approval is the trust decision and the approval prompt
must display every advertised and discovered endpoint.
`AllowInteractiveApproval` permits discovery and registration with an unknown
provider after explicit user approval.

Configuration is validated when it is read. Redirect URIs must be absolute
loopback HTTP URIs whose path ends with `/`. Registrations must declare at
least one grant type. `AllowInteractiveApproval` and `UsePkce` must parse as
booleans. Client authentication must be self-consistent: a client secret with
`TokenEndpointAuthenticationMethod` `None` is rejected, a `ClientCredentials`
registration cannot use `None`, and a secret-based method requires a non-blank
secret.

### Registration settings

A configured client registration contains:

- Supported grant types
- Client ID
- Optional client secret
- Optional redirect URI
- Token endpoint authentication method
- PKCE policy when applicable

Client secrets remain outside source control and are supplied through user
secrets or environment variables.

## Components

### `A2AAgentCardAuthentication`

Continue to normalize the selected Agent Card or in-task OAuth requirement:

- Authentication mode
- Flow type
- Agent Card security-scheme name, when available
- OAuth2 metadata URL, when advertised
- Authorization endpoint
- Device authorization endpoint
- Token endpoint
- Required scopes

Remove any concept of a local connection name. For in-task authorization, the
scheme name is absent rather than replaced with `delegated`.

### `OAuthCredentialProviderOptions`

Represents one configured provider. It contains provider type, trust rules,
additional scopes, registrations, DCR policy, and optional discovery
configuration.

### `OAuthClientRegistration`

Represents one configured local client registration and the grants it can
satisfy.

### `OAuthCredentialProviderResolver`

Accepts:

- Agent origin
- `A2AAgentCardAuthentication`
- Configured provider catalog

Returns an `OAuthCredentialBinding` containing:

- Provider ID and type
- Registration ID
- Resolved client registration
- Validated authorization, device authorization, token, metadata, and
  registration endpoints as applicable
- Effective scopes

The binding is the only input accepted by token clients. Token clients do not
perform provider selection.

### Provider implementations

Each provider implementation exposes its supported grants, validates candidate
endpoints, calculates match specificity, and resolves configured or dynamic
registrations.

Initial implementations:

- `EntraOAuthCredentialProvider`
- `GenericOAuth2CredentialProvider`
- `GenericOAuth2PkceCredentialProvider`
- `OAuth21DcrCredentialProvider`

### `IOAuthProviderApproval`

Requests explicit approval before trusting and dynamically registering with an
unknown provider. The console implementation displays:

- Agent origin
- Agent Card scheme name, if present
- Requested scopes
- Advertised OAuth endpoints
- Discovered issuer
- Discovered metadata URL
- Registration endpoint
- Redirect URI to be registered

Declining approval aborts token acquisition. Approval is not silently cached as
provider policy; a registration stored by `IOAuthClientRegistrationStore`
avoids repeating approval for that registration.

### `IDynamicClientRegistrationClient`

Discovers registration metadata and submits a public-client registration.
Registration requests use:

- Authorization Code grant
- PKCE
- Token endpoint authentication method `none`
- The configured loopback redirect URI

The response must contain a non-empty client ID. Confidential registrations and
returned client secrets are rejected by this initial implementation.

### `IOAuthClientRegistrationStore`

Provides asynchronous lookup and save operations keyed by normalized provider
identity and redirect URI. The sample uses an in-memory implementation.
Applications can substitute a durable secure store.

### `A2AAccessTokenProvider`

Resolves an `OAuthCredentialBinding` before token-cache lookup and acquisition.
The cache key contains:

- Provider identity
- Registration identity or dynamically assigned client ID
- Flow type
- Effective scopes

The Agent Card security-scheme name is excluded from cache identity.

## Provider Resolution

### Candidate validation

All advertised endpoints must be absolute HTTPS URIs before provider matching.
Loopback redirect URIs remain permitted for Authorization Code callbacks.

A provider is a candidate only when:

- It supports the advertised flow.
- A compatible configured or stored registration exists, or it supports
  approved DCR.
- Every credential-bearing endpoint satisfies its authority/origin policy.

### Ranking

Candidates are ranked without using configuration order:

1. Provider-specific match, such as Entra, over a generic provider.
2. Exact issuer or authority match over origin-only match.
3. A path-specific authority rule over a host-wide rule.
4. A configured registration over DCR.

Ranking is lexicographic and provider specificity is evaluated first. Only the
matches at the highest provider specificity are considered, so a lower-
specificity provider - including a DCR provider - cannot win because a more
specific trusted provider lacks a compatible registration. In that case the
more specific provider reports its own missing-compatible-registration error.

If multiple candidates remain at the same rank, resolution fails and names the
ambiguous provider and registration IDs.

### No configured match

When no provider of higher specificity matches, an `OAuth21PkceDcr` provider
with interactive approval can attempt discovery:

1. Use a configured metadata URL when present.
2. Otherwise use the selected OAuth2 scheme's metadata URL when advertised.
3. Otherwise try both RFC 8414 and OpenID Connect metadata URLs on each
   advertised authorization/token endpoint origin.
4. If discovery fails, prompt for an issuer or server URL and retry standards-
   based metadata discovery.
5. Require a valid HTTPS `registration_endpoint`.
6. Enforce the provider's trust lists, when non-empty, on every discovered
   endpoint.
7. Display the discovered values and request approval.
8. Register a public PKCE client.
9. Save the returned client ID through `IOAuthClientRegistrationStore`.

No registration request is sent before approval.

Successfully discovered metadata is memoized for the lifetime of the provider,
keyed by the normalized discovery inputs and provider configuration, so
repeated token requests neither rediscover metadata nor repeat the server-URL
prompt. Failures and approvals are never memoized, and the registration store
is consulted on every bind.

## Token Acquisition Flow

1. The request planner selects a skill and its effective security requirement.
2. `A2AAgentCardAuthentication` normalizes the advertised requirement.
3. `A2AAccessTokenProvider` asks the resolver for a credential binding.
4. The resolver selects a configured registration, reuses a stored dynamic
   registration, or performs approved DCR.
5. The token cache is checked using binding identity, flow, and scopes.
6. The flow-specific token client receives only validated endpoints and the
   resolved registration.
7. The access token is attached only to the configured A2A agent origin.

In-task authorization begins at step 2 with metadata from the auth-required
status. It uses the same provider resolver and no hard-coded connection name.

## Endpoint and Credential Security

- Agent Card and in-task endpoints are untrusted input.
- Provider matching and endpoint validation occur before a client ID or secret
  is transmitted.
- All authorization-server, token, metadata, and registration endpoints must
  use HTTPS.
- Authority scheme and host comparisons are case-insensitive; authority path
  comparisons are case-sensitive (`Ordinal`).
- Metadata and DCR clients disable automatic redirects and reject redirect
  responses. Token redirects must not escape the approved authority/origin
  policy.
- Client secrets are available only to configured confidential
  registrations.
- A shipped all-zero placeholder client ID is rejected at a shared acquisition
  validation point, so no flow can transmit it.
- DCR creates public clients and does not persist secrets.
- The approval prompt identifies the agent and every endpoint that will receive
  registration metadata.
- Access tokens continue to be attached only to the configured agent origin.
- Cancellation tokens propagate through discovery, approval, registration,
  token acquisition, refresh, and registration-store operations.

## Error Handling

Errors are explicit and do not fall back to success-shaped behavior.

- **No match:** include flow type, advertised endpoints, and evaluated provider
  IDs.
- **Ambiguous match:** list equally ranked provider and registration IDs.
- **Untrusted endpoint:** identify the endpoint and provider policy that
  rejected it.
- **Approval declined:** report that dynamic registration was not authorized.
- **Metadata failure:** identify each attempted metadata URL.
- **Missing registration endpoint:** identify the discovered issuer.
- **DCR rejection:** include the HTTP status and safe OAuth error fields, but
  never secrets.
- **Invalid DCR response:** identify the missing or unsupported registration
  property.
- **Missing compatible registration:** identify the provider and required
  grant.

## Program Composition

`Program` explicitly constructs:

- Provider catalog
- In-memory registration store
- Console provider-approval implementation
- Metadata and DCR clients
- Provider resolver
- Access token provider

No dependency-injection container is required for the sample. Constructor
boundaries remain interface-based so tests and applications can replace
approval and storage behavior.

## Documentation Changes

Update:

- `src/samples/A2A/A2AClient/README.md`
- `src/samples/A2A/A2AClient/appsettings.json`
- User-secret examples in the A2A agent and client documentation

Documentation must explain:

- Agent Card scheme names no longer select local configuration.
- Providers match flows and trusted authorities/endpoints.
- Configured registrations are still required unless approved DCR succeeds.
- DCR approval and registration persistence behavior.
- The default registration store is process-local.

## Testing Strategy

### Configuration tests

- Bind all four provider types.
- Bind multiple registrations with distinct grants.
- Reject invalid provider types, URIs, grants, and duplicate IDs.
- Confirm `Authentication:Connections` is not read.

### Resolver tests

- Match Entra across different Agent Card scheme names.
- Match generic providers by trusted endpoint origin.
- Select public and confidential registrations by grant.
- Prefer Entra over generic for equivalent endpoint rules.
- Prefer exact/path-specific rules over broad origins.
- Reject untrusted endpoints.
- Reject equal-ranked ambiguity.
- Report no compatible registration.

### DCR tests

- Reuse a stored dynamic registration.
- Discover metadata from an OIDC URL.
- Fall back to endpoint-origin metadata.
- Prompt for issuer/server URL after discovery failure.
- Perform no registration before approval.
- Abort when approval is declined.
- Submit a public PKCE registration.
- Reject missing client IDs and confidential DCR responses.
- Save successful registrations through the store.

### Token tests

- Cache tokens by provider and registration identity rather than scheme name.
- Reuse a token across different scheme names that resolve to the same binding.
- Keep different provider/registration bindings isolated.
- Refresh with validated endpoints and the resolved registration.
- Share concurrent acquisition for the same binding.

### Integration tests

- Agent Card delegated authentication through Entra.
- Agent Card application authentication through a configured confidential
  registration.
- Generic PKCE authentication.
- Approved OAuth 2.1 DCR authentication.
- In-task authorization through the common resolver.
- Existing agent-origin token isolation.

## Implementation Scope

The expected implementation primarily changes:

- `A2AClientAuthenticationOptions.cs`
- `A2AClientOptions.cs`
- `A2AAgentCardAuthentication.cs`
- `A2AAccessTokenProvider.cs`
- `A2AInTaskAuthorizationClient.cs`
- `OAuthConnectionOptions.cs`, replaced by provider and registration models
- Flow-specific OAuth token clients
- `OAuthEndpointValidator.cs`
- `Program.cs`
- A2A client sample configuration, README, and tests

New files will contain the provider catalog, resolver, provider
implementations, approval interface, metadata/DCR clients, credential binding,
and registration store.
