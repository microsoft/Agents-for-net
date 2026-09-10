# Final Fix Report — A2A samples and OAuth client

- Worktree: `D:\code\Agents-for-net\.worktrees\a2a-samples-oauth-client`
- Branch: `users/tracyboehrer/a2a-samples-oauth-client`
- Baseline reviewed: `6d28702c`
- Fix commits (oldest first):
  - `5bce6c84` fix: enable A2A sample OAuth in Development
  - `28da5303` fix: capture only the validated A2A bearer token
  - `01b45aba` fix: constrain A2A client credentials and keep the console alive
  - `0914b6de` fix: harden loaded-assembly user authorization discovery
  - `ecd6cde4` docs: describe A2A sample auth startup and registration rules

No commit was amended. No live tenant was used; every claim below is backed by an automated test,
an MSBuild evaluation, or a code trace.

---

## 1. CRITICAL — Development run disables authentication

**Root cause.** `AgentHostExtensions.AddAgentAuthorization` computes
`shouldEnable = forceEnable ?? !builder.Environment.IsDevelopment()`
(`src/libraries/Hosting/AspNetCore/AgentHostExtensions.cs:134`). `A2AAgent/Program.cs` passed no
`forceEnable`, and `Properties/launchSettings.json` sets `ASPNETCORE_ENVIRONMENT=Development`, which is
what the README's `dotnet run` uses. The `configure` action therefore never ran: no authentication
scheme was registered, `UseAgents()` → `UseAuthentication()` had no default scheme, no request could
ever produce an authenticated principal, and `A2AUserAuthorization` had no validated request token for
`-delegated`, `-me`, or `-app`.

A second defect sits directly on the same path. The sample declares `"Settings": {}` for the
`delegated` and `app` handlers. JSON configuration turns that into a section with **no children**, and
`IConfigurationSection.Get<OBOSettings>()` returns `null` for such a section, so
`A2AUserAuthorization.GetOBOSettings` dereferenced null and handler construction threw
`NullReferenceException` wrapped as `Failed to create user Authorization provider`. This only surfaces
once handler instances are actually created — that is, once the OAuth routes can run at all.

**Test / reproduction evidence.**
- `A2AAgentStartupTests.Development_WithConfiguredTokenValidation_RegistersJwtBearer` builds the real
  sample startup in the Development environment with GUID `TokenValidation` values and asserts the
  JwtBearer scheme and the `AgentAuthConfigured` marker exist. Reverting
  `ShouldEnableTokenValidation` to `!environment.IsDevelopment()` makes it fail
  (verified: `Failed: 4, Passed: 4` in that run, `Passed: 8` after restore).
- The empty-settings crash reproduced through the same test: the sample's own `appsettings.json` is
  copied to the test output, so `A2AAgentStartup.ConfigureApplication` resolved `MyAgent` and threw
  `Failed to create user Authorization provider for handler name 'A2AUserAuthorization' ... ->
  NullReferenceException` at `A2AUserAuthorization.GetOBOSettings`.
  `A2AUserAuthorizationConfigurationTests.Constructor_WithEmptySettingsSection_CreatesHandlerWithoutOBO`
  now pins it and asserts `settings.Exists() == false` as the root-cause evidence.

**Change.**
- New `src/samples/A2A/A2AAgent/A2AAgentStartup.cs` holds the startup composition so it is testable
  without running a host. `Program.cs` is now four lines that call it.
- `ShouldEnableTokenValidation(configuration, environment)` returns
  `!environment.IsDevelopment() || IsTokenValidationConfigured(configuration)`, and
  `IsTokenValidationConfigured` requires at least one `TokenValidation:Audiences` entry that parses as a
  GUID — exactly what `AddAgentAspNetAuthentication` demands. The shipped `{{ClientId}}` placeholder
  therefore keeps the anonymous-only `dotnet run` working unchanged, while a configured registration
  enables validation locally. Non-Development behavior is unchanged.
- `ConfigureApplication` maps A2A with `MapA2AApplicationEndpoints(requireAuth: false)`, so echo,
  `-multi`, `-stream`, and `-a2a` stay anonymous while the protected routes obtain the validated token
  through their route-scoped `autoSignInHandlers`. `AuthenticationMiddleware` still authenticates every
  request, so a valid bearer produces an authenticated principal on an `AllowAnonymous` endpoint.
- `A2AUserAuthorization.GetOBOSettings` treats a missing or empty settings section as "no OBO".

**Validation.** `A2AAgentStartupTests` (8 tests, including
`A2AEndpoints_RemainAnonymous_WhenTokenValidationIsEnabled`, which asserts `IAllowAnonymous` metadata on
every mapped A2A endpoint while authorization is configured); full sample and A2A extension suites; all
three sample builds; solution build.

---

## 2. CRITICAL — Claim mapping mismatch

**Root cause.** The shared sample helper `src/samples/Shared/AspNetExtensions.cs` configures
`AddJwtBearer` without touching `MapInboundClaims`. `JwtBearerOptions` initializes its default token
handler with `MapInboundClaims = JwtSecurityTokenHandler.DefaultMapInboundClaims`, which is `true`.
Verified against the actual assemblies rather than assumed:

- `JwtSecurityTokenHandler.DefaultMapInboundClaims` = `True`
  (`JsonWebTokenHandler.DefaultMapInboundClaims` is `False`, which is why the value comes from
  `JwtBearerOptions`, not the handler default).
- `DefaultInboundClaimTypeMap`: `sub` → `…/claims/nameidentifier`, `tid` → `…/identity/claims/tenantid`,
  `oid` → `…/identity/claims/objectidentifier`, `scp` → `…/identity/claims/scope`,
  `roles` → `…/claims/role`. `azp`, `appid`, `idtyp`, `aud`, `iss`, `ver` are **unmapped**.

`A2ATokenIdentity` and `MyAgent.BuildIdentitySummary` looked only at `scp`, `tid`, `oid`, `sub`, so with
the sample's own JwtBearer configuration `RequireDelegated` always threw and the identity summary was
empty except for the authentication type.

**Decision.** Accept both spellings rather than disabling inbound mapping. `AspNetExtensions.cs` is
linked into *every* sample by `src/samples/Directory.Build.targets`, so flipping `MapInboundClaims`
there would change unrelated samples. Accepting both names is the established SDK pattern:
`AgentClaims.IsTenantIdIssuerValid` already matches `tid` **or**
`http://schemas.microsoft.com/identity/claims/tenantid`. The sample now uses
`AuthenticationConstants.TenantIdClaim` / `AzpClaim` / `AppIdClaim` and `ClaimTypes.NameIdentifier` /
`ClaimTypes.Role` where those constants exist.

**Test / reproduction evidence.** `A2AAgentStartupTests` resolves the real
`IOptionsMonitor<JwtBearerOptions>` produced by the sample startup, takes its `JsonWebTokenHandler`,
signs a token with a local test key, and validates it through that handler — so the identity under test
is produced by the sample's actual JwtBearer configuration:
- `JwtBearerConfiguration_MapsInboundClaims` asserts `handler.MapInboundClaims` and that `tid`/`scp` are
  absent while the mapped names are present.
- `DelegatedIdentity_FromJwtBearer_IsAcceptedAndSummarized`,
  `ApplicationIdentity_FromJwtBearer_IsAcceptedAndSummarized` (app roles + `azp` + `idtyp`), and
  `ApplicationRolesIdentity_WithoutIdentityTypeClaim_IsStillApplication` cover delegated, app roles, and
  the summary fields.
Removing the alternate-name lookup made three of these fail (`Failed: 4, Passed: 4` overall).

**Validation.** As above; the pre-existing short-claim-name route tests in `A2AAgentOAuthRouteTests`
still pass, so both spellings work.

---

## 3. IMPORTANT/security — unvalidated bearer treated as validated

**Root cause.** `A2ARequestAuthentication.Create` used
`request.HttpContext.User.Identity.IsAuthenticated` as the gate and then read the raw `Authorization`
header. An authenticated principal does not imply the header produced it: a request authenticated by
any other scheme (cookie, certificate, custom) could carry an attacker-chosen `Bearer` value, and that
value was handed to `A2AUserAuthorization` as the "validated request token" — and from there into
`OBOExchange.AcquireTokenOnBehalfOf`.

**Change.** The token is now read from the authentication ticket:
`context.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult`, then
`Properties.GetTokenValue("access_token")` — the same value `HttpContext.GetTokenAsync("access_token")`
returns, populated by handlers with `SaveToken = true`. `UseAgents()` calls `UseAuthentication()`, which
is what sets `IAuthenticateResultFeature`. The shared sample JwtBearer configuration already sets
`options.SaveToken = true` (`src/samples/Shared/AspNetExtensions.cs`), so the sample path is complete.
The unvalidated-JWT claims fallback in `HttpHelper.GetClaimsIdentity` is untouched — claims are still
available for turn identity — but the unvalidated token itself is never exposed. Nothing is logged.

**Test / reproduction evidence.** `A2ARequestAuthenticationTests` now runs requests through the real
`AuthenticationMiddleware` with two schemes: one bearer-like handler that validates and saves the token,
one ticket-only handler that authenticates without reading the header.
- `Create_WithNonBearerPrincipalAndMaliciousBearerHeader_DoesNotExposeHeaderToken` — the attack case.
- `Create_WithValidatedBearer_ExposesSavedToken`, `Create_WithValidatedOpaqueToken_ExposesSavedToken` —
  valid bearer and existing opaque behavior.
- `Create_WithRejectedBearer_DoesNotExposeToken`, `Create_WithAnonymousRequest_HasNoToken`,
  `Create_WithUnauthenticatedBearerRequest_DoesNotExposeToken` — anonymous/rejected behavior.
Temporarily restoring header-based capture made exactly the malicious-header test fail
(`Failed: 1, Passed: 5`), then `Passed: 6` after restore. `A2AAdapterTests` and
`A2AUserAuthorizationTests` were updated to build a real authentication ticket instead of a bare
principal plus header.

**Validation.** A2A extension suite: 117 passed.

---

## 4. IMPORTANT/security — Agent API token sent to any advertised interface

**Root cause.** `AuthenticatedA2AHttpHandler.SendAsync` attached the bearer token to every request, and
`Program.CreateClient` selected the first advertised interface with a supported binding without checking
where it pointed. The Agent Card is data returned by the agent, so a compromised or misconfigured card
could move the Agent API access token to another host.

**Change.**
- New `A2AAgentOrigin` helper: `IsSameOrigin` (scheme + host + `Uri.Port`, which already resolves the
  scheme default), `IsSecureTarget` (HTTPS or loopback), `EnsureCredentialTarget`, `FromAgentUrl`.
- `Program.CreateClient(card, httpClient, agentUrl)` now selects only an interface on the configured
  agent origin and throws before any request when none exists.
- `AuthenticatedA2AHttpHandler` takes the configured agent URL and calls `EnsureCredentialTarget` **only
  when a token would be attached**, so anonymous (`:auth none`) traffic is unaffected. The same
  authenticated `HttpClient` is still shared by `A2ACardResolver` and the task client.
- Redirects: they are followed by the inner handler, *below* the delegating handler, so a redirect
  target would never reach the origin check. Rather than re-implementing redirect handling, the client's
  inner handler is created with the existing `HttpClientHandler.AllowAutoRedirect = false`.

**Test / reproduction evidence.**
- `AuthenticatedA2AHttpHandlerTests.SendAsync_CrossOriginTarget_FailsWithoutSendingToken` (different
  host, different port, different scheme) asserts the request never reached the inner handler and the
  token is absent from the error text; `SendAsync_PlaintextNonLoopbackAgent_FailsWithoutSendingToken`;
  `SendAsync_LoopbackHttpAgent_AttachesToken`; `SendAsync_CrossOriginTargetInNoneMode_IsNotBlocked`;
  `CreateInnerHandler_DisablesAutomaticRedirects`.
- `A2AClientInterfaceSelectionTests` covers same-origin JSON-RPC and HTTP+JSON selection, cross-origin
  rejection, port mismatch, relative URL, preference for the same-origin entry when a foreign one is
  advertised first, and no supported binding.
Reverting the origin checks and the redirect setting failed 6 of these (part of a `Failed: 10` run).

---

## 5. IMPORTANT — per-request failure ended the console

**Root cause.** `A2AConsole.RunAsync` awaited `CreateRequestAsync`, `SendAsync`/`SendStreamingAsync`, and
the history read directly in the loop body. Any `A2AException`, `HttpRequestException`, or JSON failure
propagated to `Program.Main`, which printed the message and exited.

**Change.** The per-command block is wrapped in `try`/`catch` with the exception filter
`IsRecoverableRequestFailure(exception, cancellationToken)`, which returns `false` whenever
`cancellationToken.IsCancellationRequested` — so Ctrl+C is never swallowed — and otherwise matches
`A2AException`, `HttpRequestException`, `HttpIOException`, `JsonException`, `InvalidOperationException`,
`IOException`, `TimeoutException`, and `OperationCanceledException` (request timeouts). Only
`GetType().Name` and `Message` are printed: no stack, no request/response dump, no token.

**Test / reproduction evidence.**
`A2AConsoleLoopTests.RunAsync_FailedSend_ReportsErrorAndKeepsAcceptingCommands` scripts a failing send
followed by `:auth app` and `:q`, and asserts the later command still ran;
`RunAsync_FailedHistoryRead_DoesNotEndTheSession` covers the history path;
`RunAsync_CancellationDuringSend_IsNotSwallowed` asserts an `OperationCanceledException` still escapes
and no "Request failed" line was written. All three failed before the change.

---

## 6. IMPORTANT — OBO exchangeability for a separate public-client registration

**The reviewer is correct.** Trace: `A2AUserAuthorization.CreateTokenResponse` sets
`IsExchangeable = AgentClaims.IsExchangeableToken(jwtToken)`. That method returns `false` when
`idtyp == "user"`, and otherwise returns `aud.Contains(appId)` where `appId` is `azp` (v2) or `appid`
(v1) — `src/libraries/Core/Microsoft.Agents.Authentication/AgentClaims.cs:37-50`. `OBOExchange.HandleOBO`
throws `OBONotExchangeableToken` when `IsExchangeable` is false. A delegated token acquired by a
*separate* public console client has `aud` = Agent API client ID and `azp` = console client ID, so the
audience does not contain the app ID, the token is not exchangeable, and `-me` cannot run — exactly the
registration model the READMEs previously prescribed.

Corroborating evidence from existing SDK tests: the only tokens `UserTokenRestClientTests` treats as
exchangeable are `{"aud":"api://<id>","ver":"1.0","appid":"<same id>"}` and
`{"aud":"<id>","ver":"2.0","azp":"<same id>"}` — audience equal to the requesting app.

**Change — documentation, not implementation.** `AgentClaims.IsExchangeableToken` is core SDK behavior
consumed by `UserTokenRestClient` and `ConnectorUserAuthorization` (Azure Bot Token Service paths) and is
pinned by existing tests. Changing it is outside this branch's approved scope and would risk those
flows, so the READMEs now state the requirement the SDK actually enforces: for `-me`, the delegated
token must be acquired with the **Agent API registration itself** (enable public client flows on it and
set `Authentication:PublicClientId` to the Agent API client ID), and the optional `idtyp` claim must not
be enabled for delegated tokens on that registration. `-delegated` and `-app` do not perform OBO and
still work with a separate console client registration. Expected-failure lists in both READMEs were
updated.

**Test / reproduction evidence.** New tests in `A2AUserAuthorizationTests` using representative claims:
- `SignInUserAsync_DelegatedTokenFromSeparateClientRegistration_IsNotExchangeable` (`aud` ≠ `azp`).
- `SignInUserAsync_DelegatedTokenFromAgentApiRegistration_IsExchangeable` (`aud` = `azp`).
- `SignInUserAsync_WithOBOScopesAndSeparateClientRegistration_Throws` — the OBO handler configuration
  from the sample's `graph` handler fails on the separate-registration token.

**Unresolved concern.** `IsExchangeableToken`'s `aud contains azp` rule is the inverse of canonical
Entra on-behalf-of semantics, where a middle-tier API legitimately exchanges a token whose `aud` is its
own app ID and whose `azp` is a *different* client. That divergence is an SDK-level question beyond this
branch; it is recorded here rather than changed speculatively.

---

## 7. MINOR — analyzer references removed from the A2A sample projects

**Adjudication: correctly removed. No change made.**

`src/samples/Directory.Build.props` declares the analyzer for every project beneath `src/samples`:

```xml
<ProjectReference Include="$(MSBuildThisFileDirectory)..\libraries\Core\Microsoft.Agents.Core.Analyzers\Microsoft.Agents.Core.Analyzers.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

There is no intervening `Directory.Build.props` in `src/samples/A2A`, so MSBuild's nearest-ancestor
lookup finds the samples-level file for all three A2A projects. Verified by evaluation, not inspection:

| Project | `ProjectReference` contains `Core.Analyzers` | Resolved `Analyzer` items matching `Microsoft.Agents.Core.Analyzers.dll` |
| --- | --- | --- |
| `A2AAgent.csproj` | yes | 2 |
| `A2AClient.csproj` | yes | 2 |
| `A2ATCKAgent.csproj` | yes | 2 |

(`dotnet msbuild <proj> -getItem:Analyzer -t:ResolveProjectReferences`; resolved identity
`…\bin\Debug\CplCoreAnalyzers\netstandard2.0\Microsoft.Agents.Core.Analyzers.dll`.) An explicit
per-project reference would have been redundant.

---

## 8. MINOR — loaded-assembly authorization type discovery

**Split adjudication.**

*Order dependence — not a regression.* `FindLoadedProviderType` only runs when configuration supplies a
`Type` with no `Assembly`. Enumerating already-loaded assemblies is the established SDK
extension-discovery pattern: `AgentServiceRegistrationAttribute.GetRegistrations()` and
`AgentSdkInitializer.Initialize()` both iterate `AppDomain.CurrentDomain.GetAssemblies()`. For the
configured scenario the extension assembly is necessarily loaded already — `AddAgentCore` must have
found `Microsoft.Agents.Extensions.A2A`'s assembly-level `AgentServiceRegistrationAttribute` to register
`A2AAdapter` at all, and that happens before `AgentApplicationOptions` (and therefore
`UserAuthorizationDispatcher`) is constructed. `GetProviderConstructor_WithTypeNotInLoadedAssemblies_Throws`
pins the deterministic, documented `UserAuthorizationTypeNotFound` failure for a genuinely absent type.

*Failure handling — a real regression in blast radius, fixed.* Before this branch,
`GetProviderConstructor` never enumerated every loaded assembly, so a third-party assembly that cannot
be inspected could not affect user-authorization handler creation. With the scan in place, a single such
assembly would throw out of `Assembly.GetTypes()` and break **all** handler resolution, including the
built-in default. `GetLoadableTypes` now handles the documented `GetTypes` failures individually —
`ReflectionTypeLoadException` (partial types kept), `TypeLoadException`, `FileNotFoundException`,
`FileLoadException`, `BadImageFormatException` (assembly skipped) — each logged at debug with the
assembly name. There is no broad catch: any other exception still propagates. The scan is also ordered
by assembly name and prefers a fully qualified match over a bare type name, so ambiguity reporting is
stable.

**Test / reproduction evidence.** `UserAuthorizationModuleLoaderTests`:
`GetLoadableTypes_WithReflectionTypeLoadException_ReturnsResolvedTypes`,
`GetLoadableTypes_WithInspectionFailure_SkipsAssembly` (four failure types),
`GetLoadableTypes_WithUnexpectedFailure_Propagates`,
`GetProviderConstructor_WithCollidingSimpleNameInAnotherAssembly_StaysDeterministic` (backed by a real
same-simple-name handler declared in `Microsoft.Agents.Builder.Tests.Collisions`), plus the three
pre-existing resolution tests. 11 tests, green on `net8.0`, `net10.0`, and `net48`.

---

## 9. MINOR — auth mode change retained the continuation task

**Root cause.** `TryHandleCommand`'s `:auth` branch called `SetMode` and left `_taskId`/`_contextId`
untouched, so the next message continued the previous principal's A2A task under a different credential.

**Change.** The command compares the requested mode with the current one and, when it actually changes,
clears the continuation through the new `ClearContinuation()` (also used by `UpdateContinuation`). A
notice is printed only when a task was actually dropped. Re-selecting the same mode changes nothing.

**Test / reproduction evidence.**
`A2AConsoleLoopTests.RunAsync_AuthModeChange_ClearsContinuationTask` captures the outgoing
`SendMessageRequest`s: after an `input-required` task and `:auth delegated`, the next request carries
`TaskId == null` and `ContextId == null`.
`RunAsync_SameAuthMode_KeepsContinuationTask` proves the continuation survives a no-op `:auth`. The
first failed before the change.

---

## 10. Deferred-test triage

**Ledger correction.** The ledger records "`A2AResponseWriterTests` token-exclusion assertions are
vacuous because the sentinel token is not inserted into the task." That is **wrong**. The test builds an
`Artifact` whose `Name` is `$"request headers: Authorization: {sentinelToken}"`, i.e. the sentinel *is*
embedded in artifact metadata, and `A2AResponseWriter.Format` reads only `Status.Message` parts and
`Artifact.Parts` — never `Artifact.Name`. The assertions are therefore meaningful: they prove artifact
metadata (a plausible carrier for header/credential text) is not echoed to the console. The test was
left unchanged.

**High-value console tests added** for the paths behind findings 4, 5, and 9:
`A2AClientInterfaceSelectionTests` (7 tests), `AuthenticatedA2AHttpHandlerTests` (+6 tests),
`A2AConsoleLoopTests` (5 tests driving `RunAsync` with scripted input). Broader protocol coverage
(streaming, attachments, push notifications) remains deferred: it is not required by any finding in this
wave, and none of the fixes touch those paths.

---

## Validation

Run sequentially from the worktree root after the final commit.

| # | Command | Result |
| --- | --- | --- |
| 1 | `dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj` | Passed 58 / Failed 0 (net10.0) |
| 2 | `dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj` | Passed 117 / Failed 0 (net8.0) |
| 3 | `dotnet test src\tests\Microsoft.Agents.Builder.Tests\... --filter "UserAuthenticationFeatureTests|AzureBotUserAuthorizationTests|UserAuthorizationModuleLoaderTests"` | Passed 65 / Failed 0 on each of net8.0, net10.0, net48 |
| 4a | `dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj --no-restore` | 0 warnings, 0 errors |
| 4b | `dotnet build src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj --no-restore` | 0 warnings, 0 errors |
| 4c | `dotnet build src\samples\A2A\A2AClient\A2AClient.csproj --no-restore` | 0 warnings, 0 errors |
| 5 | `dotnet build src\Microsoft.Agents.SDK.sln --no-restore` | Build succeeded, 0 warnings, 0 errors (no restore needed) |
| 6a | `git diff --check` | exit 0, no whitespace or conflict markers |
| 6b | `git status --short` | clean |
| 6c | `git ls-files "*bin*" "*obj*"` | no tracked generated files |
| 6d | secret scan over `git diff 6d28702c..HEAD` | 0 JWT-like literals, 0 secret assignments; only repeated-digit placeholder GUIDs (`1111…`, `2222…`, `3333…`) |

Baselines for comparison: the pre-fix branch had 31 sample tests, 109 A2A extension tests, and 57
targeted Builder authorization tests.

**Diff scope** (`git diff --stat 6d28702c..HEAD`): 23 files, confined to the A2A sample agent and
client, the A2A extension (`A2ARequestAuthentication`, `A2AUserAuthorization`), the Builder
user-authorization module loader, their tests, and the two sample READMEs.

---

## Unresolved concerns

1. **`AgentClaims.IsExchangeableToken` versus canonical OBO semantics** (finding 6). The SDK requires the
   token audience to contain the *requesting* app ID, which excludes the standard middle-tier OBO shape
   (`aud` = API, `azp` = a different client). The sample documentation was aligned with actual SDK
   behavior; whether the SDK rule itself should change is an SDK-level decision outside this branch.
2. **`HttpHelper.GetClaimsIdentity` and non-JWT bearer tokens.** When no authentication succeeds and the
   request carries an opaque (non-JWT) `Bearer` value, `new JwtSecurityToken(...)` throws
   `SecurityTokenMalformedException` out of `A2ARequestAuthentication.Create`. This is pre-existing
   shared hosting behavior, unrelated to the findings, and changing `Microsoft.Agents.Hosting.AspNetCore`
   for it was outside the approved scope. It becomes marginally more reachable now that the A2A endpoints
   are anonymous while authentication is configured; observed while writing the finding-3 tests, and the
   test was written against a well-formed rejected JWT instead.
3. **No live tenant run.** All Entra behavior is covered by locally signed tokens and representative
   claims. The documented manual `:auth delegated` / `:auth app` walkthrough has not been executed
   against a real tenant in this wave.

---

## Residual fix — A2A OAuth docs now call out v2 access tokens

**Change.**
- `src/samples/A2A/A2AAgent/README.md`: added a registration step under the Agent API setup that requires
  `requestedAccessTokenVersion = 2` in the app registration manifest or API settings before publishing
  `api://<agent-client-id>/access_as_user`, so the delegated token has a GUID `aud` that matches
  `TokenValidation:Audiences`.
- `src/samples/A2A/A2AClient/README.md`: added the same `requestedAccessTokenVersion = 2` requirement to
  the public-client setup for `-me`, keeping the existing caveat that `-me` must use the Agent API
  registration itself and that delegated tokens for that registration must not carry `idtyp=user`.

**Validation.**
- Checked the updated wording against `A2AAgentStartup.IsTokenValidationConfigured`, which requires GUID
  audiences, and against `A2AClientOptions` / `MsalTokenClient`, which read
  `Authentication:AgentDelegatedScope` as `api://<agent-client-id>/access_as_user`.
- Ran `git diff --check` successfully; Git only reported the expected LF→CRLF working-tree warnings for the
  two edited README files.
- No live tenant or token run was used.
