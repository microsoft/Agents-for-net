# Microsoft.Agents.Storage.Transcript

## About

Provides transcript logging for the Microsoft 365 Agents SDK. A [transcript](https://github.com/microsoft/Agents/blob/main/specs/transcript/transcript.md) is a log of conversational activities — including tracing generated during activity processing — made by humans and automated software.

## Main Types

- `ITranscriptLogger`: Interface for logging activities to a transcript store
- `TranscriptLoggerMiddleware`: Middleware that automatically logs incoming and outgoing activities
- `MemoryTranscriptStore`: In-memory transcript store for testing
- `BlobsTranscriptStore`: Azure Blob Storage transcript store for production (in `Microsoft.Agents.Storage.Blobs`)
