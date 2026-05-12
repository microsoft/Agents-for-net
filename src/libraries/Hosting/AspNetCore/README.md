# Microsoft.Agents.Hosting.AspNetCore

## About

ASP.NET Core integration package for hosting agents built with the Microsoft 365 Agents SDK. Provides dependency injection extensions and HTTP middleware for processing agent requests from Azure Bot Service and other channels.

## Main Types

- `AddAgent<T>()`: Registers an agent with the DI container
- `AddAgentApplicationOptions()`: Registers `AgentApplicationOptions` with DI
- `AddAgentAspNetAuthentication()`: Configures Azure Bot Service JWT authentication
- `MapAgentApplicationEndpoints()`: Maps the agent HTTP endpoint (default: `/api/messages`)
