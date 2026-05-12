# Microsoft.Agents.Storage.Blobs

## About

Azure Blob Storage implementation of `IStorage` for the Microsoft 365 Agents SDK. Suitable for production deployments requiring persistent, scalable conversation state storage.

Also includes `BlobsTranscriptStore` for storing conversation transcripts in Azure Blob Storage.

## Main Types

- `BlobsStorage`: `IStorage` implementation backed by Azure Blob Storage
- `BlobsTranscriptStore`: Transcript store backed by Azure Blob Storage

See [Agents SDK storage overview](https://learn.microsoft.com/en-us/microsoft-365/agents-sdk/storage) for more information.
