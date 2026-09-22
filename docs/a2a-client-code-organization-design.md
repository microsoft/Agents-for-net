# A2A Client Code Organization Design

## Summary

The `A2AClient` sample currently contains 48 C# files in one directory and one
namespace. A2A protocol orchestration, OAuth provider resolution, token flows,
metadata discovery, dynamic client registration, configuration, and console
interaction are therefore presented as one undifferentiated component.

Reorganize the sample into explicit A2A and OAuth boundaries. Keep the
application composition root at the project root, place application
configuration in its own namespace, and divide OAuth into focused subareas for
providers, discovery, registration, and tokens. Production and test namespaces
will mirror their folder structures.

This is a structural refactor. Provider selection, trust evaluation, dynamic
registration, token caching, HTTP behavior, and user-visible behavior remain
unchanged.

## Goals

- Make the A2A protocol flow distinguishable from OAuth implementation details.
- Make each OAuth responsibility discoverable without scanning one large
  directory.
- Establish namespace boundaries that communicate component ownership.
- Keep interfaces next to the feature they abstract.
- Keep shared OAuth primitives visible without creating generic `Common`,
  `Models`, or `Abstractions` folders.
- Mirror the production structure in the A2A client tests.
- Reduce `A2AClientOptions` to application-level configuration by extracting
  OAuth provider catalog parsing.

## Non-goals

- Renaming existing runtime types.
- Changing public or internal behavior.
- Changing provider ranking, matching, trust, or registration rules.
- Changing token acquisition, refresh, cache identity, or expiration behavior.
- Splitting large classes whose responsibilities are already cohesive.
- Reorganizing the separate `A2AAgent` sample or its tests.
- Introducing new dependency-injection, factory, or assembly boundaries.

## Project Structure

The project root remains the application composition boundary:

```text
A2AClient/
  Program.cs
  A2AClient.csproj
  README.md
  appsettings.json

  Configuration/
    A2AClientOptions.cs
    A2AClientAuthenticationOptions.cs

  A2A/
    A2AAccessTokenProvider.cs
    IA2AAccessTokenProvider.cs
    A2AAgentCardAuthentication.cs
    A2AAgentCardSkillSelector.cs
    A2AAgentOrigin.cs
    A2AAuthenticationSession.cs
    A2AAuthMode.cs
    A2AConsole.cs
    A2AInTaskAuthorizationClient.cs
    A2ARequestPlanner.cs
    A2AResponseWriter.cs
    AuthenticatedA2AHttpHandler.cs

  OAuth/
    A2AOAuthFlowType.cs
    OAuthCredentialBinding.cs
    OAuthEndpointValidator.cs
    OAuthScopeResolver.cs

    Configuration/
      OAuthCredentialProviderConfigurationReader.cs
      OAuthCredentialProviderOptions.cs
      OAuthCredentialProviderType.cs

    Providers/
      IOAuthCredentialProvider.cs
      OAuthCredentialProviderResolver.cs
      OAuthProviderMatch.cs
      GenericOAuth2CredentialProvider.cs
      GenericOAuth2PkceCredentialProvider.cs
      EntraOAuthCredentialProvider.cs
      OAuth21DcrCredentialProvider.cs

    Discovery/
      IOAuthAuthorizationServerMetadataClient.cs
      OAuthAuthorizationServerMetadata.cs
      OAuthAuthorizationServerMetadataClient.cs

    Registration/
      IDynamicClientRegistrationClient.cs
      DynamicClientRegistrationClient.cs
      OAuthClientRegistration.cs
      OAuthClientRegistrationValidator.cs
      IOAuthClientRegistrationStore.cs
      InMemoryOAuthClientRegistrationStore.cs
      IOAuthProviderApproval.cs
      ConsoleOAuthProviderApproval.cs

    Tokens/
      IOAuthTokenClient.cs
      OAuthTokenClient.cs
      OAuthTokenModels.cs
      OAuthTokenEndpointClient.cs
      OAuthDeviceCodeTokenClient.cs
      OAuthClientCredentialsTokenClient.cs
      OAuthAuthorizationCodeTokenClient.cs
      IOAuthAuthorizationCodeReceiver.cs
      LoopbackOAuthAuthorizationCodeReceiver.cs
```

## Namespace Rules

Namespaces mirror the folder structure under the existing root namespace:

| Folder | Namespace |
| --- | --- |
| Project root | `Microsoft.Agents.Samples.A2AClient` |
| `Configuration` | `Microsoft.Agents.Samples.A2AClient.Configuration` |
| `A2A` | `Microsoft.Agents.Samples.A2AClient.A2A` |
| `OAuth` | `Microsoft.Agents.Samples.A2AClient.OAuth` |
| `OAuth/Configuration` | `Microsoft.Agents.Samples.A2AClient.OAuth.Configuration` |
| `OAuth/Providers` | `Microsoft.Agents.Samples.A2AClient.OAuth.Providers` |
| `OAuth/Discovery` | `Microsoft.Agents.Samples.A2AClient.OAuth.Discovery` |
| `OAuth/Registration` | `Microsoft.Agents.Samples.A2AClient.OAuth.Registration` |
| `OAuth/Tokens` | `Microsoft.Agents.Samples.A2AClient.OAuth.Tokens` |

Shared OAuth primitives remain directly under `.OAuth`. This avoids a
catch-all folder while allowing provider, registration, and token components
to share the same domain types.

## Component Boundaries

### Composition and Configuration

`Program` remains the only composition root. It constructs the HTTP clients,
OAuth providers, resolver, token clients, registration services, A2A transport,
and console application.

`A2AClientOptions` remains the application configuration entry point. It owns
agent URL, console behavior, push notification settings, and the aggregate
authentication options. OAuth provider catalog parsing moves to
`OAuthCredentialProviderConfigurationReader`, which returns the existing
provider option types and preserves current validation and error messages.

No provider factory abstraction is added. The provider switch in `Program`
continues to make the configured runtime composition explicit.

### A2A

The `.A2A` namespace owns protocol-facing behavior:

- Agent Card authentication interpretation and skill selection.
- A2A request planning and response rendering.
- In-task authorization and resume behavior.
- Authentication session state and authenticated A2A HTTP transport.
- The A2A-facing access-token provider and its cache lifecycle.
- Interactive console orchestration.

`A2AAccessTokenProvider` remains in `.A2A` because its contract is expressed in
terms of an A2A authentication requirement. It delegates provider selection and
token acquisition through OAuth interfaces.

### OAuth Providers

The `.OAuth.Providers` namespace owns deterministic matching and binding of an
advertised A2A OAuth requirement to local provider policy and client
registration. It contains provider implementations, their resolver, and match
metadata.

Providers continue to consume `A2AAgentCardAuthentication`. This is the single
intentional dependency from OAuth provider resolution to the A2A requirement
model. Replacing that type with a protocol-neutral model is outside this
structural refactor.

### OAuth Discovery

The `.OAuth.Discovery` namespace owns authorization-server metadata discovery,
validation, and the normalized metadata result. It has no dependency on A2A
runtime orchestration.

### OAuth Registration

The `.OAuth.Registration` namespace owns configured and dynamically created
client registrations, registration validation, the RFC 7591 client,
registration persistence, and interactive approval. Approval remains here
because it is specifically part of the dynamic-registration trust flow.

### OAuth Tokens

The `.OAuth.Tokens` namespace owns grant execution, token endpoint requests,
token response models, refresh, and the loopback authorization-code receiver.
It accepts resolved `OAuthCredentialBinding` values and does not select
providers.

## Dependency Direction

The intended runtime dependency flow is:

```text
Program
  -> Configuration
  -> A2A
       -> OAuth.Providers
            -> OAuth.Discovery
            -> OAuth.Registration
       -> OAuth.Tokens
  -> authenticated A2A transport
```

Additional rules:

- Feature-specific interfaces remain in the same namespace as their
  implementations.
- OAuth discovery, registration, and token components do not depend on
  `Program`, console orchestration, or authenticated A2A transport.
- OAuth token components do not perform provider selection.
- A2A request planning and in-task authorization do not directly perform OAuth
  discovery or registration.
- Configuration types may reference OAuth option and registration types but do
  not construct runtime providers.

## Error Handling and Behavioral Preservation

The refactor preserves:

- Configuration keys, validation order, and exception messages.
- Provider specificity, authority specificity, and registration specificity.
- Ambiguous-match failures and missing-registration failures.
- Endpoint and redirect validation.
- Interactive approval ordering and DCR trust enforcement.
- Metadata discovery fallback and timeout behavior.
- Token endpoint authentication and PKCE behavior.
- Token cache identity, synchronization, refresh recovery, and expiration
  skew.
- Redirect-disabled HTTP clients for credential-bearing traffic.

Namespace changes must not introduce broad catches, fallback behavior, or
error translation. The extracted configuration reader must retain the existing
startup failure behavior.

## Test Organization

Only A2A client tests move. Tests for the separate `A2AAgent` sample remain in
their current locations and namespaces.

```text
Microsoft.Agents.Samples.A2A.Tests/
  A2AClient/
    A2A/
    Configuration/
    OAuth/
      Providers/
      Discovery/
      Registration/
      Tokens/
    A2AClientProgramTests.cs
    A2AClientInterfaceSelectionTests.cs
```

Test namespaces mirror production namespaces with `Tests` inserted:

```text
Microsoft.Agents.Samples.A2AClient.A2A
Microsoft.Agents.Samples.A2AClient.Tests.A2A

Microsoft.Agents.Samples.A2AClient.OAuth.Discovery
Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Discovery
```

Program and interface-selection tests remain at the A2A client test root
because they validate composition and top-level client selection. Existing
tests continue to verify behavior through the moved types. A focused
configuration-reader test is added only if the extraction exposes behavior not
already covered through `A2AClientOptionsTests`.

## Migration Strategy

1. Extract OAuth provider catalog parsing from `A2AClientOptions` into
   `OAuthCredentialProviderConfigurationReader` without changing configuration
   behavior.
2. Move production files into the approved folders and update file-scoped
   namespaces and imports.
3. Update `Program` and cross-component references to the new namespaces.
4. Move A2A client tests to the mirrored folder structure and update their
   namespaces and imports.
5. Keep A2A Agent tests unchanged.
6. Verify that no A2A client production type remains in the original flat
   namespace unless it is intentionally the composition root.

The moves should remain reviewable as structural changes. Type renames and
unrelated cleanup are excluded.

## Validation

Run:

```powershell
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
```

If either command reveals cross-project fallout, run the smallest additional
build or test command that covers the affected dependency. A repository-wide
test run is not required solely for namespace and folder changes.
