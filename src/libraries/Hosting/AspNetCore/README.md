# Microsoft.Agents.Hosting.AspNetCore

## About

ASP.NET Core integration package for hosting agents built with the Microsoft 365 Agents SDK. Provides dependency injection extensions and HTTP middleware for processing agent requests from Azure Bot Service and other channels.

## Main Types

- `AddAgent<T>()`: Registers an agent with the DI container
- `AddAgentApplicationOptions()`: Registers `AgentApplicationOptions` with DI
- `AddAgentAspNetAuthentication()`: Configures Azure Bot Service JWT authentication
- `MapAgentApplicationEndpoints()`: Maps the agent HTTP endpoint (default: `/api/messages`)

## Dependency injection behavior

`AddAgent<TAgent>()` registers the standard Activity Protocol stack and selects `CloudAdapter` as the
default `IChannelAdapter`. When a custom `TAdapter : CloudAdapter` is supplied, that adapter becomes
the conventional default instead.

An application can override the conventional default before or after `AddAgent`:

```csharp
builder.Services.SetDefaultChannelAdapter<CustomAdapter>();
builder.AddAgent<MyAgent>();
```

Hosts that do not use `AddAgent` must register `IAgent`, `AgentApplicationOptions`, storage when
persistent state is required, at least one channel adapter, and a default adapter when fallback or
proactive operations can occur:

```csharp
services.AddChannelAdapter<CustomAdapter>("custom");
services.SetDefaultChannelAdapter<CustomAdapter>();
services.AddAgentApplicationOptions();
services.AddTransient<IAgent, MyAgent>();
```

## Extension service registration

`AddAgentCore` automatically applies service registrars declared by referenced Agents SDK extension
assemblies:

```csharp
[assembly: AgentServiceRegistration(typeof(MyExtensionServiceRegistrar))]
```

The registrar implements `IAgentServiceRegistrar`. Registrations are applied once per
`IServiceCollection`. Extension registrars should use `TryAdd` methods so explicit application
registrations remain authoritative. Custom hosts can invoke `AddAgentExtensionServices()` directly.
