---
applyTo:
  - "**/CloudAdapter*"
  - "**/ChannelResponseQueue*"
  - "**/ActivityResponseHandler*"
  - "**/ChannelServiceAdapterBase*"
  - "**/Hosting/AspNetCore/**"
  - "**/MiddlewareSet*"
  - "**/TurnContext*"
---

# CloudAdapter Pipeline Context

When working on the request processing pipeline, delivery modes, or response handling, read `docs/full-cloudadapter-sequence-diagram.md` for the full mermaid sequence diagram.

## Key Design Points

- **Normal delivery**: `HostResponseAsync` returns `false` → response sent via `ConnectorClient`. HTTP returns 202 immediately (fire-and-forget path).
- **Stream delivery**: `HostResponseAsync` returns `true` → response queued into `ChannelResponseQueue` → HTTP thread writes SSE events directly to `HttpResponse.Body` (blocking path).
- `ChannelResponseQueue` is a producer/consumer bridge using an unbounded `Channel<IActivity>` between the background agent thread and the HTTP response thread.
- Middleware chain is recursive — each middleware calls `next()` to invoke the next in the pipeline.

## Related Source Files

| Component | Path |
|-----------|------|
| CloudAdapter | `src/libraries/Hosting/AspNetCore/CloudAdapter.cs` |
| ChannelResponseQueue | `src/libraries/Hosting/AspNetCore/ChannelResponseQueue.cs` |
| ActivityResponseHandler (SSE writer) | `src/libraries/Hosting/AspNetCore/ActivityResponseHandler.cs` |
| ChannelServiceAdapterBase | `src/libraries/Builder/Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs` |
| TurnContext | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| MiddlewareSet | `src/libraries/Builder/Microsoft.Agents.Builder/MiddlewareSet.cs` |
| Sequence Diagram | `docs/full-cloudadapter-sequence-diagram.md` |
