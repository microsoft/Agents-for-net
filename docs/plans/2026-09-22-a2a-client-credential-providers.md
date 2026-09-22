# A2A Client Credential Providers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Agent Card security-scheme-name-to-connection coupling with a provider catalog, deterministic provider resolver, shared Agent Card/in-task resolution, and approved OAuth 2.1 Dynamic Client Registration.

**Architecture:** Agent-advertised OAuth requirements remain in `A2AAgentCardAuthentication`. A provider catalog evaluates those requirements against local trust rules and compatible client registrations, returning an `OAuthCredentialBinding` containing validated endpoints and effective scopes. Token clients consume only the binding; unknown OAuth 2.1 providers can be discovered, approved interactively, dynamically registered, and retained through a pluggable registration store.

**Tech Stack:** C#/.NET 8 sample application, `System.Text.Json`, `HttpClient`, `Microsoft.Extensions.Configuration`, xUnit, Moq, A2A 1.0 protocol models.

**Spec:** `docs/a2a-client-credential-provider-design.md`

## Global Constraints

- Work in the current branch and primary checkout; do not create a git worktree.
- This is intentionally breaking: remove `Authentication:Connections` and do not add a compatibility alias.
- Provider types implemented now: Entra, Generic OAuth 2.0, Generic OAuth 2.0 with PKCE, and OAuth 2.1 with PKCE and DCR.
- Agent Card security-scheme names are diagnostic protocol identifiers only and must never select local configuration.
- Agent Card and in-task authorization must use the same provider resolver.
- Unknown DCR providers require explicit interactive approval before registration.
- DCR first tries configured or advertised OAuth2 metadata, then standards metadata on advertised endpoint origins, then prompts for an issuer/server URL.
- DCR creates public Authorization Code + PKCE registrations with token endpoint authentication method `none`.
- Registration persistence is interface-based; the sample default is in-memory.
- Validate HTTPS endpoints and provider trust before sending a client ID, client secret, authorization code, refresh token, or registration request.
- Preserve access-token attachment only to the configured A2A agent origin.
- Preserve cancellation-token propagation.
- Use `System.Text.Json`; do not introduce Newtonsoft.Json.
- Do not add package dependencies unless an existing framework API cannot implement RFC 8414/RFC 7591 requests.
- Every production behavior change follows red-green-refactor and ends with a focused commit.

---

## File Structure

### Existing files to modify

- `src/samples/A2A/A2AClient/A2AAgentCardAuthentication.cs` — advertised authentication only; remove local connection naming from in-task construction.
- `src/samples/A2A/A2AClient/A2AClientAuthenticationOptions.cs` — expose provider catalog instead of named connections.
- `src/samples/A2A/A2AClient/A2AClientOptions.cs` — bind and validate provider/registration configuration.
- `src/samples/A2A/A2AClient/A2AAccessTokenProvider.cs` — resolve bindings before token-cache lookup.
- `src/samples/A2A/A2AClient/A2AInTaskAuthorizationClient.cs` — remove the hard-coded `delegated` connection.
- `src/samples/A2A/A2AClient/IOAuthTokenClient.cs` — accept resolved credential bindings.
- `src/samples/A2A/A2AClient/OAuthTokenClient.cs` — route binding-based acquisition and refresh.
- `src/samples/A2A/A2AClient/OAuthDeviceCodeTokenClient.cs` — consume validated binding and registration.
- `src/samples/A2A/A2AClient/OAuthAuthorizationCodeTokenClient.cs` — consume validated binding and enforce provider PKCE policy.
- `src/samples/A2A/A2AClient/OAuthClientCredentialsTokenClient.cs` — consume validated binding and confidential registration.
- `src/samples/A2A/A2AClient/OAuthTokenEndpointClient.cs` — use resolved registration for client authentication.
- `src/samples/A2A/A2AClient/OAuthScopeResolver.cs` — resolve scopes from binding data.
- `src/samples/A2A/A2AClient/OAuthEndpointValidator.cs` — central URI normalization and authority/origin matching.
- `src/samples/A2A/A2AClient/Program.cs` — compose providers, resolver, discovery, approval, DCR, store, and token provider.
- `src/samples/A2A/A2AClient/appsettings.json` — replace connections with providers and registrations.
- `src/samples/A2A/A2AClient/README.md` — document provider resolution, DCR, approval, and configuration.
- `src/samples/A2A/A2AAgent/README.md` — update A2A client configuration examples.
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientOptionsTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAgentCardAuthenticationTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAccessTokenProviderTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AInTaskAuthorizationClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthDeviceCodeTokenClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthAuthorizationCodeTokenClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthClientCredentialsTokenClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientProgramTests.cs`

### Existing file to delete

- `src/samples/A2A/A2AClient/OAuthConnectionOptions.cs` — replaced by provider, registration, and binding models.

### New production files

- `src/samples/A2A/A2AClient/OAuthCredentialProviderType.cs`
- `src/samples/A2A/A2AClient/OAuthCredentialProviderOptions.cs`
- `src/samples/A2A/A2AClient/OAuthClientRegistration.cs` — registration model plus `OAuthTokenEndpointAuthenticationMethod`.
- `src/samples/A2A/A2AClient/OAuthCredentialBinding.cs`
- `src/samples/A2A/A2AClient/IOAuthCredentialProvider.cs`
- `src/samples/A2A/A2AClient/OAuthProviderMatch.cs`
- `src/samples/A2A/A2AClient/OAuthCredentialProviderResolver.cs`
- `src/samples/A2A/A2AClient/EntraOAuthCredentialProvider.cs`
- `src/samples/A2A/A2AClient/GenericOAuth2CredentialProvider.cs`
- `src/samples/A2A/A2AClient/GenericOAuth2PkceCredentialProvider.cs`
- `src/samples/A2A/A2AClient/OAuth21DcrCredentialProvider.cs`
- `src/samples/A2A/A2AClient/OAuthAuthorizationServerMetadata.cs`
- `src/samples/A2A/A2AClient/IOAuthAuthorizationServerMetadataClient.cs`
- `src/samples/A2A/A2AClient/OAuthAuthorizationServerMetadataClient.cs`
- `src/samples/A2A/A2AClient/IOAuthProviderApproval.cs`
- `src/samples/A2A/A2AClient/ConsoleOAuthProviderApproval.cs`
- `src/samples/A2A/A2AClient/IDynamicClientRegistrationClient.cs`
- `src/samples/A2A/A2AClient/DynamicClientRegistrationClient.cs`
- `src/samples/A2A/A2AClient/IOAuthClientRegistrationStore.cs`
- `src/samples/A2A/A2AClient/InMemoryOAuthClientRegistrationStore.cs`

### New test files

- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthCredentialProviderResolverTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthAuthorizationServerMetadataClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/DynamicClientRegistrationClientTests.cs`
- `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuth21DcrCredentialProviderTests.cs`

---

### Task 1: Provider and Registration Configuration Model

**Files:**
- Create: `src/samples/A2A/A2AClient/OAuthCredentialProviderType.cs`
- Create: `src/samples/A2A/A2AClient/OAuthCredentialProviderOptions.cs`
- Create: `src/samples/A2A/A2AClient/OAuthClientRegistration.cs`
- Modify: `src/samples/A2A/A2AClient/A2AClientAuthenticationOptions.cs`
- Modify: `src/samples/A2A/A2AClient/A2AClientOptions.cs`
- Delete: `src/samples/A2A/A2AClient/OAuthConnectionOptions.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientOptionsTests.cs`

**Interfaces:**
- Produces:

```csharp
internal enum OAuthCredentialProviderType
{
    Entra,
    GenericOAuth2,
    GenericOAuth2Pkce,
    OAuth21PkceDcr,
}

internal enum OAuthTokenEndpointAuthenticationMethod
{
    None,
    ClientSecretBasic,
    ClientSecretPost,
}

internal sealed class OAuthCredentialProviderOptions
{
    public required string Id { get; init; }
    public OAuthCredentialProviderType Type { get; init; }
    public IReadOnlyList<Uri> AllowedAuthorities { get; init; } = [];
    public IReadOnlyList<Uri> AllowedOrigins { get; init; } = [];
    public IReadOnlyList<string> AdditionalScopes { get; init; } = [];
    public IReadOnlyDictionary<string, OAuthClientRegistration> Registrations { get; init; }
        = new Dictionary<string, OAuthClientRegistration>(StringComparer.Ordinal);
    public bool AllowInteractiveApproval { get; init; }
    public Uri? RedirectUri { get; init; }
    public Uri? MetadataUrl { get; init; }
    public Uri? ServerUrl { get; init; }
}

internal sealed record OAuthClientRegistration(
    string Id,
    IReadOnlyList<A2AOAuthFlowType> GrantTypes,
    string ClientId,
    string? ClientSecret,
    Uri? RedirectUri,
    OAuthTokenEndpointAuthenticationMethod TokenEndpointAuthenticationMethod,
    bool UsePkce);
```

- `A2AClientAuthenticationOptions.Providers` is an ordinal dictionary keyed by provider ID.
- Remove `Connections` and `GetRequiredConnection`.

- [ ] **Step 1: Replace the connection-binding test with failing provider-binding tests**

Add tests that bind:

```csharp
["Authentication:Providers:entra:Type"] = "Entra",
["Authentication:Providers:entra:AllowedAuthorities:0"] = "https://login.microsoftonline.com",
["Authentication:Providers:entra:AdditionalScopes:0"] = "offline_access",
["Authentication:Providers:entra:Registrations:delegated:GrantTypes:0"] = "DeviceCode",
["Authentication:Providers:entra:Registrations:delegated:GrantTypes:1"] = "AuthorizationCode",
["Authentication:Providers:entra:Registrations:delegated:ClientId"] = "entra-client-id",
["Authentication:Providers:entra:Registrations:delegated:RedirectUri"] = "http://localhost:8400/callback/",
["Authentication:Providers:dcr:Type"] = "OAuth21PkceDcr",
["Authentication:Providers:dcr:AllowInteractiveApproval"] = "true",
["Authentication:Providers:dcr:RedirectUri"] = "http://localhost:8400/callback/",
```

Assert provider type, authorities, scopes, grant types, client ID, redirect URI, and DCR policy. Add separate tests rejecting an unknown provider type, relative authority URI, duplicate grant, blank configured client ID, and any configuration that defines only `Authentication:Connections`.

- [ ] **Step 2: Run the option tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientOptionsTests" --no-restore
```

Expected: compile failures because provider types and `Providers` do not exist, followed by failing compatibility-removal assertions after compilation is restored.

- [ ] **Step 3: Implement strict provider configuration binding**

Parse `Authentication:Providers` explicitly rather than relying on permissive binder defaults. Validate:

- Provider ID is the configuration child key.
- `Type` parses to a defined enum value.
- Allowed authorities/origins and metadata/server/redirect URIs are absolute.
- Trust endpoints use HTTPS; registration redirect URIs may be absolute loopback HTTP.
- Registration grant types parse to defined `A2AOAuthFlowType` values and are distinct.
- Configured registrations have nonblank client IDs.
- `OAuth21PkceDcr` requires a redirect URI when interactive registration is enabled.
- Entra defaults `AllowedAuthorities` to `https://login.microsoftonline.com` when omitted.
- The old `Authentication:Connections` section is ignored and cannot satisfy any provider lookup.

- [ ] **Step 4: Run the option tests and verify GREEN**

Run the command from Step 2. Expected: all `A2AClientOptionsTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClientOptionsTests.cs
git commit -m "refactor: model A2A OAuth credential providers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 2: Advertised Authentication Without Local Connection Names

**Files:**
- Modify: `src/samples/A2A/A2AClient/A2AAgentCardAuthentication.cs`
- Modify: `src/samples/A2A/A2AClient/A2AInTaskAuthorizationClient.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAgentCardAuthenticationTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AInTaskAuthorizationClientTests.cs`

**Interfaces:**
- `A2AAgentCardAuthentication.SecuritySchemeName` becomes `string?`.
- Add `string? MetadataUrl`, populated from `OAuth2SecurityScheme.OAuth2MetadataUrl`.
- Replace:

```csharp
CreateInTask(OAuthFlows flows, IReadOnlyList<string> scopes, string connectionName)
```

with:

```csharp
CreateInTask(OAuthFlows flows, IReadOnlyList<string> scopes)
```

- `CreateInTask` sets `SecuritySchemeName` to `null`.

- [ ] **Step 1: Write failing scheme-independence tests**

Add tests proving:

- Two Agent Cards with scheme names `browser-oauth` and `arbitrary-name` produce otherwise equivalent authentication descriptions.
- An advertised `OAuth2MetadataUrl` is retained independently of the scheme name.
- `CreateInTask` requires no connection name and returns `SecuritySchemeName == null`.
- `A2AInTaskAuthorizationClient` passes an authentication description with no scheme name to `IA2AAccessTokenProvider`.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AAgentCardAuthenticationTests|FullyQualifiedName~A2AInTaskAuthorizationClientTests" --no-restore
```

Expected: compile failure because the new `CreateInTask` signature and nullable scheme semantics do not exist.

- [ ] **Step 3: Remove local naming from advertised authentication**

Delete `A2AInTaskAuthorizationClient.ConnectionName`, update `CreateInTask`, and retain the Agent Card scheme name only when the authentication came from an Agent Card requirement.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run Step 2. Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\samples\A2A\A2AClient\A2AAgentCardAuthentication.cs src\samples\A2A\A2AClient\A2AInTaskAuthorizationClient.cs src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AAgentCardAuthenticationTests.cs src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AInTaskAuthorizationClientTests.cs
git commit -m "refactor: decouple A2A auth from scheme names" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 3: Credential Binding and Configured Provider Resolution

**Files:**
- Create: `src/samples/A2A/A2AClient/OAuthCredentialBinding.cs`
- Create: `src/samples/A2A/A2AClient/IOAuthCredentialProvider.cs`
- Create: `src/samples/A2A/A2AClient/OAuthProviderMatch.cs`
- Create: `src/samples/A2A/A2AClient/OAuthCredentialProviderResolver.cs`
- Create: `src/samples/A2A/A2AClient/EntraOAuthCredentialProvider.cs`
- Create: `src/samples/A2A/A2AClient/GenericOAuth2CredentialProvider.cs`
- Create: `src/samples/A2A/A2AClient/GenericOAuth2PkceCredentialProvider.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthEndpointValidator.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthCredentialProviderResolverTests.cs`

**Interfaces:**

```csharp
internal sealed record OAuthCredentialBinding(
    string ProviderId,
    OAuthCredentialProviderType ProviderType,
    string RegistrationId,
    OAuthClientRegistration Registration,
    A2AOAuthFlowType FlowType,
    Uri? AuthorizationEndpoint,
    Uri? DeviceAuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri? MetadataUrl,
    Uri? RegistrationEndpoint,
    IReadOnlyList<string> EffectiveScopes,
    string ProviderIdentity);

internal sealed record OAuthProviderMatch(
    IOAuthCredentialProvider Provider,
    string? RegistrationId,
    int ProviderSpecificity,
    int AuthoritySpecificity,
    int RegistrationSpecificity);

internal interface IOAuthCredentialProvider
{
    string Id { get; }
    OAuthProviderMatch? Match(A2AAgentCardAuthentication authentication);
    Task<OAuthCredentialBinding> BindAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        OAuthProviderMatch match,
        CancellationToken cancellationToken);
}

internal interface IOAuthCredentialProviderResolver
{
    Task<OAuthCredentialBinding> ResolveAsync(
        Uri agentOrigin,
        A2AAgentCardAuthentication authentication,
        CancellationToken cancellationToken);
}
```

- Resolver orders by `ProviderSpecificity`, then `AuthoritySpecificity`, then `RegistrationSpecificity`; equal top rank is an error.
- Configured registration specificity is greater than DCR specificity.

- [ ] **Step 1: Write resolver ranking and trust tests**

Create literal Agent Card authentication fixtures and tests for:

- Entra matches `login.microsoftonline.com` despite arbitrary scheme name.
- Generic OAuth matches all advertised endpoints on an allowed origin.
- Generic PKCE rejects Authorization Code registrations with `UsePkce == false`.
- Client Credentials selects only a registration advertising `ClientCredentials`.
- Entra outranks generic origin matching.
- An exact authority path outranks host-wide origin matching.
- Equal-ranked configured providers throw and list both provider IDs.
- Mixed-origin authorization/token endpoints are rejected.
- No compatible registration reports provider ID and required flow.

- [ ] **Step 2: Run resolver tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuthCredentialProviderResolverTests" --no-restore
```

Expected: compile failure because resolver and provider implementations do not exist.

- [ ] **Step 3: Implement URI trust primitives**

Add methods that:

- Parse advertised endpoint strings as absolute HTTPS URIs.
- Compare scheme/host/port for origin rules.
- Compare normalized URI path prefixes for authority rules.
- Return match specificity without transmitting credentials.
- Reject endpoints outside a provider's complete trust set.

- [ ] **Step 4: Implement configured provider matching and binding**

Implement Entra, Generic OAuth 2.0, and Generic OAuth 2.0 PKCE providers. Normalize effective scopes as advertised scopes followed by provider additional scopes, removing ordinal duplicates. Preserve that stable order for token requests; sort a copy only when constructing cache identity. Bind only after every endpoint needed by the selected flow passes validation.

- [ ] **Step 5: Run resolver tests and verify GREEN**

Run Step 2. Expected: all resolver tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests\OAuthCredentialProviderResolverTests.cs
git commit -m "feat: resolve configured A2A credential providers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 4: Binding-based Token Acquisition and Cache Identity

**Files:**
- Modify: `src/samples/A2A/A2AClient/IOAuthTokenClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthTokenClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthDeviceCodeTokenClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthAuthorizationCodeTokenClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthClientCredentialsTokenClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthTokenEndpointClient.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthScopeResolver.cs`
- Modify: `src/samples/A2A/A2AClient/A2AAccessTokenProvider.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAccessTokenProviderTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthDeviceCodeTokenClientTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthAuthorizationCodeTokenClientTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthClientCredentialsTokenClientTests.cs`

**Interfaces:**

```csharp
internal interface IOAuthTokenClient
{
    Task<OAuthAccessToken> AcquireTokenAsync(
        OAuthCredentialBinding binding,
        CancellationToken cancellationToken);

    Task<OAuthAccessToken> RefreshTokenAsync(
        OAuthCredentialBinding binding,
        string refreshToken,
        CancellationToken cancellationToken);
}
```

`A2AAccessTokenProvider` constructor receives:

```csharp
IOAuthCredentialProviderResolver resolver,
IOAuthTokenClient oauth,
Uri agentOrigin
```

The internal test constructor additionally accepts `TimeProvider`.

- [ ] **Step 1: Rewrite token-provider tests around bindings**

Add tests proving:

- Resolver is called before acquisition.
- Two authentication descriptions with different scheme names but the same binding reuse one token.
- Different provider IDs, registration IDs, client IDs, flow types, or scope sets do not share tokens.
- Concurrent requests resolving to the same binding share acquisition.
- Refresh uses the original binding.

Update flow-specific client tests to construct bindings with already validated endpoint URIs and registrations.

- [ ] **Step 2: Run token tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AAccessTokenProviderTests|FullyQualifiedName~OAuthDeviceCodeTokenClientTests|FullyQualifiedName~OAuthAuthorizationCodeTokenClientTests|FullyQualifiedName~OAuthClientCredentialsTokenClientTests" --no-restore
```

Expected: compile failures because token clients still accept authentication plus `OAuthConnectionOptions`.

- [ ] **Step 3: Refactor token clients to consume bindings**

Remove endpoint trust selection from flow clients; they receive validated URIs from the binding. Move client authentication fields to `binding.Registration`. Use `binding.EffectiveScopes` directly. Authorization Code creates PKCE values when `Registration.UsePkce` is true and rejects a PKCE-required provider binding if false.

- [ ] **Step 4: Refactor access-token cache identity**

Use an ordinal cache key containing provider identity, provider ID, registration ID, client ID, flow, and sorted effective scopes. Do not include Agent Card scheme name.

- [ ] **Step 5: Run token tests and verify GREEN**

Run Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests
git commit -m "refactor: acquire A2A tokens from credential bindings" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 5: Authorization Server Metadata Discovery

**Files:**
- Create: `src/samples/A2A/A2AClient/OAuthAuthorizationServerMetadata.cs`
- Create: `src/samples/A2A/A2AClient/IOAuthAuthorizationServerMetadataClient.cs`
- Create: `src/samples/A2A/A2AClient/OAuthAuthorizationServerMetadataClient.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuthAuthorizationServerMetadataClientTests.cs`

**Interfaces:**

```csharp
internal sealed record OAuthAuthorizationServerMetadata(
    Uri Issuer,
    Uri MetadataUrl,
    Uri? AuthorizationEndpoint,
    Uri TokenEndpoint,
    Uri RegistrationEndpoint,
    IReadOnlyList<string> GrantTypesSupported,
    IReadOnlyList<string> CodeChallengeMethodsSupported);

internal interface IOAuthAuthorizationServerMetadataClient
{
    Task<OAuthAuthorizationServerMetadata> DiscoverAsync(
        IReadOnlyList<Uri> metadataCandidates,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write metadata discovery tests**

Use a stub `HttpMessageHandler` and test:

- The first valid candidate is returned.
- Non-success responses continue to the next candidate.
- Invalid JSON, non-HTTPS issuer/endpoints, issuer mismatch, missing token endpoint, missing registration endpoint, unsupported Authorization Code grant, and missing `S256` are rejected.
- Automatic redirects are disabled; every 3xx response is rejected as a candidate failure without following its `Location`.
- Final failure lists every attempted metadata URL without including response bodies containing secrets.

- [ ] **Step 2: Run metadata tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuthAuthorizationServerMetadataClientTests" --no-restore
```

Expected: compile failure because metadata types do not exist.

- [ ] **Step 3: Implement RFC 8414/OIDC metadata parsing**

Send cancellation-aware GET requests. Deserialize only required fields using `System.Text.Json`. Validate issuer and endpoints before returning. Construct the metadata `HttpClient` with `HttpClientHandler.AllowAutoRedirect = false`; treat 3xx responses as candidate failures.

Candidate generation is deterministic:

- A configured or advertised metadata URL is used exactly as supplied after HTTPS validation.
- For an endpoint origin `https://host[:port]`, try `/.well-known/oauth-authorization-server` and then `/.well-known/openid-configuration`.
- For a prompted issuer/server URI, preserve its path. Generate the RFC 8414 URL by inserting `/.well-known/oauth-authorization-server` before the issuer path, then generate the OpenID Connect URL by appending `/.well-known/openid-configuration` to the issuer path.
- Remove duplicate candidate absolute URIs using ordinal comparison while retaining first-seen order.

- [ ] **Step 4: Run metadata tests and verify GREEN**

Run Step 2. Expected: all metadata tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\samples\A2A\A2AClient\OAuthAuthorizationServerMetadata* src\samples\A2A\A2AClient\IOAuthAuthorizationServerMetadataClient.cs src\tests\Microsoft.Agents.Samples.A2A.Tests\OAuthAuthorizationServerMetadataClientTests.cs
git commit -m "feat: discover OAuth authorization server metadata" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 6: Dynamic Client Registration and Registration Store

**Files:**
- Create: `src/samples/A2A/A2AClient/IDynamicClientRegistrationClient.cs`
- Create: `src/samples/A2A/A2AClient/DynamicClientRegistrationClient.cs`
- Create: `src/samples/A2A/A2AClient/IOAuthClientRegistrationStore.cs`
- Create: `src/samples/A2A/A2AClient/InMemoryOAuthClientRegistrationStore.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/DynamicClientRegistrationClientTests.cs`

**Interfaces:**

```csharp
internal interface IDynamicClientRegistrationClient
{
    Task<OAuthClientRegistration> RegisterPublicClientAsync(
        string registrationId,
        Uri registrationEndpoint,
        Uri redirectUri,
        CancellationToken cancellationToken);
}

internal interface IOAuthClientRegistrationStore
{
    Task<OAuthClientRegistration?> GetAsync(
        string providerIdentity,
        Uri redirectUri,
        CancellationToken cancellationToken);

    Task SaveAsync(
        string providerIdentity,
        Uri redirectUri,
        OAuthClientRegistration registration,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write DCR and store tests**

Test the exact RFC 7591 request fields:

```json
{
  "redirect_uris": ["http://localhost:8400/callback/"],
  "grant_types": ["authorization_code"],
  "response_types": ["code"],
  "token_endpoint_auth_method": "none"
}
```

Assert:

- Client ID is required.
- Returned token endpoint auth method must be absent or `none`.
- Returned redirect URIs, when present, must include the requested redirect URI.
- Any returned client secret causes rejection.
- Safe OAuth error fields are included for non-success responses; secret values are not.
- In-memory store uses ordinal provider identity plus normalized redirect URI and honors cancellation.

- [ ] **Step 2: Run DCR tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~DynamicClientRegistrationClientTests" --no-restore
```

Expected: compile failure because DCR and store interfaces do not exist.

- [ ] **Step 3: Implement public-client registration**

Use a redirect-disabled `HttpClient`. POST JSON with `System.Text.Json`. Validate the final request origin, response fields, and public-client constraints. Return an `OAuthClientRegistration` with `AuthorizationCode`, `UsePkce = true`, no secret, and authentication method `None`.

- [ ] **Step 4: Implement the in-memory store**

Use a `ConcurrentDictionary<string, OAuthClientRegistration>` and a stable key based on provider identity and `redirectUri.AbsoluteUri`. Throw on cancellation before read/write.

- [ ] **Step 5: Run DCR tests and verify GREEN**

Run Step 2. Expected: all DCR tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests\DynamicClientRegistrationClientTests.cs
git commit -m "feat: register OAuth public clients dynamically" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 7: Interactive OAuth 2.1 DCR Provider

**Files:**
- Create: `src/samples/A2A/A2AClient/IOAuthProviderApproval.cs`
- Create: `src/samples/A2A/A2AClient/ConsoleOAuthProviderApproval.cs`
- Create: `src/samples/A2A/A2AClient/OAuth21DcrCredentialProvider.cs`
- Modify: `src/samples/A2A/A2AClient/OAuthCredentialProviderResolver.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/OAuth21DcrCredentialProviderTests.cs`

**Interfaces:**

```csharp
internal sealed record OAuthProviderApprovalRequest(
    Uri AgentOrigin,
    string? SecuritySchemeName,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<Uri> AdvertisedEndpoints,
    OAuthAuthorizationServerMetadata Metadata,
    Uri RedirectUri);

internal interface IOAuthProviderApproval
{
    Task<Uri?> RequestServerUriAsync(
        Uri agentOrigin,
        IReadOnlyList<Uri> advertisedEndpoints,
        CancellationToken cancellationToken);

    Task<bool> ApproveAsync(
        OAuthProviderApprovalRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write provider tests for discovery, approval, and reuse**

Test:

- Authorization Code is required.
- A stored registration is reused without approval or DCR.
- Configured metadata URL is attempted first.
- An advertised `OAuth2MetadataUrl` is attempted when available.
- Otherwise candidates include both `/.well-known/oauth-authorization-server` and `/.well-known/openid-configuration` on the authorization/token endpoint origins.
- Discovery failure calls `RequestServerUriAsync`; the returned issuer/server URI produces the path-preserving RFC 8414 and OpenID Connect candidates defined in Task 5.
- No registration call occurs before `ApproveAsync` returns true.
- Approval denial throws an explicit cancellation-of-registration error.
- Successful registration is saved and returned in a binding.
- Provider identity uses discovered issuer, not scheme name or agent URL.

- [ ] **Step 2: Run provider tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuth21DcrCredentialProviderTests" --no-restore
```

Expected: compile failure because approval and DCR provider types do not exist.

- [ ] **Step 3: Implement two-phase DCR matching**

`Match` returns the lowest provider specificity and DCR registration specificity so configured trusted providers win. `BindAsync` performs metadata discovery, store lookup, approval, registration, and save in that order.

- [ ] **Step 4: Implement console approval**

Write all request details to the configured output. Accept only an explicit `y` or `yes`. For server URL fallback, repeatedly read until the user enters an absolute HTTPS URI or submits an empty line to cancel. Honor cancellation while reading by using `ReadLineAsync(cancellationToken)` on supported target framework APIs.

- [ ] **Step 5: Run provider tests and verify GREEN**

Run Step 2. Expected: all DCR provider tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests\OAuth21DcrCredentialProviderTests.cs
git commit -m "feat: approve and resolve OAuth DCR providers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 8: Program Composition and Shared Agent Card/In-task Resolution

**Files:**
- Modify: `src/samples/A2A/A2AClient/Program.cs`
- Modify: `src/samples/A2A/A2AClient/A2AAccessTokenProvider.cs`
- Modify: `src/samples/A2A/A2AClient/A2AInTaskAuthorizationClient.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientProgramTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAccessTokenProviderTests.cs`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AInTaskAuthorizationClientTests.cs`

**Interfaces:**
- Add a provider factory method:

```csharp
internal static IReadOnlyList<IOAuthCredentialProvider> CreateCredentialProviders(
    A2AClientAuthenticationOptions options,
    IOAuthAuthorizationServerMetadataClient metadataClient,
    IDynamicClientRegistrationClient registrationClient,
    IOAuthClientRegistrationStore registrationStore,
    IOAuthProviderApproval approval);
```

- Agent Card and in-task calls continue using `IA2AAccessTokenProvider`; the shared implementation resolves both.

- [ ] **Step 1: Write composition and shared-resolution tests**

Add tests proving:

- Program creates the correct provider implementation for each configured type.
- In-task authorization resolves by endpoints and flow with no scheme/local alias.
- Agent Card authentication with arbitrary scheme names resolves to the same provider.
- The DCR provider receives the configured approval and registration-store instances.
- Existing authenticated A2A requests still attach tokens only to the agent origin.

- [ ] **Step 2: Run composition tests and verify RED**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientProgramTests|FullyQualifiedName~A2AAccessTokenProviderTests|FullyQualifiedName~A2AInTaskAuthorizationClientTests" --no-restore
```

Expected: compile or assertion failures because Program does not compose the provider pipeline.

- [ ] **Step 3: Compose the runtime pipeline**

Create separate `HttpClient` instances backed by `HttpClientHandler { AllowAutoRedirect = false }` for metadata and DCR. Construct the in-memory registration store, console approval, metadata client, DCR client, providers, resolver, OAuth token client, and access token provider. Pass the configured agent origin to the access-token provider.

- [ ] **Step 4: Remove all hard-coded connection lookup**

Search:

```powershell
rg -n "GetRequiredConnection|Authentication:Connections|ConnectionName = \"delegated\"|OAuthConnectionOptions" src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests
```

Expected after cleanup: no production matches; test matches only where asserting the obsolete configuration is ignored.

- [ ] **Step 5: Run composition tests and verify GREEN**

Run Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\samples\A2A\A2AClient src\tests\Microsoft.Agents.Samples.A2A.Tests
git commit -m "feat: compose A2A credential provider resolution" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 9: Sample Configuration and Documentation

**Files:**
- Modify: `src/samples/A2A/A2AClient/appsettings.json`
- Modify: `src/samples/A2A/A2AClient/README.md`
- Modify: `src/samples/A2A/A2AAgent/README.md`
- Modify: `src/samples/A2A/A2AAgent/A2A-DEVELOPER-GUIDE.md`
- Test: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientOptionsTests.cs`

**Interfaces:**
- Documentation uses only `Authentication:Providers`.
- User-secret commands use provider and registration IDs, never Agent Card scheme names.

- [ ] **Step 1: Update the example configuration**

Replace `delegated`, `github`, and `application` connection examples with:

- One Entra provider containing delegated and application registrations.
- One Generic OAuth 2.0 PKCE provider.
- One interactive OAuth 2.1 DCR provider.

Use placeholder IDs and empty secrets only.

- [ ] **Step 2: Rewrite the authentication documentation**

Document:

- What the Agent Card discovers.
- What local providers and registrations supply.
- Deterministic provider matching.
- Ambiguity and no-match failures.
- Interactive DCR display and approval.
- Origin-first metadata discovery and server URL fallback.
- In-memory registration lifetime.
- How to replace the registration store in an application.
- Updated user-secret commands.

- [ ] **Step 3: Verify documentation examples against option tests**

Add or update an option test that loads the sample `appsettings.json` and asserts all provider types parse without real secrets.

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientOptionsTests" --no-restore
```

Expected: all option tests pass.

- [ ] **Step 4: Commit**

```powershell
git add src\samples\A2A\A2AClient\appsettings.json src\samples\A2A\A2AClient\README.md src\samples\A2A\A2AAgent\README.md src\samples\A2A\A2AAgent\A2A-DEVELOPER-GUIDE.md src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClientOptionsTests.cs
git commit -m "docs: explain A2A credential providers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

---

### Task 10: Full Verification and Review

**Files:**
- Review all files changed by Tasks 1-9.

**Interfaces:**
- No new interfaces; this task verifies the complete design.

- [ ] **Step 1: Run the complete A2A sample test project**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --no-restore --verbosity minimal
```

Expected: all tests pass with zero failures and zero warnings.

- [ ] **Step 2: Build the A2A client sample**

```powershell
dotnet build src\samples\A2A\A2AClient\A2AClient.csproj --no-restore
```

Expected: build succeeds with zero warnings and errors.

- [ ] **Step 3: Verify removed coupling**

```powershell
rg -n "Authentication:Connections|GetRequiredConnection|ConnectionName = \"delegated\"|OAuthConnectionOptions" src\samples\A2A\A2AClient src\samples\A2A\A2AAgent src\tests\Microsoft.Agents.Samples.A2A.Tests
```

Expected: no production or documentation references; only a deliberate test asserting the removed configuration is ignored may remain.

- [ ] **Step 4: Verify diff hygiene**

```powershell
git --no-pager diff --check
git --no-pager status --short
```

Expected: no whitespace errors and only intended files changed.

- [ ] **Step 5: Request focused code review**

Review against `docs/a2a-client-credential-provider-design.md`, emphasizing:

- Endpoint and redirect trust boundaries.
- Client-secret exposure.
- DCR approval ordering.
- Ambiguous provider resolution.
- Token cache isolation.
- Cancellation propagation.

Resolve all Critical and Important findings before proceeding.

- [ ] **Step 6: Commit review fixes if needed**

```powershell
git add -- src\samples\A2A\A2AClient src\samples\A2A\A2AAgent src\tests\Microsoft.Agents.Samples.A2A.Tests
git commit -m "fix: address A2A credential provider review" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: df1072e3-4e43-4fcf-be82-4c3b5a79fe3b"
```

Skip this commit only when the review produces no changes.
