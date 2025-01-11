# Microsoft.Agents.Storage.Transcript

The [Transcript](https://github.com/microsoft/Agents/blob/main/specs/transcript/transcript.md) log of conversational actions made by humans and automated software. Transcript extends the Activity Protocol to include tracing generated during the processing of those activities.

## Main Types

- ITranscriptLogger
- MemoryTranscriptStore: For testing purposes
- BlobsTranscriptStore: For production purposes
- TranscriptLoggerMiddleware: For automatic logging of incoming and outgoing Activities.