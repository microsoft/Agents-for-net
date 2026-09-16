# Microsoft.Agents.Hosting.AspNetCore

## About

ASP.NET Core hosting for agents built with the Microsoft 365 Agents SDK. This package connects the SDK's agent and adapter abstractions to ASP.NET Core dependency injection, middleware, and endpoint routing so applications can receive Activity Protocol requests from Azure Bot Service and other channels.

## Main APIs

- **Host setup:** `AddAgentDefaults()`, `AddAgent<T>()`, and `AddAgentAuthorization()` register the common ASP.NET Core services, an agent with its `CloudAdapter`, and an application-selected authentication scheme.
- **Request pipeline:** `UseAgents()` adds authentication, authorization, and header propagation middleware.
- **Endpoint mapping:** `MapDefaultAgentEndpoints()` provides conventional root and Activity Protocol endpoints. `MapAgentApplicationEndpoints()`, `MapAgentEndpoints()`, and `MapAgentProactiveEndpoints<TAgent>()` support custom and proactive routing.
- **Adapters:** `CloudAdapter` and `IAgentHttpAdapter` translate ASP.NET Core HTTP requests and responses to the SDK activity-processing pipeline, including invoke, expect-replies, and streaming responses.
- **Optional integrations:** Host extensions register agent middleware, HTTP or Microsoft 365 attachment downloaders, background activity processing, and request-header propagation.

Authentication enforcement integrates with ASP.NET Core, but this package does not provide a concrete authentication scheme.
