# A2A Namespace and Public API Cleanup Design

## Goal

Organize `Microsoft.Agents.Extensions.A2A` around clear responsibilities while keeping the normal agent-authoring experience to one root namespace import. Convert hand-authored files to file-scoped namespaces and add complete XML documentation for the public API.

The package is preview. This cleanup may make breaking namespace changes and will not retain compatibility wrappers for the previous namespace layout.

## Design principles

1. Keep APIs used in normal agent code and startup in `Microsoft.Agents.Extensions.A2A`.
2. Keep direct authoring APIs at root, move public specialty APIs into responsibility-specific subnamespaces, and make transport implementation details internal.
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
- `A2AClient`

These types are used directly in agent classes, attributed route methods, fluent route registration, message and metadata helpers, `AgentInterface` declarations, or application startup. Keeping them together allows the typical application to use one `using Microsoft.Agents.Extensions.A2A;`.

`A2ARouteHandler` remains at root for API coherence even though most consumers pass method groups or lambdas rather than declaring variables of that delegate type.

`A2AClient` also remains at root because it is exposed directly by `IA2ATurnContext.Client`. It is a same-turn accessor for A2A request primitives, not a reusable outbound protocol client. A separate one-type `Client` namespace would add an import without creating a meaningful responsibility boundary.

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

The pipeline is not a supported consumer extension point. `A2AAdapter`, `IA2AHttpAdapter`, `A2AMessageActivity`, `A2ATurnContext`, and `A2AJsonRpcProcessor` are internal. Consumers register and map the pipeline through `A2AServiceExtensions`; they do not directly construct, invoke, or replace the adapter.

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

`A2AAgentCardOptions` is internal because it is the configuration-binding model for `AgentApplication:A2A:AgentCard`, not a registered options contract. `IAgentCardHandler` remains public as the explicit final-card customization escape hatch.

### Storage

Folder and namespace: `Storage` / `Microsoft.Agents.Extensions.A2A.Storage`

Contains `BlobTaskStore` and future A2A task-store implementations.

### Routing internals

Folder and namespace: `Routing` / `Microsoft.Agents.Extensions.A2A.Routing`

Contains internal registration and handler helpers, including:

- `A2ASkillRegistration`
- `HandlerUtils`

Public authoring attributes and builders remain at root to avoid additional imports in agent classes.

### Agent SDK integration

Folder and namespace: `Integration` / `Microsoft.Agents.Extensions.A2A.Integration`

Contains infrastructure that integrates the extension with Agent SDK Core and Builder:

- `A2AServiceRegistrar`
- `SerializationInit`

This matches the `Microsoft.Agents.Extensions.MSTeams.Integration` convention. `A2AServiceRegistrar` remains public because `AgentServiceRegistrationAttribute` requires a public, concrete registrar with a public parameterless constructor. `SerializationInit` remains internal. `A2AServiceExtensions` remains public at root so startup does not require an integration namespace import. The public `A2AExtensions` class also remains at root because `NewAgentMessage` is used directly in normal A2A handlers.

### Existing infrastructure

`Errors` and `Properties` retain their current roles. `Properties/Resources.Designer.cs` remains unchanged because it is generated code.

## Public surface

The public API is limited to contracts that applications configure, implement, or call directly:

- root authoring and startup types listed above;
- `Authorization.A2AUserAuthorization` and `Authorization.A2AUserAuthorizationSettings`;
- `AgentCard.IAgentCardHandler`;
- `Storage.BlobTaskStore`;
- `Integration.A2AServiceRegistrar`, which is public only because the Agent SDK registration contract requires it.

All other A2A extension types are internal unless a concrete consumer scenario demonstrates that direct access is required. Tests use `InternalsVisibleTo` rather than expanding the product API for test convenience.

## Exception policy

Exceptions are divided by audience and protocol role:

1. Public argument validation continues to use standard `ArgumentNullException` and `ArgumentException` guards. These are programmer errors and do not consume Agent SDK error codes.
2. Errors returned to an A2A caller continue to use `A2AException` with the appropriate `A2AErrorCode`. These exceptions are part of the A2A wire contract and are not replaced by Agent SDK error definitions.
3. Extension configuration, registration, lifecycle, and internal-state failures that can escape to an application use `ExceptionHelper.GenerateException<T>` with an A2A `ErrorHelper` definition, localized resource text, stable `HResult`, and help link.
4. External dependency exceptions propagate unchanged unless the extension translates them to add actionable A2A context. A translated exception preserves the original exception as its inner exception and uses an `ErrorHelper` definition.

The implementation audits raw `InvalidOperationException` sites in skill registration, Agent Card composition and settings, endpoint discovery, missing request context, and Blob task persistence. Each surfaced extension-owned failure receives a specific error definition rather than a generic catch-all. `ErrorHelper` documentation and comments describe the A2A extension rather than copied Builder or authentication examples.

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
3. Public specialty types are limited to the approved Authorization, AgentCard, Storage, and required Integration contracts.
4. Pipeline and configuration-binding implementation types are internal.
5. Subnamespace and folder assignments match this design.
6. Surfaced extension-owned failures follow the exception policy and carry stable Agent SDK error metadata.
7. Every public/protected type and member has meaningful XML documentation or appropriate `<inheritdoc/>`.
8. Both A2A test projects pass.
9. The A2A extension, A2AAgent sample, and A2AClient sample build without warnings or errors.
10. `git diff --check` reports no errors for the cleanup change.
