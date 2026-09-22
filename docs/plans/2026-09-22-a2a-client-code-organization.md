# A2A Client Code Organization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the A2A client sample and its tests into explicit A2A, configuration, and focused OAuth folders and namespaces without changing runtime behavior.

**Architecture:** `Program` remains the composition root. A2A protocol orchestration moves to `.A2A`; application settings move to `.Configuration`; OAuth is divided into shared primitives plus `.Configuration`, `.Providers`, `.Discovery`, `.Registration`, and `.Tokens`. Tests mirror the production structure with `Tests` inserted into their namespaces.

**Tech Stack:** C#/.NET 10 sample application, `Microsoft.Extensions.Configuration`, `HttpClient`, xUnit, Moq, A2A protocol models.

**Spec:** `docs/a2a-client-code-organization-design.md`

## Global Constraints

- Work in the current branch and primary checkout; do not create a git worktree.
- Do not create or commit files under `docs/superpowers`, `.superpowers`, or `.worktrees`.
- Preserve provider selection, trust evaluation, dynamic registration, token caching, HTTP behavior, configuration keys, validation order, exception messages, and user-visible behavior.
- Keep `Program` in namespace `Microsoft.Agents.Samples.A2AClient`.
- Mirror production folders in namespaces under `Microsoft.Agents.Samples.A2AClient`.
- Mirror A2A client test folders under `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient`.
- Use test namespaces under `Microsoft.Agents.Samples.A2AClient.Tests`.
- Keep interfaces next to the feature they abstract.
- Keep shared OAuth primitives directly under `Microsoft.Agents.Samples.A2AClient.OAuth`.
- Do not rename existing runtime types.
- Do not reorganize A2A Agent production files or A2A Agent tests.
- Do not add package dependencies, factories, dependency-injection abstractions, or assembly boundaries.
- Use `System.Text.Json`; do not introduce Newtonsoft.Json.
- Use fully qualified symbols in any XML documentation `cref` added or modified by this work.
- Each task must leave the A2A client project compiling and its focused tests passing.

---

## File Structure

### Production files

```text
src/samples/A2A/A2AClient/
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

### Test files

```text
src/tests/Microsoft.Agents.Samples.A2A.Tests/
  A2AClient/
    A2AClientNamespaceTests.cs
    A2AClientProgramTests.cs
    A2AClientInterfaceSelectionTests.cs

    Configuration/
      A2AClientOptionsTests.cs

    A2A/
      A2AAccessTokenProviderTests.cs
      A2AAgentCardAuthenticationTests.cs
      A2AAgentCardSkillSelectorTests.cs
      A2AConsoleTests.cs
      A2AConsoleLoopTests.cs
      A2AInTaskAuthorizationClientTests.cs
      A2ARequestPlannerTests.cs
      A2AResponseWriterTests.cs
      AuthenticatedA2AHttpHandlerTests.cs

    OAuth/
      Providers/
        OAuthCredentialProviderResolverTests.cs
        OAuth21DcrCredentialProviderTests.cs
      Discovery/
        OAuthAuthorizationServerMetadataClientTests.cs
      Registration/
        DynamicClientRegistrationClientTests.cs
      Tokens/
        OAuthDeviceCodeTokenClientTests.cs
        OAuthClientCredentialsTokenClientTests.cs
        OAuthAuthorizationCodeTokenClientTests.cs
        LoopbackOAuthAuthorizationCodeReceiverTests.cs
```

### Structural test contract

Create one incremental structural test at:

`src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`

Each task adds assertions for the namespaces it is about to establish:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests;

public class A2AClientNamespaceTests
{
    [Theory]
    [MemberData(nameof(ExpectedNamespaces))]
    public void ProductionTypes_UseExpectedNamespace(Type type, string expectedNamespace)
    {
        Assert.Equal(expectedNamespace, type.Namespace);
    }

    public static TheoryData<Type, string> ExpectedNamespaces => new()
    {
        // Each task adds its representative production types here before moving them.
    };
}
```

The final file must contain these representatives:

```csharp
public static TheoryData<Type, string> ExpectedNamespaces => new()
{
    { typeof(global::Microsoft.Agents.Samples.A2AClient.Configuration.A2AClientOptions), "Microsoft.Agents.Samples.A2AClient.Configuration" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Configuration.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2AClient.OAuth.Configuration" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.OAuthCredentialBinding), "Microsoft.Agents.Samples.A2AClient.OAuth" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Discovery.OAuthAuthorizationServerMetadataClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Discovery" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Registration.DynamicClientRegistrationClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Registration" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Tokens.OAuthTokenClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Tokens" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Providers.OAuthCredentialProviderResolver), "Microsoft.Agents.Samples.A2AClient.OAuth.Providers" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.A2A.A2AAccessTokenProvider), "Microsoft.Agents.Samples.A2AClient.A2A" },
};
```

---

### Task 1: Extract and Move Configuration

**Files:**
- Create: `src/samples/A2A/A2AClient/OAuth/Configuration/OAuthCredentialProviderConfigurationReader.cs`
- Move: `src/samples/A2A/A2AClient/A2AClientOptions.cs` to `src/samples/A2A/A2AClient/Configuration/A2AClientOptions.cs`
- Move: `src/samples/A2A/A2AClient/A2AClientAuthenticationOptions.cs` to `src/samples/A2A/A2AClient/Configuration/A2AClientAuthenticationOptions.cs`
- Move: `src/samples/A2A/A2AClient/OAuthCredentialProviderOptions.cs` to `src/samples/A2A/A2AClient/OAuth/Configuration/OAuthCredentialProviderOptions.cs`
- Move: `src/samples/A2A/A2AClient/OAuthCredentialProviderType.cs` to `src/samples/A2A/A2AClient/OAuth/Configuration/OAuthCredentialProviderType.cs`
- Modify: `src/samples/A2A/A2AClient/Program.cs`
- Create: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`
- Move: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClientOptionsTests.cs` to `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/Configuration/A2AClientOptionsTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

internal static class OAuthCredentialProviderConfigurationReader
{
    public static IReadOnlyDictionary<string, OAuthCredentialProviderOptions> Read(
        IConfigurationSection providersSection);
}
```

- `A2AClientOptions.FromConfiguration` continues to return `A2AClientOptions`.
- `A2AClientAuthenticationOptions.Providers` remains an ordinal dictionary of `OAuthCredentialProviderOptions`.
- Configuration keys and exception messages remain byte-for-byte equivalent.

- [ ] **Step 1: Add failing namespace assertions**

Create `A2AClientNamespaceTests.cs` with the structural test contract above and these initial rows:

```csharp
public static TheoryData<Type, string> ExpectedNamespaces => new()
{
    { typeof(global::Microsoft.Agents.Samples.A2AClient.A2AClientOptions), "Microsoft.Agents.Samples.A2AClient.Configuration" },
    { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2AClient.OAuth.Configuration" },
};
```

- [ ] **Step 2: Run the structural test and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
```

Expected: both rows fail because the types still use
`Microsoft.Agents.Samples.A2AClient`.

- [ ] **Step 3: Create directories and move the existing files**

Run:

```powershell
New-Item -ItemType Directory -Force `
  .\src\samples\A2A\A2AClient\Configuration, `
  .\src\samples\A2A\A2AClient\OAuth\Configuration, `
  .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient\Configuration | Out-Null

git mv .\src\samples\A2A\A2AClient\A2AClientOptions.cs .\src\samples\A2A\A2AClient\Configuration\A2AClientOptions.cs
git mv .\src\samples\A2A\A2AClient\A2AClientAuthenticationOptions.cs .\src\samples\A2A\A2AClient\Configuration\A2AClientAuthenticationOptions.cs
git mv .\src\samples\A2A\A2AClient\OAuthCredentialProviderOptions.cs .\src\samples\A2A\A2AClient\OAuth\Configuration\OAuthCredentialProviderOptions.cs
git mv .\src\samples\A2A\A2AClient\OAuthCredentialProviderType.cs .\src\samples\A2A\A2AClient\OAuth\Configuration\OAuthCredentialProviderType.cs
git mv .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClientOptionsTests.cs .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient\Configuration\A2AClientOptionsTests.cs
```

- [ ] **Step 4: Extract the provider configuration reader**

Create `OAuthCredentialProviderConfigurationReader` by moving these methods
from `A2AClientOptions` without changing their bodies or error text:

```text
ReadOAuthProviders
ReadRegistrations
ValidateRegistrationClientAuthentication
ReadGrantTypes
ReadTrustedUris
ReadAdditionalScopes
ParseEnumValue overloads
ParseOptionalEnumValue
ParseOptionalBoolean
GetOptionalTrustedUri
GetOptionalRedirectUri
GetRequiredTrustedUri overloads
GetRequiredRedirectUri
ValidateTrustedUri
ValidateRedirectUri
GetConfigurationKey
```

Expose only this entry point:

```csharp
public static IReadOnlyDictionary<string, OAuthCredentialProviderOptions> Read(
    IConfigurationSection providersSection)
{
    ArgumentNullException.ThrowIfNull(providersSection);
    return ReadOAuthProviders(providersSection);
}
```

Retain `GetRequiredAbsoluteUri` and its string overload in
`A2AClientOptions`, because they parse the application-level `A2A:AgentUrl`.

Replace the provider assignment in `A2AClientOptions.FromConfiguration` with:

```csharp
Providers = OAuthCredentialProviderConfigurationReader.Read(
    configuration.GetSection("Authentication:Providers")),
```

- [ ] **Step 5: Update namespaces and imports**

Use these declarations:

```csharp
// Configuration/A2AClientOptions.cs
namespace Microsoft.Agents.Samples.A2AClient.Configuration;

// Configuration/A2AClientAuthenticationOptions.cs
namespace Microsoft.Agents.Samples.A2AClient.Configuration;

// OAuth/Configuration/OAuthCredentialProviderOptions.cs
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

// OAuth/Configuration/OAuthCredentialProviderType.cs
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;

// test
namespace Microsoft.Agents.Samples.A2AClient.Tests.Configuration;
```

Add explicit imports where required:

```csharp
using Microsoft.Agents.Samples.A2AClient.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
```

Update `A2AClientNamespaceTests` to reference the moved types by their new
namespaces:

```csharp
{ typeof(global::Microsoft.Agents.Samples.A2AClient.Configuration.A2AClientOptions), "Microsoft.Agents.Samples.A2AClient.Configuration" },
{ typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Configuration.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2AClient.OAuth.Configuration" },
```

- [ ] **Step 6: Run focused configuration tests and verify GREEN**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientOptionsTests|FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj --verbosity minimal
```

Expected: all selected tests pass and the sample builds with zero errors.

- [ ] **Step 7: Commit**

```powershell
git add .\src\samples\A2A\A2AClient .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient
git commit -m "refactor: separate A2A client configuration"
```

---

### Task 2: Move Shared OAuth, Discovery, and Registration

**Files:**
- Move to `src/samples/A2A/A2AClient/OAuth/`:
  - `A2AOAuthFlowType.cs`
  - `OAuthCredentialBinding.cs`
  - `OAuthEndpointValidator.cs`
  - `OAuthScopeResolver.cs`
- Move to `src/samples/A2A/A2AClient/OAuth/Discovery/`:
  - `IOAuthAuthorizationServerMetadataClient.cs`
  - `OAuthAuthorizationServerMetadata.cs`
  - `OAuthAuthorizationServerMetadataClient.cs`
- Move to `src/samples/A2A/A2AClient/OAuth/Registration/`:
  - `IDynamicClientRegistrationClient.cs`
  - `DynamicClientRegistrationClient.cs`
  - `OAuthClientRegistration.cs`
  - `OAuthClientRegistrationValidator.cs`
  - `IOAuthClientRegistrationStore.cs`
  - `InMemoryOAuthClientRegistrationStore.cs`
  - `IOAuthProviderApproval.cs`
  - `ConsoleOAuthProviderApproval.cs`
- Modify: all production files that consume these types
- Modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`
- Move: `OAuthAuthorizationServerMetadataClientTests.cs` to `A2AClient/OAuth/Discovery/`
- Move: `DynamicClientRegistrationClientTests.cs` to `A2AClient/OAuth/Registration/`

**Interfaces:**
- Shared primitives use namespace `Microsoft.Agents.Samples.A2AClient.OAuth`.
- Discovery uses namespace `Microsoft.Agents.Samples.A2AClient.OAuth.Discovery`.
- Registration uses namespace `Microsoft.Agents.Samples.A2AClient.OAuth.Registration`.
- Method signatures, record members, and enum members remain unchanged.

- [ ] **Step 1: Add failing structural assertions**

Add:

```csharp
{ typeof(global::Microsoft.Agents.Samples.A2AClient.OAuthCredentialBinding), "Microsoft.Agents.Samples.A2AClient.OAuth" },
{ typeof(global::Microsoft.Agents.Samples.A2AClient.OAuthAuthorizationServerMetadataClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Discovery" },
{ typeof(global::Microsoft.Agents.Samples.A2AClient.DynamicClientRegistrationClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Registration" },
```

- [ ] **Step 2: Run the structural test and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
```

Expected: the three new rows fail with the original flat namespace.

- [ ] **Step 3: Move the production and test files**

Create the required directories, then use `git mv` for every file listed in
this task. Do not copy and delete; preserve file history.

- [ ] **Step 4: Apply the namespace declarations**

Use:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.OAuth;
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
```

Update the moved tests to:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Discovery;
namespace Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Registration;
```

- [ ] **Step 5: Update imports without changing signatures**

Add the exact namespace imports needed by consumers:

```csharp
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
```

Pay particular attention to:

```text
Program
A2AAccessTokenProvider
A2AAgentCardAuthentication
A2AClientOptions
GenericOAuth2CredentialProvider
OAuth21DcrCredentialProvider
OAuthAuthorizationCodeTokenClient
OAuthClientCredentialsTokenClient
OAuthDeviceCodeTokenClient
OAuthTokenClient
OAuthTokenEndpointClient
```

Do not move or rename any members while resolving imports.

Update the three structural test type references to their final fully
qualified names:

```csharp
typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.OAuthCredentialBinding)
typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Discovery.OAuthAuthorizationServerMetadataClient)
typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Registration.DynamicClientRegistrationClient)
```

- [ ] **Step 6: Run focused tests and build**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuthAuthorizationServerMetadataClientTests|FullyQualifiedName~DynamicClientRegistrationClientTests|FullyQualifiedName~A2AClientOptionsTests|FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj --verbosity minimal
```

Expected: all selected tests pass and the sample builds with zero errors.

- [ ] **Step 7: Commit**

```powershell
git add .\src\samples\A2A\A2AClient .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient
git commit -m "refactor: organize OAuth discovery and registration"
```

---

### Task 3: Move OAuth Token Acquisition

**Files:**
- Move to `src/samples/A2A/A2AClient/OAuth/Tokens/`:
  - `IOAuthTokenClient.cs`
  - `OAuthTokenClient.cs`
  - `OAuthTokenModels.cs`
  - `OAuthTokenEndpointClient.cs`
  - `OAuthDeviceCodeTokenClient.cs`
  - `OAuthClientCredentialsTokenClient.cs`
  - `OAuthAuthorizationCodeTokenClient.cs`
  - `IOAuthAuthorizationCodeReceiver.cs`
  - `LoopbackOAuthAuthorizationCodeReceiver.cs`
- Modify: token consumers in `Program.cs` and `A2AAccessTokenProvider.cs`
- Modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`
- Move to `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/OAuth/Tokens/`:
  - `OAuthDeviceCodeTokenClientTests.cs`
  - `OAuthClientCredentialsTokenClientTests.cs`
  - `OAuthAuthorizationCodeTokenClientTests.cs`
  - `LoopbackOAuthAuthorizationCodeReceiverTests.cs`

**Interfaces:**
- All token types use namespace `Microsoft.Agents.Samples.A2AClient.OAuth.Tokens`.
- `IOAuthTokenClient` signatures remain:

```csharp
Task<OAuthAccessToken> AcquireTokenAsync(
    OAuthCredentialBinding binding,
    CancellationToken cancellationToken);

Task<OAuthAccessToken> RefreshTokenAsync(
    OAuthCredentialBinding binding,
    string refreshToken,
    CancellationToken cancellationToken);
```

- [ ] **Step 1: Add the failing structural assertion**

Add:

```csharp
{ typeof(global::Microsoft.Agents.Samples.A2AClient.OAuthTokenClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Tokens" },
```

- [ ] **Step 2: Run the structural test and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
```

Expected: the new token row fails with the original flat namespace.

- [ ] **Step 3: Move files and update namespaces**

Move every production and test file listed in this task with `git mv`.

Use:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;
namespace Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Tokens;
```

- [ ] **Step 4: Update token imports**

Add:

```csharp
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;
```

Only add each import to files that consume its types. Preserve all token
request construction, refresh logic, PKCE handling, loopback receiver behavior,
and response parsing.

Update the structural test reference to:

```csharp
typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Tokens.OAuthTokenClient)
```

- [ ] **Step 5: Run token and bridge tests**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuthDeviceCodeTokenClientTests|FullyQualifiedName~OAuthClientCredentialsTokenClientTests|FullyQualifiedName~OAuthAuthorizationCodeTokenClientTests|FullyQualifiedName~LoopbackOAuthAuthorizationCodeReceiverTests|FullyQualifiedName~A2AAccessTokenProviderTests|FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj --verbosity minimal
```

Expected: all selected tests pass and the sample builds with zero errors.

- [ ] **Step 6: Commit**

```powershell
git add .\src\samples\A2A\A2AClient .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient
git commit -m "refactor: organize OAuth token acquisition"
```

---

### Task 4: Move OAuth Providers

**Files:**
- Move to `src/samples/A2A/A2AClient/OAuth/Providers/`:
  - `IOAuthCredentialProvider.cs`
  - `OAuthCredentialProviderResolver.cs`
  - `OAuthProviderMatch.cs`
  - `GenericOAuth2CredentialProvider.cs`
  - `GenericOAuth2PkceCredentialProvider.cs`
  - `EntraOAuthCredentialProvider.cs`
  - `OAuth21DcrCredentialProvider.cs`
- Modify: `src/samples/A2A/A2AClient/Program.cs`
- Modify: `src/samples/A2A/A2AClient/A2AAccessTokenProvider.cs`
- Modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`
- Move to `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/OAuth/Providers/`:
  - `OAuthCredentialProviderResolverTests.cs`
  - `OAuth21DcrCredentialProviderTests.cs`

**Interfaces:**
- All provider types use namespace `Microsoft.Agents.Samples.A2AClient.OAuth.Providers`.
- `IOAuthCredentialProvider` and `IOAuthCredentialProviderResolver` signatures remain unchanged.
- `OAuth21DcrCredentialProvider` continues to depend on discovery, registration, approval, and storage interfaces.

- [ ] **Step 1: Add the failing structural assertion**

Add:

```csharp
{ typeof(global::Microsoft.Agents.Samples.A2AClient.OAuthCredentialProviderResolver), "Microsoft.Agents.Samples.A2AClient.OAuth.Providers" },
```

- [ ] **Step 2: Run the structural test and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
```

Expected: the new provider row fails with the original flat namespace.

- [ ] **Step 3: Move production and test files**

Move every file listed in this task with `git mv`.

Use:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.OAuth.Providers;
namespace Microsoft.Agents.Samples.A2AClient.Tests.OAuth.Providers;
```

- [ ] **Step 4: Update provider imports**

Provider implementations require these feature namespaces as applicable:

```csharp
using Microsoft.Agents.Samples.A2AClient.A2A;
using Microsoft.Agents.Samples.A2AClient.OAuth;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
```

`Program` and `A2AAccessTokenProvider` require:

```csharp
using Microsoft.Agents.Samples.A2AClient.OAuth.Providers;
```

Do not alter match scoring, tie handling, registration compatibility, trust
evaluation, discovery fallback, approval ordering, or metadata caching.

Update the structural test reference to:

```csharp
typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Providers.OAuthCredentialProviderResolver)
```

- [ ] **Step 5: Run provider tests and build**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~OAuthCredentialProviderResolverTests|FullyQualifiedName~OAuth21DcrCredentialProviderTests|FullyQualifiedName~A2AAccessTokenProviderTests|FullyQualifiedName~A2AClientProgramTests|FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj --verbosity minimal
```

Expected: all selected tests pass and the sample builds with zero errors.

- [ ] **Step 6: Commit**

```powershell
git add .\src\samples\A2A\A2AClient .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient
git commit -m "refactor: organize OAuth credential providers"
```

---

### Task 5: Move A2A Orchestration and Complete Test Mirroring

**Files:**
- Move to `src/samples/A2A/A2AClient/A2A/`:
  - `A2AAccessTokenProvider.cs`
  - `IA2AAccessTokenProvider.cs`
  - `A2AAgentCardAuthentication.cs`
  - `A2AAgentCardSkillSelector.cs`
  - `A2AAgentOrigin.cs`
  - `A2AAuthenticationSession.cs`
  - `A2AAuthMode.cs`
  - `A2AConsole.cs`
  - `A2AInTaskAuthorizationClient.cs`
  - `A2ARequestPlanner.cs`
  - `A2AResponseWriter.cs`
  - `AuthenticatedA2AHttpHandler.cs`
- Modify: `src/samples/A2A/A2AClient/Program.cs`
- Modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2AClientNamespaceTests.cs`
- Move to `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/A2A/`:
  - `A2AAccessTokenProviderTests.cs`
  - `A2AAgentCardAuthenticationTests.cs`
  - `A2AAgentCardSkillSelectorTests.cs`
  - `A2AConsoleTests.cs`
  - `A2AConsoleLoopTests.cs`
  - `A2AInTaskAuthorizationClientTests.cs`
  - `A2ARequestPlannerTests.cs`
  - `A2AResponseWriterTests.cs`
  - `AuthenticatedA2AHttpHandlerTests.cs`
- Move to `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AClient/`:
  - `A2AClientProgramTests.cs`
  - `A2AClientInterfaceSelectionTests.cs`

**Interfaces:**
- A2A types use namespace `Microsoft.Agents.Samples.A2AClient.A2A`.
- A2A tests use namespace `Microsoft.Agents.Samples.A2AClient.Tests.A2A`.
- Program tests use namespace `Microsoft.Agents.Samples.A2AClient.Tests`.
- `Program` remains in `Microsoft.Agents.Samples.A2AClient`.
- All A2A method signatures and behavior remain unchanged.

- [ ] **Step 1: Add the failing structural assertion**

Add:

```csharp
{ typeof(global::Microsoft.Agents.Samples.A2AClient.A2AAccessTokenProvider), "Microsoft.Agents.Samples.A2AClient.A2A" },
```

- [ ] **Step 2: Run the structural test and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
```

Expected: the new A2A row fails with the original flat namespace.

- [ ] **Step 3: Move production and test files**

Move every file listed in this task with `git mv`. Do not move
`A2AAgentStartupTests.cs` or `A2AAgentOAuthRouteTests.cs`; they test the
separate A2A Agent sample.

- [ ] **Step 4: Update namespaces and imports**

Use:

```csharp
namespace Microsoft.Agents.Samples.A2AClient.A2A;
namespace Microsoft.Agents.Samples.A2AClient.Tests.A2A;
namespace Microsoft.Agents.Samples.A2AClient.Tests;
```

Add feature imports to `Program`:

```csharp
using Microsoft.Agents.Samples.A2AClient.A2A;
using Microsoft.Agents.Samples.A2AClient.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Configuration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Discovery;
using Microsoft.Agents.Samples.A2AClient.OAuth.Providers;
using Microsoft.Agents.Samples.A2AClient.OAuth.Registration;
using Microsoft.Agents.Samples.A2AClient.OAuth.Tokens;
```

Add OAuth feature imports to A2A files only where their types are consumed.
Do not alter request planning, Agent Card selection, in-task authorization,
origin checks, response rendering, console loops, or token-cache behavior.

Update the structural test reference to:

```csharp
typeof(global::Microsoft.Agents.Samples.A2AClient.A2A.A2AAccessTokenProvider)
```

- [ ] **Step 5: Verify the final namespace contract**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AClientNamespaceTests" --verbosity minimal
rg -l "^namespace Microsoft\.Agents\.Samples\.A2AClient;$" .\src\samples\A2A\A2AClient --glob "*.cs"
```

Expected:

- The structural test passes.
- The `rg` command prints only `Program.cs`.

- [ ] **Step 6: Run the complete A2A sample test project**

Run:

```powershell
dotnet build .\src\samples\A2A\A2AClient\A2AClient.csproj --verbosity minimal
dotnet test .\src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --verbosity minimal
```

Expected:

- Build exits with zero errors.
- The complete A2A sample test project reports zero failed tests.

- [ ] **Step 7: Run repository hygiene checks**

Run:

```powershell
git --no-pager diff --check
& .\.github\scripts\verify-no-local-agent-artifacts.ps1
git --no-pager status --short
```

Expected:

- `git diff --check` prints nothing.
- The local-agent-artifact guard succeeds.
- Status contains only the intended A2A client production/test moves and edits.

- [ ] **Step 8: Commit**

```powershell
git add .\src\samples\A2A\A2AClient .\src\tests\Microsoft.Agents.Samples.A2A.Tests\A2AClient
git commit -m "refactor: organize A2A client orchestration"
```
