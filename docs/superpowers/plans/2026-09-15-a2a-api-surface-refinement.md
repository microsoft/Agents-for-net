# A2A API Surface Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align A2A integration infrastructure with the MSTeams layout, remove accidental public pipeline APIs, and give surfaced extension failures stable Agent SDK error metadata.

**Architecture:** Move Agent SDK coupling into `.Integration`, return the same-turn `A2AClient` to the root authoring namespace, and keep the pipeline behind root startup extensions. Preserve standard argument exceptions and A2A wire exceptions, while converting extension-owned configuration, registration, lifecycle, and translated storage failures to `ExceptionHelper.GenerateException<T>`.

**Tech Stack:** C# 13, .NET 8 and .NET 10, ASP.NET Core endpoint routing, Microsoft Agents SDK extension registration, System.Text.Json, xUnit, Moq, Agent SDK `AgentErrorDefinition`/`ExceptionHelper`.

**Spec:** `docs/superpowers/specs/2026-09-15-a2a-namespace-cleanup-design.md`

## Global Constraints

- The package is preview; make the namespace and accessibility changes directly without compatibility wrappers or type forwarding.
- Normal agent authoring and startup use `Microsoft.Agents.Extensions.A2A`; responsibility-specific imports are required only for Authorization, AgentCard, and Storage APIs.
- `A2AServiceExtensions` and `A2AExtensions` remain public at root.
- `A2AServiceRegistrar` remains public because `AgentServiceRegistrationAttribute` requires a public concrete registrar with a public parameterless constructor.
- The transport pipeline is not a supported replacement or direct-construction extension point.
- Public argument validation remains standard `ArgumentNullException`/`ArgumentException`.
- A2A request and protocol failures remain `A2AException` with `A2AErrorCode`.
- Surfaced extension-owned configuration, registration, lifecycle, internal-state, and translated storage failures use localized `ErrorHelper` definitions and `ExceptionHelper.GenerateException<T>`.
- Translated external failures preserve the original exception as `InnerException`.
- Do not edit `Properties/Resources.Designer.cs`; retrieve new resource strings through its existing `ResourceManager`.
- All hand-authored C# files use file-scoped namespaces.
- Public/protected XML documentation diagnostics remain enabled for the A2A project.

---

### Task 1: Consolidate Agent SDK integration and root client access

**Files:**
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Move: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Hosting/A2AServiceRegistrar.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Integration/A2AServiceRegistrar.cs`
- Move: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Serialization/SerializationInit.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Integration/SerializationInit.cs`
- Move: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Client/A2AClient.cs` → `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AClient.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AssemblyInfo.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AServiceExtensions.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/IA2ATurnContext.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2ATurnContext.cs`

**Interfaces:**
- Consumes: Existing `IA2ATurnContext.Client` contract and Agent SDK assembly registration attributes.
- Produces: Root `Microsoft.Agents.Extensions.A2A.A2AClient`; `.Integration.A2AServiceRegistrar`; `.Integration.SerializationInit`.

- [ ] **Step 1: Update namespace contract tests**

Change the namespace rows to:

```csharp
[InlineData("A2AServiceRegistrar", RootNamespace + ".Integration")]
[InlineData("SerializationInit", RootNamespace + ".Integration")]
[InlineData("A2AClient", RootNamespace)]
```

Add `typeof(A2AClient)` to `AuthoringType_RemainsInRootNamespace` and add `"A2AClient"` to the exact root exported-type array.

- [ ] **Step 2: Run the namespace tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests"
```

Expected: failures report the old `.Hosting`, `.Serialization`, and `.Client` namespaces and the missing root `A2AClient`.

- [ ] **Step 3: Move integration infrastructure**

Use:

```csharp
namespace Microsoft.Agents.Extensions.A2A.Integration;
```

for both `A2AServiceRegistrar` and `SerializationInit`.

Update the assembly references to:

```csharp
[assembly: Microsoft.Agents.Core.Serialization.SerializationInitAssembly(
    typeof(Microsoft.Agents.Extensions.A2A.Integration.SerializationInit))]

[assembly: Microsoft.Agents.Builder.AgentServiceRegistrationAttribute(
    typeof(Microsoft.Agents.Extensions.A2A.Integration.A2AServiceRegistrar))]
```

Do not change either class's behavior or accessibility.

- [ ] **Step 4: Move `A2AClient` to root**

Change its namespace to:

```csharp
namespace Microsoft.Agents.Extensions.A2A;
```

Remove `.Client` imports from `IA2ATurnContext` and `Pipeline/A2ATurnContext`. Keep the type name, constructor accessibility, `IA2ATurnContext.Client` property name, per-access construction, and three exposed properties unchanged.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2ATurnContextTests|FullyQualifiedName~A2AServiceRegistrationTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Build the extension**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
```

Expected: net8.0 and net10.0 succeed with zero warnings and errors.

- [ ] **Step 7: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests\Microsoft.Agents.Extensions.A2A.Tests
git commit -m "refactor: consolidate A2A integration infrastructure" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 2: Internalize the transport implementation

**Files:**
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2ANamespaceOrganizationTests.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AssemblyInfo.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AAdapter.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/IA2AHttpAdapter.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AJsonRpcProcessor.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AMessageActivity.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2ATurnContext.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AgentCard/A2AAgentCardOptions.cs`
- Verify/modify: `src/tests/Microsoft.Agents.Samples.A2A.Tests/A2AAgentOAuthRouteTests.cs`

**Interfaces:**
- Consumes: Public root `A2AServiceExtensions`, `IA2AActivity`, and `IA2ATurnContext`.
- Produces: A public API limited to direct authoring/configuration contracts; the transport remains registered and callable only through startup endpoints.

- [ ] **Step 1: Add exact exported-surface tests**

Add:

```csharp
[Fact]
public void ExportedTypes_AreTheApprovedPublicSurface()
{
    string[] expected =
    [
        "Microsoft.Agents.Extensions.A2A.A2AAgentExtension",
        "Microsoft.Agents.Extensions.A2A.A2AAgentTransportProtocol",
        "Microsoft.Agents.Extensions.A2A.A2AClient",
        "Microsoft.Agents.Extensions.A2A.A2AExtensionAttribute",
        "Microsoft.Agents.Extensions.A2A.A2AExtensions",
        "Microsoft.Agents.Extensions.A2A.A2AMessageRouteAttribute",
        "Microsoft.Agents.Extensions.A2A.A2ARouteHandler",
        "Microsoft.Agents.Extensions.A2A.A2AServiceExtensions",
        "Microsoft.Agents.Extensions.A2A.A2ASkillAttribute",
        "Microsoft.Agents.Extensions.A2A.A2ASkillBuilder",
        "Microsoft.Agents.Extensions.A2A.IA2AActivity",
        "Microsoft.Agents.Extensions.A2A.IA2ATurnContext",
        "Microsoft.Agents.Extensions.A2A.AgentCard.IAgentCardHandler",
        "Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorization",
        "Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationSettings",
        "Microsoft.Agents.Extensions.A2A.Integration.A2AServiceRegistrar",
        "Microsoft.Agents.Extensions.A2A.Storage.BlobTaskStore",
    ];

    string[] actual = typeof(A2AAgentExtension).Assembly
        .GetExportedTypes()
        .Select(type => type.FullName!)
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray();

    Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
}
```

Also add a theory asserting these types are not exported:

```csharp
[Theory]
[InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AAdapter")]
[InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.IA2AHttpAdapter")]
[InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AJsonRpcProcessor")]
[InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AMessageActivity")]
[InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2ATurnContext")]
[InlineData("Microsoft.Agents.Extensions.A2A.AgentCard.A2AAgentCardOptions")]
public void ImplementationType_IsNotExported(string fullName)
{
    Assert.DoesNotContain(
        typeof(A2AAgentExtension).Assembly.GetExportedTypes(),
        type => type.FullName == fullName);
}
```

- [ ] **Step 2: Run the surface tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests"
```

Expected: the six current public implementation types appear in the exported surface.

- [ ] **Step 3: Internalize implementation types**

Change these declarations:

```csharp
internal class A2AAdapter : ChannelAdapter, IA2AHttpAdapter
internal interface IA2AHttpAdapter : IAgentHttpAdapter
internal static class A2AJsonRpcProcessor
internal class A2AMessageActivity : Activity, IA2AActivity
internal class A2ATurnContext : TurnContextWrapper, IA2ATurnContext
internal sealed class A2AAgentCardOptions
```

Keep constructors and members at their current accessibility unless the compiler requires reducing a member that is more visible than its containing type. Do not change DI registration, endpoint behavior, channel adapter attributes, or serialization attributes.

- [ ] **Step 4: Grant sample tests internal access**

Add this assembly attribute using the same public key as the existing A2A test friend assembly:

```csharp
[assembly: InternalsVisibleTo(
    "Microsoft.Agents.Samples.A2A.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100b5fc90e7027f67871e773a8fde8938c81dd402ba65b9201d60593e96c492651e889cc13f1415ebb53fac1131ae0bd333c5ee6021672d9718ea31a8aebd0da0072f25d87dba6fc90ffd598ed4da35e44c398c454307e8e33b8426143daec9f596836f97c8f74750e5975c64e2189f45def46b2a2b1247adc3652bf5c308055da9")]
```

This keeps `A2AAgentOAuthRouteTests` compiling without making the adapter public for test convenience.

- [ ] **Step 5: Run registration, serialization, and sample tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2AServiceRegistrationTests|FullyQualifiedName~AgentApplicationAttributesTests|FullyQualifiedName~A2ATurnContextTests"
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj --filter "FullyQualifiedName~A2AAgentOAuthRouteTests"
```

Expected: automatic service registration, typed activity conversion, route delivery, and sample OAuth route tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests
git commit -m "refactor: internalize A2A transport pipeline" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 3: Add stable error definitions for authoring and startup failures

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Errors/ErrorHelper.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Properties/Resources.resx`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AAgentExtension.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2ASkillBuilder.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AServiceExtensions.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/AgentApplicationAttributesTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AServiceRegistrationTests.cs`
- Create: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AErrorMetadataTests.cs`

**Interfaces:**
- Consumes: `Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException<T>`.
- Produces: Stable A2A error codes `-100002` through `-100006` for authoring/startup failures.

- [ ] **Step 1: Add failing metadata assertions**

Add this helper to `A2AErrorMetadataTests`:

```csharp
private static void AssertErrorMetadata(Exception exception, int code)
{
    Assert.Equal(code, exception.HResult);
    Assert.Equal($"https://aka.ms/M365AgentsErrorCodes/#{code}", exception.HelpLink);
}
```

Cover these existing failure paths:

| Code | Failure | Trigger |
|---|---|---|
| `-100002` | Conflicting skill metadata | Construct the existing conflicting attributed agent |
| `-100003` | Missing message route | `new A2ASkillBuilder("id").Build()` |
| `-100004` | More than one route | Call two `OnMessage` overloads on one builder |
| `-100005` | No AgentApplication | Call the new internal `ResolveAgentTypes(typeof(string).Assembly, emptyServiceProvider)` helper |
| `-100006` | Missing `AgentInterfaceAttribute` | Call the new internal `ResolveAgentInterfaces(typeof(UnattributedAgent), 2, "/a2a")` helper |

The A2A test assembly already has `InternalsVisibleTo`, so tests call the internal builder constructor/methods and resolver helpers directly.

Use this test-only agent for the missing-interface case:

```csharp
private sealed class UnattributedAgent : AgentApplication
{
    public UnattributedAgent()
        : base(new AgentApplicationOptions((IStorage)null))
    {
    }
}
```

- [ ] **Step 2: Run the new tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2AErrorMetadataTests|FullyQualifiedName~AgentApplicationAttributesTests|FullyQualifiedName~A2AServiceRegistrationTests"
```

Expected: current exceptions have default `HResult` and no A2A help link.

- [ ] **Step 3: Add localized resources**

Add these exact resource keys and values to `Properties/Resources.resx`:

```xml
<data name="ConflictingSkillMetadata" xml:space="preserve">
  <value>A2A skill '{0}' has conflicting metadata.</value>
</data>
<data name="SkillRouteMissing" xml:space="preserve">
  <value>A2A skill '{0}' must define a message route.</value>
</data>
<data name="SkillRouteAlreadyDefined" xml:space="preserve">
  <value>A2A skill '{0}' can define only one message route.</value>
</data>
<data name="AgentApplicationNotFound" xml:space="preserve">
  <value>No AgentApplication was found. Define an AgentApplication in the calling assembly or register one as IAgent.</value>
</data>
<data name="AgentInterfaceMissing" xml:space="preserve">
  <value>Agent '{0}' must declare an AgentInterfaceAttribute when multiple AgentApplications are present.</value>
</data>
```

Do not edit `Resources.Designer.cs`.

- [ ] **Step 4: Replace copied `ErrorHelper` documentation and add definitions**

Replace the class comment with:

```csharp
/// <summary>
/// Defines localized Agent SDK error metadata for failures owned by the A2A extension.
/// </summary>
```

Add:

```csharp
private const string HelpLinkBase = "https://aka.ms/M365AgentsErrorCodes/#";

private static string Resource(string name) =>
    Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;

internal static readonly AgentErrorDefinition ConflictingSkillMetadata =
    new(-100002, Resource(nameof(ConflictingSkillMetadata)), $"{HelpLinkBase}-100002");
internal static readonly AgentErrorDefinition SkillRouteMissing =
    new(-100003, Resource(nameof(SkillRouteMissing)), $"{HelpLinkBase}-100003");
internal static readonly AgentErrorDefinition SkillRouteAlreadyDefined =
    new(-100004, Resource(nameof(SkillRouteAlreadyDefined)), $"{HelpLinkBase}-100004");
internal static readonly AgentErrorDefinition AgentApplicationNotFound =
    new(-100005, Resource(nameof(AgentApplicationNotFound)), $"{HelpLinkBase}-100005");
internal static readonly AgentErrorDefinition AgentInterfaceMissing =
    new(-100006, Resource(nameof(AgentInterfaceMissing)), $"{HelpLinkBase}-100006");
```

Refactor the two existing definitions to use `HelpLinkBase`; do not change their codes or messages.

- [ ] **Step 5: Convert authoring and startup throws**

Use:

```csharp
throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
    ErrorHelper.ConflictingSkillMetadata, null, group.Key);
```

Use `SkillRouteMissing` with `_id`, `SkillRouteAlreadyDefined` with `_id`, `AgentApplicationNotFound` without formatting arguments, and `AgentInterfaceMissing` with `agent.FullName`.

Do not convert argument guards.

- [ ] **Step 6: Extract testable endpoint-discovery helpers**

In `A2AServiceExtensions`, extract:

```csharp
internal static List<Type> ResolveAgentTypes(Assembly callingAssembly, IServiceProvider serviceProvider)
{
    var agents = callingAssembly.GetTypes()
        .Where(type => typeof(AgentApplication).IsAssignableFrom(type))
        .ToList();

    if (agents.Count == 0)
    {
        var inlineAgent = serviceProvider.GetService<IAgent>()
            ?? throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                ErrorHelper.AgentApplicationNotFound,
                null);
        agents.Add(inlineAgent.GetType());
    }

    return agents;
}

internal static List<AgentInterfaceAttribute> ResolveAgentInterfaces(
    Type agent,
    int agentCount,
    string defaultPath)
{
    var interfaces = agent.GetCustomAttributes<AgentInterfaceAttribute>(true).ToList();
    if (interfaces.Count == 0 && agentCount == 1)
    {
        interfaces.Add(new AgentInterfaceAttribute(A2AAgentTransportProtocol.JsonRpc, defaultPath));
    }
    else if (interfaces.Count == 0)
    {
        throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
            ErrorHelper.AgentInterfaceMissing,
            null,
            agent.FullName);
    }

    return interfaces;
}
```

Change `MapA2AApplicationEndpoints` to call these helpers with `Assembly.GetCallingAssembly()`, `endpoints.ServiceProvider`, and `allAgents.Count`. Preserve endpoint ordering and default-interface behavior.

- [ ] **Step 7: Run focused tests**

Run the Step 2 command again.

Expected: all selected tests pass and assert the exact `HResult`/`HelpLink`.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests\Microsoft.Agents.Extensions.A2A.Tests
git commit -m "refactor: standardize A2A authoring errors" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 4: Standardize authorization and Agent Card configuration errors

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Errors/ErrorHelper.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Properties/Resources.resx`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2AUserAuthorizationSettings.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AgentCard/A2AAgentCardComposer.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AUserAuthorizationConfigurationTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AAdapterTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AErrorMetadataTests.cs`

**Interfaces:**
- Consumes: Error infrastructure from Task 3.
- Produces: Stable A2A error codes `-100007` through `-100017` for authorization and Agent Card configuration.

- [ ] **Step 1: Add failing error-metadata tests**

Cover each raw `InvalidOperationException` branch with the existing configuration/card fixtures and assert:

| Code | Resource key | Formatting arguments |
|---|---|---|
| `-100007` | `AuthorizationSecuritySchemeNameRequired` | none |
| `-100008` | `AuthorizationExactlyOneOAuthFlowRequired` | none |
| `-100009` | `AuthorizationSecuritySchemeConflict` | none |
| `-100010` | `AgentCardNullSecurityScheme` | scheme name |
| `-100011` | `AgentCardDuplicateSecurityScheme` | scheme name |
| `-100012` | `AgentCardConflictingSkillRegistration` | skill id |
| `-100013` | `AgentCardUnknownAuthorizationHandler` | context, handler name |
| `-100014` | `AgentCardAuthorizationMetadataRequired` | handler name, context |
| `-100015` | `AgentCardProtectedProperty` | property name |
| `-100016` | `AgentCardMissingSecurityScheme` | scheme name |
| `-100017` | `AgentCardUndefinedScope` | scheme name |

For every case, assert exception type, message-specific argument text, exact `HResult`, and exact help link.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2AUserAuthorizationConfigurationTests|FullyQualifiedName~A2AAdapterTests|FullyQualifiedName~A2AErrorMetadataTests"
```

Expected: the new metadata assertions fail against raw `InvalidOperationException`.

- [ ] **Step 3: Add exact localized messages**

Add resource entries:

```text
AuthorizationSecuritySchemeNameRequired = SecuritySchemeName is required when OAuthFlows is configured.
AuthorizationExactlyOneOAuthFlowRequired = OAuthFlows must specify exactly one OAuth flow.
AuthorizationSecuritySchemeConflict = SecurityScheme and OAuthFlows cannot both be configured.
AgentCardNullSecurityScheme = A2A Agent Card security scheme '{0}' cannot be null.
AgentCardDuplicateSecurityScheme = A2A Agent Card security scheme '{0}' is configured more than once.
AgentCardConflictingSkillRegistration = A2A skill '{0}' has conflicting registrations.
AgentCardUnknownAuthorizationHandler = A2A {0} references authorization handler '{1}', which must be configured as an A2AUserAuthorization handler.
AgentCardAuthorizationMetadataRequired = A2A authorization handler '{0}' used by {1} requires Agent Card security metadata.
AgentCardProtectedProperty = A2A Agent Card configuration cannot set protected property '{0}'.
AgentCardMissingSecurityScheme = A2A Agent Card requirement references missing security scheme '{0}'.
AgentCardUndefinedScope = A2A Agent Card requirement for scheme '{0}' includes a scope that the scheme does not define.
```

Add corresponding `AgentErrorDefinition` fields with codes `-100007` through `-100017`.

- [ ] **Step 4: Convert authorization settings throws**

Replace the three raw `InvalidOperationException` statements in `A2AUserAuthorizationSettings.Validate` with `ExceptionHelper.GenerateException<InvalidOperationException>` and the matching definitions. Preserve validation order.

- [ ] **Step 5: Convert Agent Card composition throws**

Replace all eight raw `InvalidOperationException` statements in `A2AAgentCardComposer` with the matching definitions. Preserve the existing context and handler-name normalization behavior. For `AgentCardAuthorizationMetadataRequired`, keep the current actionable property names in the XML documentation or surrounding diagnostics, but use the stable localized exception message above.

- [ ] **Step 6: Run focused tests**

Run the Step 2 command again.

Expected: all selected tests pass.

- [ ] **Step 7: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests\Microsoft.Agents.Extensions.A2A.Tests
git commit -m "refactor: standardize A2A configuration errors" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 5: Standardize surfaced pipeline and storage failures

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Errors/ErrorHelper.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Properties/Resources.resx`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Pipeline/A2AAdapter.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Storage/BlobTaskStore.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AAdapterTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/BlobTaskStoreTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.A2A.Tests/A2AErrorMetadataTests.cs`

**Interfaces:**
- Consumes: Error infrastructure from Task 3.
- Produces: Stable A2A error codes `-100018` and `-100019`, preserving external inner exceptions.

- [ ] **Step 1: Add failing pipeline and storage tests**

Add:

```csharp
[Fact]
public async Task SendActivitiesAsync_WithoutRequestContext_UsesA2AErrorMetadata()
{
    var adapter = new A2AAdapter(new InMemoryTaskStore(), NullLoggerFactory.Instance);
    var activity = new Activity { RequestId = Guid.NewGuid().ToString("N") };
    var turnContext = new TurnContext(adapter, activity);

    InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
        () => adapter.SendActivitiesAsync(turnContext, [], CancellationToken.None));

    AssertErrorMetadata(exception, -100018);
}
```

Add a `BlobTaskStore` test whose mocked upload throws:

```csharp
var storageException = new RequestFailedException(
    (int)HttpStatusCode.BadRequest,
    "invalid block list",
    BlobErrorCode.InvalidBlockList.ToString(),
    null);
```

Assert the translated `InvalidOperationException` has code `-100019`, the A2A help link, and `Assert.Same(storageException, exception.InnerException)`.

Because Azure client upload setup is unrelated to this error-policy test, extract and directly test this internal helper:

```csharp
internal static InvalidOperationException CreateBlobTaskWriteException(RequestFailedException exception)
{
    return Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
        ErrorHelper.BlobTaskWriteConflict,
        exception);
}
```

The existing `InvalidBlockList` catch calls `throw CreateBlobTaskWriteException(ex);`, so the production catch filter and translation remain covered by source structure while the metadata/inner-exception behavior is deterministic.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2AErrorMetadataTests|FullyQualifiedName~A2AAdapterTests|FullyQualifiedName~BlobTaskStoreTests"
```

Expected: current exceptions lack A2A metadata.

- [ ] **Step 3: Add resources and definitions**

Add:

```text
AgentRequestContextMissing = No A2A request context was found for request '{0}'.
BlobTaskWriteConflict = The A2A task could not be written because Azure Blob Storage rejected the block list. Concurrent uploads larger than 128 MB can cause this failure.
```

Add:

```csharp
internal static readonly AgentErrorDefinition AgentRequestContextMissing =
    new(-100018, Resource(nameof(AgentRequestContextMissing)), $"{HelpLinkBase}-100018");
internal static readonly AgentErrorDefinition BlobTaskWriteConflict =
    new(-100019, Resource(nameof(BlobTaskWriteConflict)), $"{HelpLinkBase}-100019");
```

- [ ] **Step 4: Convert the missing-context failure**

Use:

```csharp
throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
    ErrorHelper.AgentRequestContextMissing,
    null,
    turnContext.Activity.RequestId);
```

- [ ] **Step 5: Convert the Blob translation**

Replace the existing translated exception with:

```csharp
throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
    ErrorHelper.BlobTaskWriteConflict,
    ex);
```

Keep the `RequestFailedException` filter unchanged.

- [ ] **Step 6: Run focused tests**

Run the Step 2 command again.

Expected: all selected tests pass and the Blob exception preserves its inner exception.

- [ ] **Step 7: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests\Microsoft.Agents.Extensions.A2A.Tests
git commit -m "refactor: standardize A2A runtime errors" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```

---

### Task 6: Update consumer documentation and verify the refined surface

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/README.md`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AClient.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/IA2ATurnContext.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/A2AServiceExtensions.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Integration/A2AServiceRegistrar.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Integration/SerializationInit.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Authorization/A2AUserAuthorizationSettings.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/AgentCard/IAgentCardHandler.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.A2A/Storage/BlobTaskStore.cs`
- Verify: all files changed in Tasks 1-5

**Interfaces:**
- Consumes: Final namespace, accessibility, and error contracts.
- Produces: Accurate consumer documentation and a fully verified branch.

- [ ] **Step 1: Update the API organization table**

Change the package README table so:

- root includes `A2AClient`;
- `Client`, `Hosting`, and `Serialization` rows are removed;
- `Integration` explains required Agent SDK registration and serialization initialization infrastructure;
- `Pipeline` is labeled internal implementation rather than a consumer API area;
- AgentCard lists `IAgentCardHandler` as public and configuration binding as internal.

Do not tell consumers to import `.Integration` or `.Pipeline`.

- [ ] **Step 2: Update XML references**

Replace stale `.Client`, `.Hosting`, and `.Serialization` `cref` or namespace text in the files listed above. In the README, remove imports for deleted namespaces and show `A2AClient` as part of the root handler surface. Remove public-surface wording from newly internal pipeline types where it claims consumers can instantiate or replace them; retain comments that explain internal invariants.

Update exception documentation on public methods to name the surfaced `InvalidOperationException` conditions after conversion.

- [ ] **Step 3: Run both complete A2A suites**

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj
dotnet test src\tests\Microsoft.Agents.Samples.A2A.Tests\Microsoft.Agents.Samples.A2A.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 4: Build the extension and samples**

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.A2A\Microsoft.Agents.Extensions.A2A.csproj
dotnet build src\samples\A2A\A2AAgent\A2AAgent.csproj
dotnet build src\samples\A2A\A2AClient\A2AClient.csproj
dotnet build src\samples\A2A\A2ATCKAgent\A2ATCKAgent.csproj
```

Expected: every build succeeds with zero warnings and errors.

- [ ] **Step 5: Verify public surface and namespaces**

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.A2A.Tests\Microsoft.Agents.Extensions.A2A.Tests.csproj --filter "FullyQualifiedName~A2ANamespaceOrganizationTests|FullyQualifiedName~A2AErrorMetadataTests"

Get-ChildItem src\libraries\Extensions\Microsoft.Agents.Extensions.A2A -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notlike '*\Properties\Resources.Designer.cs' } |
  Select-String -Pattern '^namespace\s+[^;]+$|^namespace\s+[^{]+\{'
```

Expected: contract tests pass and the namespace audit emits no output.

- [ ] **Step 6: Verify exception categories**

Run:

```powershell
rg "throw new InvalidOperationException" src\libraries\Extensions\Microsoft.Agents.Extensions.A2A
rg "ExceptionHelper\.GenerateException" src\libraries\Extensions\Microsoft.Agents.Extensions.A2A
rg "throw new A2AException" src\libraries\Extensions\Microsoft.Agents.Extensions.A2A
```

Expected:

- no extension-owned raw `InvalidOperationException` remains in the audited files;
- argument guards remain standard;
- protocol errors remain `A2AException`;
- every translated external exception passes the original exception to `GenerateException`.

- [ ] **Step 7: Inspect final changes**

```powershell
git --no-pager diff --check beaf0423..HEAD
git --no-pager status --short
git --no-pager diff --name-status beaf0423..HEAD
```

Expected: diff check is clean and only planned A2A library, test, sample, and README files changed.

- [ ] **Step 8: Commit**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.A2A src\tests src\samples\A2A
git commit -m "docs: describe refined A2A API surface" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 9f8152f9-4608-4106-afe9-6f6fa352aee8"
```
