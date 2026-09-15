# A2A Namespace and Public API Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize `Microsoft.Agents.Extensions.A2A` into responsibility-specific folders and namespaces while retaining a single root import for normal agent authoring, converting hand-authored source to file-scoped namespaces, and documenting the complete public API.

**Architecture:** Keep the everyday authoring and startup surface in `Microsoft.Agents.Extensions.A2A`. Move pipeline, authorization, Agent Card, client, storage, serialization, routing-internal, and hosting-internal types into matching subfolders and subnamespaces. Use reflection tests to lock the namespace contract and project-local documentation diagnostics to enforce complete XML docs.

**Tech Stack:** C# 13, .NET 8/10, xUnit, MSBuild XML documentation generation, ASP.NET Core, a2a-dotnet.

**Spec:** `docs/superpowers/specs/2026-09-15-a2a-namespace-cleanup-design.md`

## Global Constraints

- The package is preview; namespace changes are intentionally breaking.
- Do not add compatibility wrappers, type forwarding, or duplicate old namespaces.
- Keep normal agent authoring and startup APIs in `Microsoft.Agents.Extensions.A2A`.
- Match responsibility-specific namespaces with physical folders.
- Use file-scoped namespaces in every hand-authored C# file.
- Do not edit `Properties/Resources.Designer.cs`; it is generated.
- Document every public type and every public or protected member.
- Use `<inheritdoc/>` only when the inherited contract fully describes the member.
- Keep A2A-specific behavior inside `Microsoft.Agents.Extensions.A2A`, its tests, samples, and directly related documentation.
- Preserve runtime behavior; this cleanup changes organization and documentation, not protocol semantics.
- Include the repository-required Copilot co-author and session trailers on every commit.

---

### Task 1: Establish root authoring, routing, and hosting boundaries

**Files:**
- Create: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Rename: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AgentApplicationAttributes.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AMessageRouteAttribute.cs`
- Move: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2ASkillRegistration.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Routing/A2ASkillRegistration.cs`
- Move: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/HandlerUtils.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Routing/HandlerUtils.cs`
- Create: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Hosting/A2AServiceRegistrar.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AServiceExtensions.cs`
- Modify: root authoring files that consume routing internals
- Modify: affected extension tests

**Interfaces:**
- Consumes: Existing root authoring API and assembly service-registration attribute.
- Produces: `Microsoft.Agents.Extensions.A2A.Routing` internal helpers, `Microsoft.Agents.Extensions.A2A.Hosting.A2AServiceRegistrar`, and the approved root authoring surface.

- [ ] **Step 1: Add failing namespace contract tests**

Create `A2ANamespaceOrganizationTests.cs` with:

```csharp
using System;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2ANamespaceOrganizationTests
{
    private const string RootNamespace = "Microsoft.Agents.Extensions.A2A";

    [Theory]
    [InlineData("A2ASkillRegistration", RootNamespace + ".Routing")]
    [InlineData("HandlerUtils", RootNamespace + ".Routing")]
    [InlineData("A2AServiceRegistrar", RootNamespace + ".Hosting")]
    public void Type_UsesExpectedNamespace(string typeName, string expectedNamespace)
    {
        Type type = typeof(A2AAgentExtension).Assembly
            .GetTypes()
            .Single(candidate => candidate.Name == typeName);

        Assert.Equal(expectedNamespace, type.Namespace);
    }

    [Theory]
    [InlineData(typeof(A2AAgentExtension))]
    [InlineData(typeof(A2AExtensionAttribute))]
    [InlineData(typeof(IA2ATurnContext))]
    [InlineData(typeof(IA2AActivity))]
    [InlineData(typeof(A2ARouteHandler))]
    [InlineData(typeof(A2ASkillAttribute))]
    [InlineData(typeof(A2ASkillBuilder))]
    [InlineData(typeof(A2AMessageRouteAttribute))]
    [InlineData(typeof(A2AAgentTransportProtocol))]
    [InlineData(typeof(A2AServiceExtensions))]
    [InlineData(typeof(A2AExtensions))]
    public void AuthoringType_RemainsInRootNamespace(Type type)
    {
        Assert.Equal(RootNamespace, type.Namespace);
    }
}
```

- [ ] **Step 2: Run the namespace test and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests"
```

Expected: the three subnamespace assertions fail because all three types are still in the root namespace.

- [ ] **Step 3: Move routing internals**

Move `A2ASkillRegistration` and `HandlerUtils` into the `Routing` folder and use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Routing;
```

Add `using Microsoft.Agents.Extensions.A2A.Routing;` only inside extension implementation files that consume these internal types. Do not move `A2ARouteHandler`, `A2ASkillAttribute`, `A2ASkillBuilder`, or `A2AMessageRouteAttribute` from the root namespace.

- [ ] **Step 4: Split hosting registration from startup extensions**

Move `A2AServiceRegistrar` out of `A2AServiceExtensions.cs` into `Hosting/A2AServiceRegistrar.cs` with:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Hosting;
```

Update the assembly attribute to:

```csharp
[assembly: Microsoft.Agents.Builder.AgentServiceRegistrationAttribute(
    typeof(Microsoft.Agents.Extensions.A2A.Hosting.A2AServiceRegistrar))]
```

Keep `A2AServiceExtensions` in the root namespace so consumers need no additional startup import. Preserve `A2AServiceRegistrar` accessibility unless a focused service-registration test proves that narrowing it is safe.

- [ ] **Step 5: Rename the route attribute file**

Rename `AgentApplicationAttributes.cs` to `A2AMessageRouteAttribute.cs`. Keep the public type in the root namespace and do not change its behavior or signature.

- [ ] **Step 6: Convert touched files to file-scoped namespaces**

Every hand-authored file touched in this task must use:

```csharp
namespace Microsoft.Agents.Extensions.A2A;
```

or its approved subnamespace equivalent.

- [ ] **Step 7: Run focused and full extension tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2AServiceRegistrationTests|FullyQualifiedName~A2ASkillAttributeTests|FullyQualifiedName~AgentApplicationAttributesTests"
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests\Microsoft.Agents.Extensions.A2A.Tests
git commit -m "refactor: organize A2A authoring infrastructure" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 2: Move the A2A request pipeline

**Files:**
- Move: `A2AActivity.cs` → `Pipeline/A2AActivity.cs`
- Move: `A2AAdapter.cs` → `Pipeline/A2AAdapter.cs`
- Move: `A2AHttpProcessor.cs` → `Pipeline/A2AHttpProcessor.cs`
- Move: `A2AJsonRpcProcessor.cs` → `Pipeline/A2AJsonRpcProcessor.cs`
- Move: `A2AMessageActivity.cs` → `Pipeline/A2AMessageActivity.cs`
- Move: `A2ATurnContext.cs` → `Pipeline/A2ATurnContext.cs`
- Move: `IA2AHttpAdapter.cs` → `Pipeline/IA2AHttpAdapter.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Modify: pipeline consumers in the A2A library
- Modify: pipeline-focused extension tests
- Modify: A2A samples that explicitly reference concrete pipeline types

**Interfaces:**
- Consumes: Root `IA2AActivity`, `IA2ATurnContext`, `A2AAgentExtension`, and routing contracts.
- Produces: `Microsoft.Agents.Extensions.A2A.Pipeline` adapter, concrete activities/turn context, HTTP adapter contract, and protocol processors.

- [ ] **Step 1: Add failing pipeline namespace cases**

Add these rows to the existing `Type_UsesExpectedNamespace` theory:

```csharp
[InlineData("A2AAdapter", RootNamespace + ".Pipeline")]
[InlineData("IA2AHttpAdapter", RootNamespace + ".Pipeline")]
[InlineData("A2AJsonRpcProcessor", RootNamespace + ".Pipeline")]
[InlineData("A2AMessageActivity", RootNamespace + ".Pipeline")]
[InlineData("A2ATurnContext", RootNamespace + ".Pipeline")]
```

- [ ] **Step 2: Run focused test and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests"
```

Expected: the five new rows fail because those public types remain in the root namespace.

- [ ] **Step 3: Move pipeline files and update namespaces**

Move the seven files into `Pipeline` and use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Pipeline;
```

Keep `IA2AActivity` and `IA2ATurnContext` physically and logically at root. Update internal imports and all test/sample references atomically.

- [ ] **Step 4: Preserve service registration and adapter discovery**

Update `A2AServiceExtensions`, `A2AServiceRegistrar`, channel adapter attributes, assembly references, and DI tests to reference `Pipeline.A2AAdapter` and `Pipeline.IA2AHttpAdapter`. Do not alter service lifetimes or endpoint behavior.

- [ ] **Step 5: Convert all moved files to file-scoped namespaces**

Remove namespace braces and reduce only the indentation introduced by those braces. Do not reformat unrelated method bodies.

- [ ] **Step 6: Run pipeline-focused tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2AAdapterTests|FullyQualifiedName~A2AActivityTests|FullyQualifiedName~A2ATurnContextTests|FullyQualifiedName~A2AServiceRegistrationTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Build the extension**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
```

Expected: both target frameworks build with zero errors.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests src\samples\A2A
git commit -m "refactor: organize A2A request pipeline" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 3: Separate authorization and Agent Card APIs

**Files:**
- Move: `A2AUserAuthorization.cs` → `Authorization/A2AUserAuthorization.cs`
- Move: `A2AUserAuthorizationSettings.cs` → `Authorization/A2AUserAuthorizationSettings.cs`
- Move: `A2ARequestAuthentication.cs` → `Authorization/A2ARequestAuthentication.cs`
- Move: `A2AOAuthFlowConfiguration.cs` → `Authorization/A2AOAuthFlowConfiguration.cs`
- Move: `A2AAuthorizationMetadata.cs` → `Authorization/A2AAuthorizationMetadata.cs`
- Move: `A2AAgentCardOptions.cs` → `AgentCard/A2AAgentCardOptions.cs`
- Move: `A2AAgentCardComposer.cs` → `AgentCard/A2AAgentCardComposer.cs`
- Move: `IAgentCardHandler.cs` → `AgentCard/IAgentCardHandler.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Modify: authorization and Agent Card tests
- Modify: A2A library consumers, samples, and configuration tests

**Interfaces:**
- Consumes: Root skill registrations and pipeline agent-card processing.
- Produces: `Microsoft.Agents.Extensions.A2A.Authorization` and `Microsoft.Agents.Extensions.A2A.AgentCard`.

- [ ] **Step 1: Add failing public namespace cases**

Add:

```csharp
[InlineData("A2AUserAuthorization", RootNamespace + ".Authorization")]
[InlineData("A2AUserAuthorizationSettings", RootNamespace + ".Authorization")]
[InlineData("A2AAgentCardOptions", RootNamespace + ".AgentCard")]
[InlineData("IAgentCardHandler", RootNamespace + ".AgentCard")]
```

- [ ] **Step 2: Run focused test and verify RED**

Run the namespace test. Expected: all four rows fail.

- [ ] **Step 3: Move authorization files**

Use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Authorization;
```

Keep request-token extraction, OBO behavior, settings binding, and handler metadata behavior unchanged. Update configuration tests and assembly/type-name comparisons to use the new full type name while continuing to accept the simple `"A2AUserAuthorization"` configuration discriminator.

- [ ] **Step 4: Move Agent Card files**

Use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.AgentCard;
```

Update `Pipeline.A2AAdapter`, authorization metadata, DI/configuration binding, tests, and samples. Preserve composition order and final `IAgentCardHandler` behavior.

- [ ] **Step 5: Convert moved files to file-scoped namespaces**

`A2AUserAuthorization.cs` must change from a block namespace to the file-scoped authorization namespace. Remove only the namespace wrapper indentation.

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2AUserAuthorization|FullyQualifiedName~A2ARequestAuthenticationTests|FullyQualifiedName~A2AAdapterTests"
```

Expected: all selected tests pass.

- [ ] **Step 7: Run the full extension suite**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj
```

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests src\samples\A2A
git commit -m "refactor: separate A2A authorization and agent card" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 4: Move client, storage, and serialization infrastructure

**Files:**
- Move: `A2AClient.cs` → `Client/A2AClient.cs`
- Move: `BlobTaskStore.cs` → `Storage/BlobTaskStore.cs`
- Move: `SerializationInit.cs` → `Serialization/SerializationInit.cs`
- Modify: `Errors/ErrorHelper.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Modify: client/storage/serialization consumers and tests
- Modify: A2A samples

**Interfaces:**
- Consumes: Pipeline turn services and upstream `ITaskStore`.
- Produces: `Microsoft.Agents.Extensions.A2A.Client.A2AClient`, `Microsoft.Agents.Extensions.A2A.Storage.BlobTaskStore`, and internal serialization initialization.

- [ ] **Step 1: Add failing namespace cases and exact root-surface assertion**

Add:

```csharp
[InlineData("A2AClient", RootNamespace + ".Client")]
[InlineData("BlobTaskStore", RootNamespace + ".Storage")]
```

Add this test:

```csharp
[Fact]
public void ExportedRootTypes_AreTheApprovedAuthoringSurface()
{
    string[] expected =
    [
        "A2AAgentExtension",
        "A2AAgentTransportProtocol",
        "A2AExtensionAttribute",
        "A2AExtensions",
        "A2AMessageRouteAttribute",
        "A2ARouteHandler",
        "A2AServiceExtensions",
        "A2ASkillAttribute",
        "A2ASkillBuilder",
        "IA2AActivity",
        "IA2ATurnContext",
    ];

    string[] actual = typeof(A2AAgentExtension).Assembly
        .GetExportedTypes()
        .Where(type => type.Namespace == RootNamespace)
        .Select(type => type.Name)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
}
```

- [ ] **Step 2: Run namespace tests and verify RED**

Expected: `A2AClient` and `BlobTaskStore` fail their namespace rows, and the exact root-surface assertion reports the remaining infrastructure types.

- [ ] **Step 3: Move client and storage**

Use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Client;
```

and:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Storage;
```

Update `IA2ATurnContext.Client`, concrete turn-context construction, tests, samples, and storage registration references. Do not change `A2AClient` construction lifetime or `BlobTaskStore` behavior.

- [ ] **Step 4: Move serialization initialization**

Move `SerializationInit.cs` to `Serialization` with:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Serialization;
```

Keep `A2AExtensions` in the root namespace. Do not split or rename its public extension methods.

- [ ] **Step 5: Convert Errors to file-scoped syntax**

Keep `ErrorHelper` in `Microsoft.Agents.Extensions.A2A.Errors`, but convert `Errors/ErrorHelper.cs` to:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Errors;
```

Do not edit generated resource code.

- [ ] **Step 6: Run namespace, client, and storage tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~BlobTaskStoreTests|FullyQualifiedName~A2ATurnContextTests|FullyQualifiedName~A2AExtensionsTests"
```

Expected: all selected tests pass and the exact root surface matches.

- [ ] **Step 7: Run both A2A suites**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
```

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests src\samples\A2A
git commit -m "refactor: organize A2A supporting APIs" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 5: Complete and enforce public XML documentation

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Microsoft.Agents.Extensions.A2A.csproj`
- Modify: every file containing a public or protected API under `Microsoft.Agents.Extensions.A2A`
- Exclude: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Properties/Resources.Designer.cs`

**Interfaces:**
- Consumes: Final namespace layout from Tasks 1-4.
- Produces: Complete generated XML documentation and project-local compiler enforcement for missing/malformed public docs.

- [ ] **Step 1: Enable documentation diagnostics for this project**

After the existing nullable property group, add:

```xml
<PropertyGroup>
  <NoWarn>$([System.String]::Copy('$(NoWarn)').Replace('CS1591', '').Replace('CS1572', '').Replace('CS1574', ''))</NoWarn>
</PropertyGroup>
```

This removes only the repository-level suppressions for missing public documentation, mismatched parameter documentation, and unresolved XML references in this project.

- [ ] **Step 2: Build and verify RED**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
```

Expected: build fails with `CS1591`, `CS1572`, and/or `CS1574` diagnostics identifying undocumented or malformed public API documentation.

- [ ] **Step 3: Document public root authoring APIs**

Complete type/member docs in:

- `A2AAgentExtension.cs`
- `A2AAgentTransportProtocol.cs`
- `A2AExtensionAttribute.cs`
- `A2AExtensions.cs`
- `A2AMessageRouteAttribute.cs`
- `A2ARouteHandler.cs`
- `A2AServiceExtensions.cs`
- `A2ASkillAttribute.cs`
- `A2ASkillBuilder.cs`
- `IA2AActivity.cs`
- `IA2ATurnContext.cs`

Every constructor, method, property, public constant, delegate parameter, return value, and declared exception must have meaningful documentation. Replace empty `<param>` elements. Keep examples accurate for the new namespaces.

- [ ] **Step 4: Document public subnamespace APIs**

Complete type/member docs in:

- `Pipeline/A2AAdapter.cs`
- `Pipeline/IA2AHttpAdapter.cs`
- `Pipeline/A2AJsonRpcProcessor.cs`
- `Pipeline/A2AMessageActivity.cs`
- `Pipeline/A2ATurnContext.cs`
- `Authorization/A2AUserAuthorization.cs`
- `Authorization/A2AUserAuthorizationSettings.cs`
- `AgentCard/A2AAgentCardOptions.cs`
- `AgentCard/IAgentCardHandler.cs`
- `Client/A2AClient.cs`
- `Storage/BlobTaskStore.cs`
- `Hosting/A2AServiceRegistrar.cs`

Use `<inheritdoc/>` for complete interface implementations such as `ITaskStore` methods. Add explicit remarks where A2A behavior differs from a base contract, especially request-token consumption, OBO separation, Agent Card customization, and same-turn client access.

- [ ] **Step 5: Fix malformed and stale references**

Update `cref` values to new namespaces or imported type names. Remove references to obsolete namespaces such as `Microsoft.Agents.Hosting.A2A.Protocol.AgentCard`. Ensure every `<param name="...">` matches an actual parameter.

- [ ] **Step 6: Build until documentation diagnostics are clean**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
```

Expected: both target frameworks build with zero warnings and zero errors.

- [ ] **Step 7: Audit file-scoped namespaces**

Run:

```powershell
Get-ChildItem src\libraries\Extensions\Microsoft.Agents.Extensions.A2A -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notlike '*\Properties\Resources.Designer.cs' } |
  Select-String -Pattern '^namespace\s+[^;]+$|^namespace\s+[^{]+\{' 
```

Expected: no output.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A
git commit -m "docs: complete A2A public API documentation" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 6: Update consumer documentation and verify the cleanup

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/README.md`
- Modify: `src/samples/A2A/README.md`
- Modify: `src/samples/A2A/A2AAgent/README.md`
- Modify: `src/samples/A2A/A2AClient/README.md`
- Modify: `src/samples/A2A/A2ATCKAgent/README.md` when it references moved public types
- Verify: all files changed in Tasks 1-5

**Interfaces:**
- Consumes: Final namespaces and public docs.
- Produces: Updated import examples and a fully verified cleanup branch.

- [ ] **Step 1: Update namespace examples**

Use one root import for normal authoring:

```csharp
using Microsoft.Agents.Extensions.A2A;
```

Add responsibility-specific imports only when the example explicitly uses those APIs:

```csharp
using Microsoft.Agents.Extensions.A2A.AgentCard;
using Microsoft.Agents.Extensions.A2A.Authorization;
using Microsoft.Agents.Extensions.A2A.Pipeline;
using Microsoft.Agents.Extensions.A2A.Storage;
```

Do not add all subnamespace imports to every example.

- [ ] **Step 2: Document the organization briefly**

In the package README, add a concise API organization table covering root authoring, Pipeline, Authorization, AgentCard, Client, and Storage. State that the package is preview and these namespace changes are breaking.

- [ ] **Step 3: Run extension tests**

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 4: Run sample tests**

```powershell
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 5: Build the extension and samples**

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj
dotnet build src\samples\A2A\A2AClient\A2AClient.csproj
dotnet build src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj
```

Expected: every build succeeds with zero warnings and zero errors.

- [ ] **Step 6: Verify namespace contract and file layout**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests"
Get-ChildItem src\libraries\Extensions\Microsoft.Agents.Extensions.A2A -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notlike '*\Properties\Resources.Designer.cs' } |
  Select-String -Pattern '^namespace\s+[^;]+$|^namespace\s+[^{]+\{'
```

Expected: namespace tests pass and the source audit emits no block-scoped namespace declarations.

- [ ] **Step 7: Inspect the final diff**

Run:

```powershell
git --no-pager diff --check 5db8ac09..HEAD
git --no-pager status --short
git --no-pager diff --name-status 5db8ac09..HEAD
```

Expected: no whitespace errors; only the A2A extension, its tests, A2A samples, and directly related README files are changed.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests src\samples\A2A
git commit -m "docs: update A2A namespace examples" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```
