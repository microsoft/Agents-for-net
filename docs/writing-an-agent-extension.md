# Writing an AgentExtension

## Overview

An AgentExtension packages channel-, protocol-, or domain-specific behavior for an Agents SDK
application. An extension can add an agent-facing routing API, strongly typed protocol models,
serialization support, a channel adapter, native clients, and dependency-injection registrations.

The backing philosophy is **"it just works."** Taking a dependency on an AgentExtension package
should be enough to wire up its infrastructure. An application should not need package-specific
calls in `Program.cs` to:

- register `System.Text.Json` converters or type metadata;
- register custom `Entity` or `Activity` types;
- add a channel adapter to the adapter registry;
- register the extension's internal services or adapter aliases.

The application still makes intentional product choices. For example, it opts into an
agent-facing extension API by annotating its `partial AgentApplication` class, maps any
protocol-specific HTTP endpoints, and supplies required configuration or credentials. Those are
application concerns; the package's internal wiring is not.

An extension can use any combination of these extensibility points:

| Extensibility point | Purpose | Automatic discovery mechanism |
|---|---|---|
| Route builders and route attributes | Add channel- or feature-specific route APIs | Explicit route registration through helpers or `IRouteAttribute` discovery |
| Custom entities | Add polymorphic objects to `Activity.Entities` | `Entity` subclass and optional `[EntityName]` |
| Custom activities | Select a protocol-specific `Activity` subtype from wire discriminators | `[ActivityType]` |
| Serialization customization | Add converters or `JsonTypeInfoResolver` instances | `[SerializationInit]` |
| Host endpoint mapping | Expose host- or channel-specific HTTP endpoints | Explicit mapping in `Program.cs` |
| Custom adapters | Translate a native protocol to and from the Activity Protocol | `[ChannelAdapter]` |
| Custom DI | Register extension services and adapter aliases | `IAgentServiceRegistrar` and `[assembly: AgentServiceRegistration]` |
| Custom access-token providers | Acquire application/service tokens for named connections | `IAccessTokenProvider` selected by `Connections` configuration |
| Custom user authorization | Implement end-user sign-in, refresh, sign-out, and flow state | `IUserAuthorization` selected by `AgentApplication:UserAuthorization` configuration |

### In this guide

- [Design the extension-facing API](#design-the-extension-facing-api)
- [Route builder extensibility](#route-builder-extensibility)
- [Route attribute helpers](#route-attribute-helpers)
- [Custom entities](#custom-entities)
- [Custom activities](#custom-activities)
- [Serialization customization](#serialization-customization)
- [Map host endpoints](#map-host-endpoints)
- [Custom adapters](#custom-adapters)
- [Custom DI with `IAgentServiceRegistrar`](#custom-di-with-iagentserviceregistrar)
- [Custom `IAccessTokenProvider`](#custom-iaccesstokenprovider)
- [Custom `IUserAuthorization`](#custom-iuserauthorization)
- [Typed turn contexts and native clients](#typed-turn-contexts-and-native-clients)
- [Package layout](#package-layout)
- [Validation checklist](#validation-checklist)
- [Reference implementations](#reference-implementations)

An extension will commonly also provide:

- an `AgentExtension` subclass that exposes channel-scoped route builders and helpers;
- an `AgentExtensionAttribute<TExtension>` subclass that generates an extension property on an
  `AgentApplication`;
- a typed `ITurnContext` and typed `IActivity`;
- access to the protocol's native client for operations that do not map cleanly to
  `ITurnContext.SendActivityAsync`.

### How automatic discovery works

Agents SDK source generators emit assembly-level manifests for extension types. A consuming
application also receives a generated preload registry that references extension types from its
package dependencies. At runtime, the neutral SDK initializer loads those assemblies before
feature-specific discovery occurs.

The relevant subsystem then reads the manifests:

1. `ProtocolJsonSerializer` discovers serialization initializers, entities, and activities.
2. `IChannelAdapterRegistry` discovers channel adapters.
3. `AddAgentCore` calls `AddAgentExtensionServices`, which discovers DI registrars.

The SDK initialization entry points ensure referenced extension assemblies are loaded before each
subsystem performs its feature-specific discovery.

Types that must be referenced from a consuming assembly, especially adapter and registrar types,
should be public. Keep generated-manifest types concrete and externally accessible.

Remember that a CLR does not load an otherwise unused assembly merely because it appears in the
dependency graph. Give the package a recognized preload anchor: a public entity, activity,
channel adapter, or service registrar. Applying the package's AgentExtension attribute also creates
a direct runtime type reference. A service registrar is the most reliable anchor for an extension
that otherwise contains only serialization customization.

## Design the extension-facing API

`AgentExtension` is the base implementation of `IAgentExtension`. It associates routes and helper
APIs with a channel. A typical extension stores the owning `AgentApplication`, sets `ChannelId`,
and exposes channel-scoped route registration methods.

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;

public sealed class ContosoAgentExtension : AgentExtension
{
    private readonly AgentApplication _application;

    public ContosoAgentExtension(AgentApplication application)
    {
        _application = application;
        ChannelId = "contoso";
    }

    public ContosoAgentExtension OnMessage(
        ContosoRouteHandler handler,
        ushort rank = RouteRank.Unspecified)
    {
        _application.AddRoute(
            ContosoMessageRouteBuilder.Create()
                .WithHandler(handler)
                .WithOrderRank(rank)
                .Build());

        return this;
    }
}
```

Create a marker attribute so applications can opt into the extension without manually
constructing or registering it:

```csharp
using Microsoft.Agents.Builder.App;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ContosoExtensionAttribute
    : AgentExtensionAttribute<ContosoAgentExtension>
{
}
```

The application class must be `partial`:

```csharp
[ContosoExtension]
public partial class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        ContosoExtension.OnMessage(OnContosoMessageAsync);
    }

    private Task OnContosoMessageAsync(
        IContosoTurnContext turnContext,
        ITurnState turnState,
        CancellationToken cancellationToken)
    {
        // Extension-specific handler implementation.
        return Task.CompletedTask;
    }
}
```

The AgentExtension source generator creates a `ContosoExtension` property, constructs the
extension, and registers it during `AgentApplication` construction. The property name is derived
from the extension type: `ContosoAgentExtension` becomes `ContosoExtension`.

Extension construction occurs from the base `AgentApplication` constructor. Do not access fields
that are initialized only in the derived agent constructor. Limit extension construction to the
owning application's base state, route registration, and before-turn infrastructure.

## Route builder extensibility

`RouteBuilderBase<TBuilder>` provides the common route machinery used by:

- `AgentApplication` route helpers, such as `OnMessage`;
- route attributes, such as `[MessageRoute]`;
- direct `AgentApplication.AddRoute(builder.Build())` calls.

It supplies channel matching, custom selectors, OAuth handler selection, agentic and invoke flags,
non-terminal routes, route rank, and final validation in `Build()`. Specialized Builder types add
the matching rules and handler type for a feature. The Builder library includes bases such as
`MessageRouteBuilderBase<TBuilder>`, `TypeRouteBuilderBase<TBuilder>`,
`EventRouteBuilderBase<TBuilder>`, and `ConversationUpdateRouteBuilderBase<TBuilder>`.

`RouteBuilderBase<TBuilder>` and its specialized bases are public extension points, but they are
not usually the primary application-facing API. Most applications should use route helpers or
route attributes. A custom Builder is useful when an extension needs to:

- share the same matching and route-option behavior between helpers and attributes;
- enforce a channel ID, activity type, invoke flag, or other invariant;
- convert a channel-specific handler delegate to an SDK route delegate;
- expose advanced options that would make the common helper or attribute unnecessarily complex.

It is acceptable for a Builder to expose more advanced configuration than the corresponding route
helper or attribute. Callers that need those options can construct the Builder and pass its built
route to `AgentApplication.AddRoute`.

For example, a channel-specific message Builder can reuse the SDK's message matching while
enforcing the channel and adapting a typed handler:

```csharp
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core;

public sealed class ContosoMessageRouteBuilder
    : MessageRouteBuilderBase<ContosoMessageRouteBuilder>
{
    public static ContosoMessageRouteBuilder Create() => new();

    public ContosoMessageRouteBuilder WithHandler(
        ContosoRouteHandler handler)
    {
        AssertionHelpers.ThrowIfNull(handler, nameof(handler));
        _route.Handler = HandlerUtils.WrapHandler(handler);
        return this;
    }

    protected override void PreBuild()
    {
        _route.ChannelId = "contoso";
        base.PreBuild();
    }
}
```

`Microsoft.Agents.Extensions.MSTeams.App` follows this pattern. For example:

- `TeamsMessageRouteBuilder` derives from `MessageRouteBuilderBase<TBuilder>`, wraps a
  `TeamsRouteHandler`, and sets the Teams channel in `PreBuild()`.
- `TeamsTypeRouteBuilder`, `TeamsEventRouteBuilder`, and
  `TeamsConversationUpdateRouteBuilder` specialize the corresponding SDK bases.
- feature-specific Builders such as task-module, message-extension, meeting, file-consent, and
  message-event Builders derive from `RouteBuilderBase<TBuilder>` directly or through another
  feature-specific base.

The same Teams Builders are used by fluent helpers such as `OnTeamsMessage` and by route
attributes such as `[TeamsMessageRoute]`. Keeping route construction in the Builder prevents the
two surfaces from implementing subtly different matching, channel, authentication, or handler
behavior.

### Builder implementation guidance

- Return the concrete `TBuilder` from fluent methods.
- Set mandatory channel or protocol invariants in `PreBuild()` so callers cannot accidentally omit
  them.
- Call `base.PreBuild()` when deriving from a specialized base.
- Put feature-specific selector composition in the Builder rather than duplicating it in each
  helper and attribute.
- Let `Build()` validate that the final route has both a selector and a handler.
- Use `WithChannelId`, `WithOAuthHandlers`, `AsAgentic`, `AsInvoke`, `AsNonTerminal`, and
  `WithOrderRank` instead of manipulating the corresponding route fields unless the Builder is
  enforcing an invariant.

## Route attribute helpers

A route attribute implements `IRouteAttribute`. During `AgentApplication` construction, the SDK
reflects over the agent's methods and calls `AddRoute` for every applied `IRouteAttribute`.

Use `RouteAttributeHelper` instead of duplicating reflection and sign-in-handler logic:

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method, Inherited = true)]
[RouteHandlerType(typeof(ContosoRouteHandler))]
public sealed class ContosoMessageRouteAttribute(
    bool isAgenticOnly = false,
    ushort rank = RouteRank.Unspecified,
    string autoSignInHandlers = null)
    : Attribute, IRouteAttribute
{
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler =
            RouteAttributeHelper.CreateHandlerDelegate<ContosoRouteHandler>(
                app,
                method);

        var builder = ContosoMessageRouteBuilder.Create()
            .WithHandler(handler)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);

        RouteAttributeHelper.ApplySignInHandlers(
            app,
            autoSignInHandlers,
            names => builder.WithOAuthHandlers(names),
            selector => builder.WithOAuthHandlers(selector));

        app.AddRoute(builder.Build());
    }
}
```

`RouteAttributeHelper` provides:

- `CreateHandlerDelegate<T>` and its non-generic overload, which bind instance methods to the
  current `AgentApplication` and create unbound delegates for static methods;
- `GetDeclaredHandlerType` and `GetDeclaredHandlerTypes`, which read the delegate types declared by
  `[RouteHandlerType]`;
- `CreateMatchingHandlerDelegate`, which selects the first declared, closed delegate whose
  `Invoke` signature exactly matches the attributed method;
- `InvokeGenericWithHandler`, for attributes whose handler is an open generic delegate and whose
  closed type must be inferred from a method parameter;
- `ApplySignInHandlers`, which treats the attribute argument as either the name of an instance or
  static `Func<ITurnContext, string[]>` method on the agent, or as a comma-, space-, or
  semicolon-delimited list of handler names;
- `DelimitedToList`, when an attribute needs the parsed handler-name array directly.

### Declare handler signatures with RouteHandlerTypeAttribute

Apply `[RouteHandlerType(typeof(TDelegate))]` to every route attribute class whose handler
signature can be declared statically. The attribute stores the expected delegate type in compiled
metadata. `Microsoft.Agents.Core.Analyzers` uses that metadata because an analyzer in the
consuming project cannot infer a delegate type from the runtime body of `IRouteAttribute.AddRoute`.

The `MAA002` analyzer compares the attributed method's return type and parameter types with the
declared delegate's `Invoke` signature. A mismatch is therefore a compile-time error instead of a
runtime delegate-binding failure during agent construction.

`RouteHandlerTypeAttribute` is inherited and can be applied more than once. Multiple applications
allow an attribute to accept any of several handler delegates. Slack uses this for route
attributes that accept `SlackRouteHandler`, the base `RouteHandler`, or
`TypedRouteHandler<ISlackActivity>`; `CreateMatchingHandlerDelegate` chooses the matching closed
delegate at runtime.

Open generic delegates, such as `FetchHandler<>`, cannot be validated until their type argument is
inferred from the decorated method. The analyzer skips those declarations; the attribute can use
`InvokeGenericWithHandler` during route registration. Route methods are discovered by reflection,
so the analyzer package also suppresses IDE0051 for otherwise apparently unused private methods
decorated with an `IRouteAttribute`.

## Custom entities

Use a custom `Entity` when the protocol needs a typed object in `Activity.Entities`. Entity
deserialization is polymorphic: the wire-level `type` value selects the concrete CLR type.

```csharp
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

[EntityName(EntityType)]
public sealed class ContosoContextEntity : Entity
{
    public const string EntityType = "contoso.context";

    public ContosoContextEntity() : base(EntityType)
    {
    }

    public string Tenant { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;
}
```

The `Entity` subclass is discovered by the source generator. `[EntityName]` explicitly controls
the discriminator key; without it, the class name is used. Calling `base(EntityType)` ensures new
instances serialize with the same discriminator used for deserialization.

No application registration or custom converter is required. Once the package is referenced,
`ProtocolJsonSerializer` can deserialize an entity with `"type": "contoso.context"` into
`ContosoContextEntity`.

The existing `ClientInfo` entity in `Microsoft.Agents.Extensions.Teams` follows this pattern.

### Entity guidance

- Make cross-assembly entity types public.
- Use a stable, protocol-owned discriminator. Treat changing it as a wire-format breaking change.
- Provide a parameterless constructor for `System.Text.Json`.
- Keep entity-specific conversion in the entity or a dedicated converter; do not require
  applications to mutate global serializer options.
- Test serialization through `ProtocolJsonSerializer`, not a separately constructed
  `JsonSerializerOptions`.

## Custom activities

Use a custom `Activity` when handlers should receive a protocol-specific shape rather than a base
`Activity`. `[ActivityType]` supports three case-insensitive wire discriminators:

- `Type`
- `ChannelId`
- `Name`

Set at least one discriminator. When more than one is set, all must match. Multiple attributes can
be applied to one activity class, and the most specific matching registration wins.

`Microsoft.Agents.Extensions.MSTeams` defines a typed activity interface:

```csharp
public interface ITeamsActivity : IActivity
{
    new Microsoft.Teams.Api.ChannelData ChannelData { get; set; }
}
```

Its concrete activity is selected for every Teams channel activity:

```csharp
[ActivityType(ChannelId = Channels.Msteams)]
public class TeamsActivity : Activity, ITeamsActivity
{
    public new Microsoft.Teams.Api.ChannelData ChannelData
    {
        get => this.GetChannelData<Microsoft.Teams.Api.ChannelData>();
        set => base.ChannelData = value;
    }
}
```

The interface is the handler-facing contract. The concrete, attributed `Activity` subclass is
what the serializer registers.

### More specific activity matching

An extension can discriminate further:

```csharp
[ActivityType(
    ActivityTypes.Invoke,
    ChannelId = "contoso",
    Name = "contoso/action")]
public sealed class ContosoActionActivity : Activity
{
    public string Operation { get; set; } = string.Empty;
}
```

For matching rules that cannot be represented by `Type`, `ChannelId`, and `Name`, use
`ProtocolJsonSerializer.RegisterActivityTypeResolver`. Imperative resolvers are global and run for
all candidate activities, so they must:

- tolerate every valid Activity payload shape;
- return `null` quickly for unrelated channels and activity types;
- advance the supplied private `Utf8JsonReader` copy as needed without mutating global state;
- be registered during deterministic extension initialization.

Prefer `[ActivityType]` whenever declarative matching is sufficient.

## Serialization customization

Agents SDK serialization uses `System.Text.Json` exclusively through
`ProtocolJsonSerializer`. Do not ask an application to add converters to ASP.NET Core MVC JSON
options; those options are not the protocol serializer.

Mark an extension initializer with `[SerializationInit]`:

```csharp
using Microsoft.Agents.Core.Serialization;

[SerializationInit]
internal sealed class SerializationInit
{
    public static void Init()
    {
        ProtocolJsonSerializer.ApplyExtensionConverters(
        [
            new ContosoEnvelopeConverter(),
            new ContosoEventConverter(),
        ]);

        ProtocolJsonSerializer.AddTypeInfoResolver(
            ContosoJsonContext.Default);
    }
}
```

The initializer type can remain internal, but `Init` must have the exact signature
`public static void Init()`. A source generator emits the assembly manifest, and
`ProtocolJsonSerializer` invokes the method automatically.

The current A2A hosting library uses this mechanism to add its source-generated
`A2AJsonUtilities.JsonContext.Default` resolver; the application does not configure that resolver
itself.

### Supported customization APIs

Use the narrowest API that fits:

- `ApplyExtensionConverters` adds one or more `JsonConverter` instances.
- `AddTypeInfoResolver` prepends a source-generated context or another
  `IJsonTypeInfoResolver`.
- `ApplyExtensionOptions` is an advanced escape hatch for other option transformations.

These APIs use copy-on-write under a lock. Never mutate
`ProtocolJsonSerializer.SerializationOptions` directly because a concurrent serializer can freeze
the options instance.

If `ApplyExtensionOptions` replaces `TypeInfoResolver`, preserve the SDK's resolver chain,
including `CoreJsonContext.Default`. Omitting core metadata can silently change or break Activity
Protocol serialization.

## Map host endpoints

Endpoint mapping is not automatically discovered or applied. The consuming application must
explicitly map endpoints in `Program.cs` after building the host:

```csharp
WebApplication app = builder.Build();

app.UseAgents();
app.MapAgentApplicationEndpoints();
```

Mapping is host-, transport-, and sometimes channel-specific. An extension that exposes a native
HTTP protocol normally provides a host-specific mapping extension alongside its service
extensions. Service registration makes the adapter and its dependencies available through DI;
the mapping extension makes the protocol reachable over HTTP. These are separate operations.

For example, `AgentEndpointExtensions.MapAgentApplicationEndpoints` maps the shared Activity
Protocol endpoint. An A2A extension can provide its own mapping extension for A2A routes:

```csharp
WebApplication app = builder.Build();

app.UseAgents();
app.MapAgentApplicationEndpoints();
app.MapA2AApplicationEndpoints();
```

`A2AServiceExtensions.MapA2AApplicationEndpoints` is an example of a protocol-specific endpoint
mapper. It maps the A2A request surface and resolves the A2A adapter from DI. Other hosts or
channels can require different endpoint types, middleware, authentication conventions, or route
metadata, so endpoint mapping belongs in the host-specific package rather than the
host-independent Builder registration contract.

Use `AgentInterfaceAttribute` when an endpoint mapper needs to discover which interfaces an
`AgentApplication` exposes:

```csharp
[AgentInterface(
    AgentTransportProtocol.ActivityProtocol,
    "/api/messages")]
[AgentInterface(
    A2AAgentTransportProtocol.JsonRpc,
    "/a2a")]
public sealed class ContosoAgent : AgentApplication
{
    // Agent implementation.
}
```

The attribute declares the transport protocol and relative path for one agent interface. Because
it allows multiple instances, the same agent can expose multiple transports or multiple paths for
a transport when the corresponding host endpoint mappers support them.

`AgentInterfaceAttribute.Protocol` is the endpoint mapper's selection key, not descriptive
metadata and not the Activity `channelId`. A mapper inspects the attributes and maps only entries
whose `Protocol` value it recognizes:

- `MapAgentApplicationEndpoints` selects `AgentTransportProtocol.ActivityProtocol` and maps the
  matching `Path` as an Activity Protocol endpoint.
- `MapA2AApplicationEndpoints` selects the A2A JSON-RPC and HTTP+JSON protocol values and maps each
  matching `Path` with the appropriate A2A endpoint shape.
- A mapper ignores attributes for protocols it does not support, allowing multiple mappers to
  process the same `AgentApplication` without mapping each other's interfaces.

Protocol values are an exact contract between the attribute and the endpoint mapper. Extension
authors should define public constants for their protocol values and application authors should
use those constants rather than string literals. A mismatch means the mapper will not create an
endpoint for that interface.

When a single agent has no `AgentInterfaceAttribute`, a mapper can synthesize its own protocol and
default path. Once the agent declares any interface attribute, each intended interface should be
declared explicitly; a mapper does not synthesize a missing protocol-specific default when
attributes already exist. Applications with multiple agents must also declare their interfaces
explicitly so protocols and paths can be assigned unambiguously.

## Custom adapters

A custom channel adapter translates between a native transport/protocol and the Activity Protocol.
There are two hosting models:

| Model | Endpoint | Request shape | Adapter selection |
|---|---|---|---|
| Tier 1 | A protocol-specific endpoint | Native protocol payload | The endpoint requests the protocol-specific adapter directly from DI |
| Tier 2 | The shared Activity Protocol endpoint | Activity Protocol JSON with a top-level `channelId` | The host peeks at `channelId` and resolves the adapter through `IChannelAdapterRegistry` |

Both models normally require the adapter to:

1. Convert an inbound request into an `IActivity`, construct a `TurnContext`, populate per-turn
   native services, and run the agent pipeline.
2. Convert outbound Activities from `SendActivitiesAsync` into native protocol responses.

Annotate an adapter with `[ChannelAdapter("channelId")]` when it should be discoverable through
`IChannelAdapterRegistry`, including for Tier 2 dispatch or proactive adapter resolution. A Tier 1
adapter reached only through a dedicated endpoint can instead be registered explicitly in DI. The
HTTP interface and endpoint mapping depend on whether the extension uses Tier 1 or Tier 2.

### Tier 1: protocol-specific endpoints

Use Tier 1 when the protocol has its own wire format or endpoint surface. The endpoint already
knows which adapter handles the request, so it asks DI for that adapter or a protocol-specific
adapter interface. It does not inspect an Activity `channelId` to choose an adapter.

For example, a protocol can define an HTTP adapter contract:

```csharp
public interface IContosoHttpAdapter : IAgentHttpAdapter
{
    Task ProcessNotificationAsync(
        HttpRequest request,
        HttpResponse response,
        IAgent agent,
        CancellationToken cancellationToken);
}
```

The concrete class implements both the channel-adapter and HTTP contracts:

```csharp
[ChannelAdapter("contoso")]
public sealed class ContosoAdapter
    : ChannelAdapter, IContosoHttpAdapter
{
    public Task ProcessAsync(
        HttpRequest request,
        HttpResponse response,
        IAgent agent,
        CancellationToken cancellationToken = default)
    {
        return ProcessNotificationAsync(
            request,
            response,
            agent,
            cancellationToken);
    }

    public async Task ProcessNotificationAsync(
        HttpRequest request,
        HttpResponse response,
        IAgent agent,
        CancellationToken cancellationToken)
    {
        // Validate and deserialize the native Contoso request.
        // Convert it to an Activity and invoke the agent pipeline.
    }

    public override Task<ResourceResponse[]> SendActivitiesAsync(
        ITurnContext turnContext,
        IActivity[] activities,
        CancellationToken cancellationToken)
    {
        // Translate outbound Activities to native Contoso responses.
        throw new NotImplementedException();
    }
}
```

The endpoint explicitly requires the protocol interface:

```csharp
app.MapPost(
    "/contoso",
    (
        HttpRequest request,
        HttpResponse response,
        IContosoHttpAdapter adapter,
        IAgent agent,
        CancellationToken cancellationToken) =>
            adapter.ProcessNotificationAsync(
                request,
                response,
                agent,
                cancellationToken));
```

The service registrar aliases the interface to the same concrete singleton:

```csharp
services.TryAddSingleton<ContosoAdapter>();
services.TryAddSingleton<IContosoHttpAdapter>(
    provider => provider.GetRequiredService<ContosoAdapter>());
```

`[ChannelAdapter]` is still useful in Tier 1. It allows proactive operations and other SDK
features to resolve the adapter by `channelId`, even though inbound HTTP dispatch reaches it
directly through the dedicated endpoint.

The current `Microsoft.Agents.Hosting.A2A` implementation is a Tier 1 example:

- `A2AAdapter` derives from `ChannelAdapter` and implements `IA2AHttpAdapter`.
- `AddA2AAdapter` explicitly registers `A2AAdapter` and aliases `IA2AHttpAdapter` to the same
  singleton.
- `MapA2AEndpoints` maps the dedicated JSON-RPC and agent-card endpoints and requests
  `IA2AHttpAdapter` directly from DI.
- inbound message requests are converted to Activities; `ProcessActivityAsync` creates a
  `TurnContext` and runs the adapter pipeline.
- outbound Activities are sent to the request's `ChannelResponseQueue`.

The current A2A adapter is not annotated with `[ChannelAdapter]`, so it is not discovered through
`IChannelAdapterRegistry` and is not selected by the shared-endpoint Tier 2 channel peek.

### Tier 2: shared Activity Protocol endpoint

Use Tier 2 when the incoming request is already an Activity Protocol Activity and should share the
endpoint mapped by `MapAgentApplicationEndpoints`. Tier 2 lets one endpoint dispatch different
Activity channels to different adapters.

The endpoint performs this sequence:

1. If no channel-specific adapters are registered, it immediately uses the default
   `IAgentHttpAdapter`, normally `CloudAdapter`, without reading the body for dispatch.
2. Otherwise, it enables request buffering and incrementally scans the JSON body for the
   top-level `channelId`.
3. It calls `IChannelAdapterRegistry.TryGetAdapter(channelId, out adapter)`.
4. If the resolved `IChannelAdapter` also implements `IAgentHttpAdapter`, the endpoint calls that
   adapter's `ProcessAsync`.
5. For a missing, malformed, or unregistered `channelId`, or when the registered adapter does not
   implement `IAgentHttpAdapter`, it falls back to the default adapter.
6. The request body is rewound so the selected adapter can deserialize the complete Activity.

The channel peek is only a dispatch operation. ASP.NET Core authorization middleware normally
authenticates the endpoint before adapter dispatch. The selected adapter consumes that
authenticated identity and remains responsible for full Activity deserialization and validation,
pipeline invocation, and writing the HTTP response.

There is currently no production extension in this repository that serves as a reference
implementation of a Tier 2 custom adapter. The following is an illustrative pattern for a channel
that uses the standard Activity Protocol HTTP shape but needs a specialized `CloudAdapter`:

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Adapters;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;

[ChannelAdapter("contoso")]
public sealed class ContosoActivityProtocolAdapter : CloudAdapter
{
    public ContosoActivityProtocolAdapter(
        IChannelServiceClientFactory channelServiceClientFactory,
        IActivityTaskQueue activityTaskQueue,
        ILogger<CloudAdapter> logger,
        AdapterOptions options = null,
        IMiddleware[] middlewares = null,
        IConfiguration configuration = null,
        IOutboundHostValidator hostValidator = null)
        : base(
            channelServiceClientFactory,
            activityTaskQueue,
            logger,
            options,
            middlewares,
            configuration,
            hostValidator)
    {
        OnTurnError = HandleContosoTurnErrorAsync;
    }

    private Task HandleContosoTurnErrorAsync(
        ITurnContext turnContext,
        Exception exception)
    {
        // Apply channel-specific error behavior.
        return Task.CompletedTask;
    }
}
```

Because `CloudAdapter` already implements both `IChannelAdapter` and `IAgentHttpAdapter`, the
registry can return this type directly to the shared endpoint. The extension registrar should
register the concrete adapter as a singleton:

```csharp
services.TryAddSingleton<ContosoActivityProtocolAdapter>();
```

The application continues to map only the normal shared endpoint:

```csharp
app.MapAgentApplicationEndpoints();
```

No Contoso-specific endpoint call is required. An inbound Activity with
`"channelId": "contoso"` selects `ContosoActivityProtocolAdapter`; other channels continue through
the default adapter.

Tier 2 is appropriate only when the request is compatible with the shared Activity Protocol
endpoint. A native webhook, JSON-RPC protocol, multipart request, or protocol with additional
resource endpoints belongs in Tier 1.

### Adapter discovery versus DI registration

`[ChannelAdapter]` registers the channel-to-adapter-type mapping. It does not register every
protocol-specific service alias. Use an `IAgentServiceRegistrar` for constructor dependencies,
the concrete adapter singleton, and aliases such as `IContosoHttpAdapter`.

The adapter registry creates adapters lazily and caches one instance per adapter type. Register
adapter types as singletons. If a concrete adapter is already in DI, the registry resolves that
singleton; otherwise, it uses `ActivatorUtilities`, so all constructor dependencies must still be
available.

Adding a channel adapter does not make it the default. In the normal `AddAgent` flow,
`CloudAdapter` remains the conventional default and the registry selects a custom adapter only
when the Activity's channel matches.

For an unusual custom host that intentionally does not use `CloudAdapter`, compose the stack
explicitly:

```csharp
services.AddChannelAdapter<ContosoAdapter>("contoso");
services.SetDefaultChannelAdapter<ContosoAdapter>();
```

Use `TrySetDefaultChannelAdapter<TAdapter>` only when establishing a framework convention that
must not replace an application's explicit choice.

## Custom DI with IAgentServiceRegistrar

Use `IAgentServiceRegistrar` for services that must exist whenever the extension package is
referenced. The contract lives in `Microsoft.Agents.Builder`, so it is not tied to ASP.NET Core.

Declare the registrar at assembly scope:

```csharp
using Microsoft.Agents.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: AgentServiceRegistration(
    typeof(ContosoExtensionServiceRegistrar))]

public sealed class ContosoExtensionServiceRegistrar
    : IAgentServiceRegistrar
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<ContosoTransport>();
        services.TryAddSingleton<ContosoAdapter>();
        services.TryAddSingleton<IContosoHttpAdapter>(
            provider => provider.GetRequiredService<ContosoAdapter>());
    }
}
```

`AddAgentCore` automatically invokes `AddAgentExtensionServices`, so normal ASP.NET Core
applications need no extension-specific service-registration call. A host that manually composes
the SDK without `AddAgentCore` calls the generic Builder API once:

```csharp
services.AddAgentExtensionServices();
```

### Registrar requirements

- The registrar must be public, concrete, and implement `IAgentServiceRegistrar`.
- It must have a public parameterless constructor.
- Use `TryAdd`, `TryAddEnumerable`, or equivalent idempotent registration patterns so explicit
  application registrations remain authoritative.
- Do not call `BuildServiceProvider` or resolve services during registration.
- Keep registration deterministic and free of runtime I/O.
- Registration is applied at most once per `IServiceCollection`.
- If the extension exposes a protocol-specific adapter interface, alias it to the same concrete
  singleton rather than constructing a second adapter.

Not every host integration currently uses automatic registrar discovery. For example, the current
A2A hosting library requires the application to call `AddA2AAdapter`. An AgentExtension package
that promises dependency-only DI wiring should add an `IAgentServiceRegistrar` instead of relying
on a package-specific registration call.

## Custom IAccessTokenProvider

Implement `IAccessTokenProvider` when an extension needs a connection-level authentication
mechanism other than the built-in MSAL provider. Connection token providers acquire
application/service tokens used for outbound calls to Azure Bot Service, another agent, Microsoft
Graph, or another protected API.

This is different from `IUserAuthorization`:

- `IAccessTokenProvider` represents a named application connection.
- `IUserAuthorization` represents an end user's sign-in and token lifecycle during a turn.
- An `IUserAuthorization` implementation can use `IConnections` to obtain an
  `IAccessTokenProvider` for on-behalf-of exchange or a downstream API.

The interface requires:

```csharp
public interface IAccessTokenProvider
{
    Task<string> GetAccessTokenAsync(
        string resourceUrl,
        IList<string> scopes,
        bool forceRefresh = false);

    TokenCredential GetTokenCredential();

    ImmutableConnectionSettings ConnectionSettings { get; }
}
```

`GetAccessTokenAsync` must honor the requested resource, scopes, and `forceRefresh`. Cache tokens
when the backing identity system supports caching, but bypass or refresh that cache when
`forceRefresh` is `true`.

`GetTokenCredential` exposes the same provider through Azure SDK clients. Return a
provider-specific `TokenCredential` whose `AccessToken.ExpiresOn` reflects the token's real
expiration whenever possible; Azure SDK caching depends on that value.

`ConnectionSettings` exposes the immutable connection metadata used by the SDK, including client
ID, authority, tenant, scopes, and alternate blueprint connection. A custom settings type can
derive from `ConnectionSettingsBase`:

```csharp
public sealed class ContosoConnectionSettings : ConnectionSettingsBase
{
    public ContosoConnectionSettings(IConfigurationSection configuration)
        : base(configuration)
    {
        TokenEndpoint = configuration.GetValue<string>("TokenEndpoint")
            ?? throw new InvalidOperationException(
                "TokenEndpoint is required.");
    }

    public string TokenEndpoint { get; }
}
```

### Required IAccessTokenProvider constructor

Configuration-based loading requires the concrete provider to be a public, non-nested,
non-abstract class with this exact public constructor:

```csharp
public ContosoAccessTokenProvider(
    IServiceProvider serviceProvider,
    IConfigurationSection configurationSection)
```

The loader does not use arbitrary DI constructor selection. A class without that exact signature
is rejected even if another constructor could otherwise be satisfied by DI.

The `configurationSection` argument is the connection's `Settings` section. The
`IServiceProvider` is the application's provider and can be used to resolve shared infrastructure
such as `IHttpClientFactory`, `ILoggerFactory`, or an extension service registered through
`IAgentServiceRegistrar`. Do not call `BuildServiceProvider`.

An abbreviated implementation looks like this:

```csharp
public sealed class ContosoAccessTokenProvider
    : IAccessTokenProvider
{
    private readonly ContosoIdentityClient _client;
    private readonly ContosoConnectionSettings _settings;

    public ContosoAccessTokenProvider(
        IServiceProvider serviceProvider,
        IConfigurationSection configurationSection)
    {
        _settings = new ContosoConnectionSettings(
            configurationSection);

        _client = new ContosoIdentityClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>(),
            _settings.TokenEndpoint);
    }

    public ImmutableConnectionSettings ConnectionSettings =>
        new(_settings);

    public Task<string> GetAccessTokenAsync(
        string resourceUrl,
        IList<string> scopes,
        bool forceRefresh = false)
    {
        return _client.GetTokenAsync(
            resourceUrl,
            scopes ?? _settings.Scopes,
            forceRefresh);
    }

    public TokenCredential GetTokenCredential()
    {
        return new ContosoTokenCredential(this);
    }
}
```

The built-in `MsalAuth` and `SidecarAuth` implementations are useful references.
`SidecarTokenCredential` demonstrates adapting an `IAccessTokenProvider` to `TokenCredential`.

### Configure a custom access-token provider

Add the provider under the top-level `Connections` section:

```json
{
  "Connections": {
    "ContosoConnection": {
      "Assembly": "Contoso.Agents.Authentication",
      "Type": "Contoso.Agents.Authentication.ContosoAccessTokenProvider",
      "Settings": {
        "TokenEndpoint": "https://identity.contoso.example/token",
        "ClientId": "<configured-client-id>",
        "TenantId": "<configured-tenant-id>",
        "AuthorityEndpoint": "https://identity.contoso.example/",
        "Scopes": [
          "api://contoso/.default"
        ]
      }
    }
  },
  "ConnectionsMap": [
    {
      "ServiceUrl": "^https://api\\.contoso\\.example/",
      "Audience": "optional-incoming-audience",
      "Connection": "ContosoConnection"
    }
  ]
}
```

`Assembly` is the loadable assembly name, not a file path. `Type` should be the fully qualified
public CLR type name. The assembly must be part of the deployed application.

`ConnectionsMap` is evaluated in document order. `ServiceUrl` is either `*` or a regular
expression, and `Audience` is optional. The first matching entry selects the connection. If there
is exactly one connection and no map, `ConfigurationConnections` treats that connection as the
default.

If `Assembly` and `Type` are omitted, the loader defaults to
`Microsoft.Agents.Authentication.Msal` and `MsalAuth`; therefore, always specify both for a custom
provider. Do not store client secrets directly in committed `appsettings.json`; use the host's
secret/configuration provider. For example, the standard .NET environment-variable provider can
override the client ID with
`Connections__ContosoConnection__Settings__ClientId`.

Provider instances are created lazily and cached in `ConfigurationConnections`. Implementations
must therefore be safe for concurrent use.

## Custom IUserAuthorization

Implement `IUserAuthorization` when an extension supplies a user-authentication flow that is not
covered by the built-in Azure Bot Token Service, connector, or agentic authorization handlers.
This contract owns the turn-level user lifecycle:

- begin or continue sign-in;
- obtain or refresh a user token;
- sign the user out;
- reset any multi-turn flow state.

The interface exposes a stable handler `Name` and four asynchronous operations:

```csharp
public interface IUserAuthorization
{
    string Name { get; }

    Task<TokenResponse> SignInUserAsync(
        ITurnContext context,
        bool forceSignIn = false,
        string exchangeConnection = null,
        IList<string> exchangeScopes = null,
        CancellationToken cancellationToken = default);

    Task<TokenResponse> GetRefreshedUserTokenAsync(
        ITurnContext turnContext,
        string exchangeConnection = null,
        IList<string> exchangeScopes = null,
        CancellationToken cancellationToken = default);

    Task SignOutUserAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);

    Task ResetStateAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken = default);
}
```

Return a `TokenResponse` with a non-empty token when sign-in is complete. Returning no token from
`SignInUserAsync` means the sign-in flow remains pending. Honor `forceSignIn`,
`exchangeConnection`, and `exchangeScopes` when they are meaningful to the provider, and propagate
the cancellation token through all I/O.

### Required IUserAuthorization constructor

The configuration loader requires the concrete handler to be a public, non-nested, non-abstract
class with this exact public constructor:

```csharp
public ContosoUserAuthorization(
    string name,
    IStorage storage,
    IConnections connections,
    IConfigurationSection configurationSection,
    ILogger logger)
```

The parameter order and declared types are part of the reflection contract. In particular:

- the logger parameter is the non-generic `Microsoft.Extensions.Logging.ILogger`;
- `configurationSection` is the handler's `Settings` section;
- `name` is the key under `Handlers` and should be returned from `Name`;
- `storage` is available for multi-turn authorization state;
- `connections` provides application token providers for OBO or downstream calls.

The last parameter can declare `logger = null`, as built-in implementations do, but it must still
be present with type `ILogger`. Additional code-first constructors are allowed; they do not replace
the required configuration constructor.

```csharp
public sealed class ContosoUserAuthorization
    : IUserAuthorization
{
    private readonly IStorage _storage;
    private readonly IConnections _connections;
    private readonly ContosoUserAuthorizationSettings _settings;
    private readonly ILogger _logger;

    public ContosoUserAuthorization(
        string name,
        IStorage storage,
        IConnections connections,
        IConfigurationSection configurationSection,
        ILogger logger)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _storage = storage
            ?? throw new ArgumentNullException(nameof(storage));
        _connections = connections
            ?? throw new ArgumentNullException(nameof(connections));
        _settings = configurationSection
            .Get<ContosoUserAuthorizationSettings>()
            ?? throw new InvalidOperationException(
                "Contoso user authorization settings are required.");
        _logger = logger;
    }

    public string Name { get; }

    public Task<TokenResponse> SignInUserAsync(
        ITurnContext context,
        bool forceSignIn = false,
        string exchangeConnection = null,
        IList<string> exchangeScopes = null,
        CancellationToken cancellationToken = default)
    {
        // Start or continue the provider-specific, multi-turn flow.
        // When forceSignIn is true, restart authentication rather than
        // reusing an existing session.
        throw new NotImplementedException();
    }

    public Task<TokenResponse> GetRefreshedUserTokenAsync(
        ITurnContext turnContext,
        string exchangeConnection = null,
        IList<string> exchangeScopes = null,
        CancellationToken cancellationToken = default)
    {
        // Validate/refresh the user token and optionally use
        // _connections for OBO exchange.
        throw new NotImplementedException();
    }

    public Task SignOutUserAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        // Revoke provider state and delete persisted flow state.
        return Task.CompletedTask;
    }

    public Task ResetStateAsync(
        ITurnContext turnContext,
        CancellationToken cancellationToken = default)
    {
        // Delete incomplete flow state without assuming sign-out.
        return Task.CompletedTask;
    }
}
```

Namespace persisted state by handler, user, channel, and conversation as appropriate. Treat sign-in
as a multi-turn operation, remove flow state on completion or terminal failure, and avoid storing
raw access or refresh tokens unless the backing store and threat model explicitly support it.

Handlers are created lazily and cached by `UserAuthorizationDispatcher`, so a handler must be safe
for concurrent turns.

### Configure a custom user authorization handler

Configure handlers under `AgentApplication:UserAuthorization:Handlers`:

```json
{
  "AgentApplication": {
    "UserAuthorization": {
      "DefaultHandlerName": "contoso",
      "AutoSignIn": true,
      "Handlers": {
        "contoso": {
          "Assembly": "Contoso.Agents.Authorization",
          "Type": "Contoso.Agents.Authorization.ContosoUserAuthorization",
          "Settings": {
            "AuthorizationEndpoint": "https://identity.contoso.example/authorize",
            "OBOConnectionName": "ContosoConnection",
            "OBOScopes": [
              "api://contoso/user.read"
            ]
          }
        }
      }
    }
  }
}
```

As with connection providers, `Assembly` is a loadable assembly name and `Type` should be the fully
qualified public CLR type name. The handler package must be referenced and copied to the
application output.

If `Assembly` and `Type` are omitted, the loader defaults to
`Microsoft.Agents.Builder.UserAuth.TokenService.AzureBotUserAuthorization`. Built-in short names
such as `AzureBotUserAuthorization`, `AgenticUserAuthorization`, and
`ConnectorUserAuthorization` are expanded automatically. Custom handlers should specify both
properties explicitly.

`DefaultHandlerName` selects the handler used by automatic sign-in. `AutoSignIn` controls whether
the default selector starts authorization automatically; applications can replace that boolean
behavior with an `AutoSignInSelector` through DI. Routes can also select named authorization
handlers.

## Typed turn contexts and native clients

A strong extension API should keep protocol casts and service lookups out of application handlers.
Expose:

1. a typed Activity interface;
2. a typed `ITurnContext`;
3. a native protocol client on that context.

```csharp
public interface IContosoTurnContext : ITurnContext
{
    new IContosoActivity Activity { get; }

    ContosoClient Client { get; }
}

public sealed class ContosoTurnContext : TurnContextWrapper, IContosoTurnContext
{
    public ContosoTurnContext(ITurnContext turnContext)
        : base(turnContext)
    {
    }

    public new IContosoActivity Activity =>
        _turnContext.Activity as IContosoActivity
        ?? ProtocolJsonSerializer.ToObject<ContosoActivity>(
            _turnContext.Activity);

    public ContosoClient Client =>
        _turnContext.Services.Get<ContosoClient>();
}
```

Define a channel-specific delegate and wrap it at the extension boundary:

```csharp
public delegate Task ContosoRouteHandler(
    IContosoTurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken);

internal static RouteHandler WrapHandler(
    ContosoRouteHandler handler)
{
    return async (context, turnState, cancellationToken) =>
    {
        var contosoContext = new ContosoTurnContext(context);
        await handler(
            contosoContext,
            turnState,
            cancellationToken);
    };
}
```

This is the pattern used by MSTeams and Slack. Application code receives
`ITeamsTurnContext` or `ISlackTurnContext`, not a base context that it must cast.

`HandlerUtils.WrapHandler` is an adapter between delegate types; it does not create a new turn or
copy turn state. It wraps the existing `ITurnContext` in a `TurnContextWrapper`, so the typed
context delegates to the same adapter, services, identity, turn state, and outbound pipeline.

MSTeams puts this adaptation in its route Builders: `TeamsMessageRouteBuilder.WithHandler`, for
example, stores `HandlerUtils.WrapHandler(handler)` in the underlying route. As a result, both
`OnTeamsMessage` and `[TeamsMessageRoute]` get the same typed-context behavior because both use
`TeamsMessageRouteBuilder`.

Slack's extension helpers use the standard SDK Builders and pass a wrapped handler explicitly.
Slack route attributes can accept several delegate forms, so they create the matching delegate
with `RouteAttributeHelper.CreateMatchingHandlerDelegate` and then use
`HandlerUtils.ResolveRouteHandler` to wrap only the Slack-specific forms.

### Populate native clients per turn

Native clients often depend on request-specific credentials, IDs, queues, or callbacks. Do not put
that mutable request state in a global singleton.

Populate `TurnContext.Services` while creating or preparing the turn:

- `TeamsAgentExtension` creates and stores the Teams API client in `OnBeforeTurn`.
  `ITeamsTurnContext.Client` returns `Microsoft.Teams.Api.Clients.ApiClient`.
- `SlackAgentExtension` creates a per-turn `SlackApi` in `OnBeforeTurn`.
  `ISlackTurnContext.Client` returns it.

The `Client` API should represent the underlying protocol rather than duplicating it behind an
Activity-shaped facade. This gives extension consumers an escape hatch for protocol features that
the Activity Protocol does not model.

### Direct native responses

Use `ITurnContext.SendActivityAsync` when the response should flow through the configured adapter
and Activity middleware. Use the native client when the operation is intentionally protocol-native.

The `SlackAgent` sample responds directly through Slack:

```csharp
[SlackMessageRoute]
public async Task OnSlackMessageAsync(
    ISlackTurnContext turnContext,
    ITurnState turnState,
    CancellationToken cancellationToken)
{
    var channelData = turnContext.Activity.ChannelData;
    var message = new
    {
        channel = channelData.Channel,
        text = $"You said: {turnContext.Activity.Text}",
        thread_ts = channelData.ThreadTs,
    };

    await turnContext.Client.CallAsync(
        "chat.postMessage",
        message,
        channelData.ApiToken,
        cancellationToken);
}
```

That call goes directly to Slack rather than using `ITurnContext.SendActivityAsync`.

When exposing direct native operations, document that they bypass the normal outbound Activity
pipeline. Preserve cancellation, surface native errors, avoid logging credentials, and account for
middleware behavior such as typing indicators or telemetry. Slack handles its typing-timer concern
by giving the per-turn `SlackApi` an `onCallAsync` callback.

## Package layout

A practical extension package can use this layout:

```text
Microsoft.Agents.Extensions.Contoso/
|-- ContosoAgentExtension.cs
|-- ContosoExtensionAttribute.cs
|-- ContosoAdapter.cs
|-- ContosoServiceRegistrar.cs
|-- Activities/
|   |-- IContosoActivity.cs
|   `-- ContosoActivity.cs
|-- Entities/
|   `-- ContosoContextEntity.cs
|-- Routing/
|   |-- ContosoMessageRouteBuilder.cs
|   |-- ContosoMessageRouteAttribute.cs
|   |-- ContosoRouteHandler.cs
|   |-- HandlerUtils.cs
|   `-- ...
|-- Serialization/
|   |-- SerializationInit.cs
|   |-- ContosoJsonContext.cs
|   `-- Converters/
|       `-- ContosoEnvelopeConverter.cs
|-- Authentication/
|   |-- ContosoAccessTokenProvider.cs
|   |-- ContosoConnectionSettings.cs
|   |-- ContosoTokenCredential.cs
|   `-- ContosoUserAuthorization.cs
`-- Transport/
    |-- IContosoHttpAdapter.cs
    |-- ContosoClient.cs
    `-- ContosoTransport.cs
```

Keep host-specific endpoint mapping separate from the host-independent Builder registration
contract. If the package supports multiple hosts, put ASP.NET Core endpoint extensions in a
host-specific assembly while keeping activities, entities, serialization, and the
`IAgentServiceRegistrar` contract in the core extension package.

## Validation checklist

Before publishing an AgentExtension package, verify:

- Referencing the package is sufficient for entity, activity, serialization, adapter, and DI
  discovery.
- A minimal `AddAgent` application does not call an extension-specific DI method.
- Required host- or channel-specific endpoints are explicitly mapped in `Program.cs`.
- Endpoint mapping extensions honor `AgentInterfaceAttribute` protocol and path declarations.
- The `AgentApplication` extension attribute generates the expected property and eagerly
  initializes required before-turn behavior.
- Route helpers and route attributes use the same specialized Builders.
- Specialized Builders enforce channel and protocol invariants in `PreBuild()`.
- Route attributes use `RouteAttributeHelper` and declare closed handler signatures with
  `[RouteHandlerType]`.
- Attributed handler methods produce no MAA002 signature diagnostics.
- Every attributed Activity discriminator round-trips through `ProtocolJsonSerializer`.
- Custom entities deserialize by their stable `type` value.
- Serialization initializers use exactly `public static void Init()`.
- Serializer customization preserves `CoreJsonContext.Default`.
- The channel adapter is a singleton and the registry returns the DI-managed instance.
- `CloudAdapter` remains the default in a normal multi-channel application.
- Explicit application registrations override registrar defaults.
- A custom `IAccessTokenProvider` loads from `Connections` and has the exact
  `(IServiceProvider, IConfigurationSection)` constructor.
- A custom `IUserAuthorization` loads from `AgentApplication:UserAuthorization:Handlers` and has
  the exact `(string, IStorage, IConnections, IConfigurationSection, ILogger)` constructor.
- Access-token providers and user-authorization handlers are safe for concurrent use.
- Typed route handlers receive the expected typed Activity and native client.
- Native-client calls use per-turn state, propagate cancellation, and do not leak credentials.
- Direct native responses and Activity-pipeline responses are both tested.
- A custom host can opt in through `AddAgentExtensionServices`, `AddChannelAdapter`, and
  `SetDefaultChannelAdapter` without taking an ASP.NET Core dependency.

## Reference implementations

- `Microsoft.Agents.Extensions.MSTeams`
  - `ITeamsActivity` and `TeamsActivity`
  - `ITeamsTurnContext` and `TeamsTurnContext`
  - `App/HandlerUtils`
  - route Builders in `App` and the feature-specific directories
  - route attributes that use `RouteAttributeHelper` and `[RouteHandlerType]`
  - `TeamsAgentExtension`
- `Microsoft.Agents.Extensions.Slack`
  - `ISlackTurnContext` and `SlackTurnContext`
  - `HandlerUtils`
  - `AgentApplicationAttributes`
  - `SlackAgentExtension`
  - the `SlackAgent` sample's direct `chat.postMessage` calls
- `Microsoft.Agents.Builder.App`
  - `RouteBuilderBase<TBuilder>` and the specialized route Builder bases
  - `RouteAttributeHelper`
  - `RouteHandlerTypeAttribute`
- `Microsoft.Agents.Core.Analyzers`
  - `RouteHandlerSignatureAnalyzer` (MAA002)
  - `RouteHandlerUnusedSuppressor`
- `Microsoft.Agents.Hosting.A2A`
  - `A2AAdapter`
  - `A2AServiceExtensions`
  - `SerializationInit`
- Authentication and user authorization
  - `Microsoft.Agents.Authentication.Msal.MsalAuth`
  - `Microsoft.Agents.Authentication.EntraAuthSidecar.SidecarAuth`
  - `Microsoft.Agents.Authentication.EntraAuthSidecar.SidecarTokenCredential`
