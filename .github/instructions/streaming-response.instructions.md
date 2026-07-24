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

- `StreamingResponse` is an **internal class** exposed via `ITurnContext.StreamingResponse` (created per-turn in `TurnContext`).
- It sends **intermediate Typing activities** on a timer interval, giving the UX of a streamed message. Each intermediate contains the **full accumulated text** (not a delta).
- A **final Message activity** is sent with `StreamInfo.StreamType = Final` when `EndStreamAsync()` is called.
- The timer is **one-shot, re-armed after each successful send** — prevents overlapping sends if `SendActivityAsync` takes longer than `Interval`.
- **Non-streaming channels** buffer all text and send a single normal message on `EndStreamAsync()` — no timer runs.
- Teams requires using the `Activity.Id` returned from the first send as the `StreamId` for all subsequent messages.
- `ResetAsync()` allows reusing the stream for multiple streaming sequences in one turn.

## Channel Intervals

| Channel | Interval | Stream Start |
|---------|----------|--------------|
| Teams | 1000ms | StreamId from first response |
| WebChat / DirectLine | 500ms | Pre-generated GUID |
| DeliveryMode.Stream (A2A) | 100ms | Pre-generated GUID |
| Other / ExpectReplies | N/A | Non-streaming fallback |

## Error Scenarios

- **ContentStreamNotAllowed** → user canceled on client; returns `UserCancelled`
- **BadArgument + "streaming api is not enabled"** → disables streaming for this turn (does not cancel)
- **Other errors** → cancels stream; returns `Error`

## Related Source Files

| Component | Path |
|-----------|------|
| StreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` |
| IStreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponse.cs` |
| TurnContext | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| Sequence Diagrams | `docs/streaming-response-sequence-diagram.md` |
