# A2A Namespace and Public API Cleanup Design

## Goal

Organize `Microsoft.Agents.Extensions.A2A` around clear responsibilities while keeping the normal agent-authoring experience to one root namespace import. Convert hand-authored files to file-scoped namespaces and add complete XML documentation for the public API.

The package is preview. This cleanup may make breaking namespace changes and will not retain compatibility wrappers for the previous namespace layout.

## Design principles

1. Keep APIs used in normal agent code and startup in `Microsoft.Agents.Extensions.A2A`.
2. Move implementation, advanced configuration, client, and persistence APIs into responsibility-specific subnamespaces.
3. Match namespace boundaries with physical folders.
4. Keep generated source generator and resource files generator-owned.
5. Document the complete public and protected API surface, not only the OAuth POC additions.

## Namespace and folder layout

### Root authoring surface

The root `Microsoft.Agents.Extensions.A2A` namespace contains:

- `A2AAgentExtension`
- `A2AExtensionAttribute`
- `IA2ATurnContext`
- `IA2AActivity`
- `A2ARouteHandler`
- `A2ASkillAttribute`
- `A2ASkillBuilder`
- `A2AMessageRouteAttribute`
- `A2AAgentTransportProtocol`
- `A2AServiceExtensions`
- `A2AExtensions`

These types are used directly in agent classes, attributed route methods, fluent route registration, message and metadata helpers, `AgentInterface` declarations, or application startup. Keeping them together allows the typical application to use one `using Microsoft.Agents.Extensions.A2A;`.

`A2ARouteHandler` remains at root for API coherence even though most consumers pass method groups or lambdas rather than declaring variables of that delegate type.

### Pipeline

Folder and namespace: `Pipeline` / `Microsoft.Agents.Extensions.A2A.Pipeline`

Contains:

- `A2AAdapter`
- `IA2AHttpAdapter`
- `A2AActivity`
- `A2AMessageActivity`
- `A2ATurnContext`
- `A2AHttpProcessor`
- `A2AJsonRpcProcessor`
- pipeline-specific conversion and dispatch helpers

The public interfaces `IA2AActivity` and `IA2ATurnContext` remain at root because agent handlers consume them directly. Their concrete implementations belong to the pipeline.

### Authorization

Folder and namespace: `Authorization` / `Microsoft.Agents.Extensions.A2A.Authorization`

Contains:

- `A2AUserAuthorization`
- `A2AUserAuthorizationSettings`
- `A2ARequestAuthentication`
- `A2AOAuthFlowConfiguration`
- internal authorization metadata resolution

This namespace owns request-token consumption, OBO configuration, and Agent Card OAuth metadata binding. ASP.NET Core remains responsible for authenticating incoming requests.

### Agent Card

Folder and namespace: `AgentCard` / `Microsoft.Agents.Extensions.A2A.AgentCard`

Contains:

- `A2AAgentCardOptions`
- `IAgentCardHandler`
- internal Agent Card composition

This separates discovery metadata from request execution and authorization runtime behavior.

### Client

Folder and namespace: `Client` / `Microsoft.Agents.Extensions.A2A.Client`

Contains `A2AClient` and any future reusable client-side APIs. Sample-only client code remains under the sample project.

### Storage

Folder and namespace: `Storage` / `Microsoft.Agents.Extensions.A2A.Storage`

Contains `BlobTaskStore` and future A2A task-store implementations.

### Serialization

Folder and namespace: `Serialization` / `Microsoft.Agents.Extensions.A2A.Serialization`

Contains serialization initialization helpers that are not part of the normal authoring surface. The public `A2AExtensions` class remains at root because `NewAgentMessage` is used directly in normal A2A handlers; the class will not be split solely to relocate its lower-level metadata helper.

### Routing internals

Folder and namespace: `Routing` / `Microsoft.Agents.Extensions.A2A.Routing`

Contains internal registration and handler helpers, including:

- `A2ASkillRegistration`
- `HandlerUtils`

Public authoring attributes and builders remain at root to avoid additional imports in agent classes.

### Hosting

Folder and namespace: `Hosting` / `Microsoft.Agents.Extensions.A2A.Hosting`

Contains `A2AServiceRegistrar`. Its existing accessibility will be preserved unless the registration mechanism is proven to support a narrower surface. `A2AServiceExtensions` remains public at root so startup does not require a second A2A namespace import.

### Existing infrastructure

`Errors` and `Properties` retain their current roles. `Properties/Resources.Designer.cs` remains unchanged because it is generated code.

## Namespace syntax

Every hand-authored C# file will use file-scoped namespace syntax:

```csharp
namespace Microsoft.Agents.Extensions.A2A;
```

or its approved subnamespace equivalent. Block-scoped namespaces will not remain in hand-authored files. Generated files are excluded from this rule.

## XML documentation

Every public type and every public or protected member will have XML documentation, including:

- classes, interfaces, delegates, and attributes;
- constructors and methods;
- properties and public constants;
- extension methods;
- method parameters, return values, and declared exceptions.

`<inheritdoc/>` will be used when an inherited interface or base contract completely describes the member. Members with A2A-specific behavior will receive explicit documentation. Empty parameter elements will be replaced with meaningful descriptions.

Internal helpers will be documented only when their responsibility or invariants are not clear from their names and signatures.

## Migration

The namespace changes are intentionally breaking because the package is preview. The implementation will:

- move files and update namespaces atomically;
- update library references and assembly attributes;
- update tests, samples, and README examples;
- avoid type forwarding, compatibility wrappers, or duplicate old namespaces.

## Validation

The cleanup is complete when:

1. All hand-authored A2A extension files use file-scoped namespaces.
2. The public root namespace contains only the approved authoring and startup surface.
3. Subnamespace and folder assignments match this design.
4. Every public/protected type and member has meaningful XML documentation or appropriate `<inheritdoc/>`.
5. Both A2A test projects pass.
6. The A2A extension, A2AAgent sample, and A2AClient sample build without warnings or errors.
7. `git diff --check` reports no errors for the cleanup change.
