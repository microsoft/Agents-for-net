---
applyTo:
  - "**/StreamingResponse*"
  - "**/IStreamingResponse*"
  - "**/StreamInfo*"
  - "**/StreamTypes*"
  - "**/StreamResults*"
  - "**/LLMClient*"
---

# StreamingResponse Context

When working on streaming message delivery, read `docs/streaming-response-sequence-diagram.md` for the full mermaid sequence diagrams covering all scenarios (streaming channels, non-streaming fallback, error handling).

## Key Design Points

- `StreamingResponseBase` owns buffering, sequencing, the async send loop, and end/reset behavior.
- `StreamingResponse` provides shared Activity Protocol formatting for WebChat, DirectLine, and `DeliveryMode.Stream`.
- `TeamsStreamingResponse` adds Teams defaults, stream identity, feedback metadata, timeout updates, and service-error handling.
- `M365CopilotStreamingResponse` derives from `TeamsStreamingResponse` and adds keep-alive and maximum-duration behavior.
- It sends **intermediate Typing activities** on an interval, giving the UX of a streamed message. Each intermediate contains the **full accumulated text** (not a delta).
- A **final Message activity** is sent with `StreamInfo.StreamType = Final` when `EndStreamAsync()` is called.
- The async delay loop waits until each send completes before scheduling the next interval, preventing overlapping sends.
- **Non-streaming channels** buffer all text and send a single normal message on `EndStreamAsync()` — no send loop runs.
- Teams requires using the `Activity.Id` returned from the first send as the `StreamId` for all subsequent messages.
- `ResetAsync()` clears buffered and synchronization state and restores shared and channel-specific defaults,
  including intervals, stream identity, feedback settings, and M365 Copilot lifecycle timeouts.

## Channel Intervals

| Channel | Interval | Stream Start |
|---------|----------|--------------|
| Teams | 1000ms | StreamId from first response |
| M365 Copilot | 1000ms | StreamId from first response |
| WebChat / DirectLine | 500ms | Pre-generated GUID |
| DeliveryMode.Stream (A2A) | 100ms | Pre-generated GUID |
| Other / ExpectReplies | N/A | Non-streaming fallback |

## Error Scenarios

The following service errors are Teams-specific and are handled by `TeamsStreamingResponse`:

- **ContentStreamNotAllowed** → user canceled on client; returns `UserCancelled`
- **BadArgument + "streaming api is not enabled"** → disables streaming for this turn (does not cancel)
- **ContentStreamNotAllowed + exceeded streaming time** → updates the existing activity and falls back to non-streaming

WebChat, DirectLine, `DeliveryMode.Stream`, and other errors are treated as transport failures; the stream is canceled and returns `Error`.

## Related Source Files

| Component | Path |
|-----------|------|
| Shared loop | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs` |
| Activity Protocol response | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` |
| Teams response | `src/libraries/Builder/Microsoft.Agents.Builder/TeamsStreamingResponse.cs` |
| M365 Copilot response | `src/libraries/Builder/Microsoft.Agents.Builder/M365CopilotStreamingResponse.cs` |
| IStreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponse.cs` |
| TurnContext | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| Sequence Diagrams | `docs/streaming-response-sequence-diagram.md` |
