# Microsoft.Agents.Authentication

## About

Defines core authentication and token retrieval abstractions for the Microsoft 365 Agents SDK. Supports named, configuration-driven connections to different token providers.

## Main Types

- `IAccessTokenProvider`: Interface for retrieving access tokens
- `IConnections`: Returns an `IAccessTokenProvider` by name or matching criteria
- `ConfigurationConnections`: Loads `IConnections` from `appsettings.json` configuration
