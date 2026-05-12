# Microsoft.Agents.Storage

## About

Provides the storage abstraction layer for the Microsoft 365 Agents SDK. Because agents are stateless by design, this package enables conversation and user state to be persisted across turns.

Includes `MemoryStorage` for development and testing. For production deployments, use `Microsoft.Agents.Storage.Blobs` or `Microsoft.Agents.Storage.CosmosDb`.

## Main Types

- `IStorage`: Core key-value storage interface
- `MemoryStorage`: In-memory storage for testing (non-persistent)

See [Agents SDK storage overview](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage) for more information.
