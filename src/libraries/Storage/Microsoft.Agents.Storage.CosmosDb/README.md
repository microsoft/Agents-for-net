# Microsoft.Agents.Storage.CosmosDb

## About

Azure Cosmos DB implementation of `IStorage` for the Microsoft 365 Agents SDK. Suitable for production deployments requiring persistent, globally distributed conversation state storage.

## Main Types

- `CosmosDbPartitionedStorage`: `IStorage` implementation backed by Azure Cosmos DB with partition key support
- `CosmosDbPartitionedStorageOptions`: Configuration options for the Cosmos DB endpoint, database, and container

See [Agents SDK storage overview](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage) for more information.
