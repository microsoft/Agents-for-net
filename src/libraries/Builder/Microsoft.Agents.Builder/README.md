# Microsoft.Agents.Builder

## About

The primary package for building agents with the Microsoft 365 Agents SDK. Provides the `AgentApplication` routing framework, middleware pipeline, and turn context model for handling conversational activities across channels and platforms.

## Main Types

- `AgentApplication`: Base class for agents with route-based activity handling and middleware support
- `IAgent`: Core agent interface implemented by all agents
- `ITurnContext`: Provides access to the current activity, channel, and services for a given turn
- `ITurnState`: Per-turn state container

## Channel adapter registration

Adapters annotated with `[ChannelAdapter("channelId")]` are discovered automatically. A host can
also register an adapter explicitly:

```csharp
services.AddChannelAdapter<CustomAdapter>("custom");
```

Registering a channel adapter does not make it the fallback adapter. Select the default explicitly
when manually composing the agent stack:

```csharp
services.SetDefaultChannelAdapter<CustomAdapter>();
```

`TrySetDefaultChannelAdapter<TAdapter>()` establishes a conventional default without replacing a
default already selected by the application. `SetDefaultChannelAdapter<TAdapter>()` always represents
an explicit application choice.

Applications that construct `AgentApplicationOptions` through dependency injection must provide an
`IChannelAdapter`. The constructor uses an injected `IChannelAdapterRegistry` when available; otherwise,
it creates a registry around the supplied adapter. Applications constructing options programmatically
must set `ChannelAdapterRegistry` or use proactive APIs that accept an adapter explicitly.

## Extension service registration

Extensions can register services without requiring an explicit application call by declaring an
assembly registrar:

```csharp
[assembly: AgentServiceRegistration(typeof(MyExtensionServiceRegistrar))]
```

The registrar implements `IAgentServiceRegistrar`. Registrations are applied once per
`IServiceCollection`; registrars should use `TryAdd` methods so application registrations remain
authoritative. Hosts can invoke `AddAgentExtensionServices()` directly. The ASP.NET Core
`AddAgentCore` convenience method invokes it automatically.
