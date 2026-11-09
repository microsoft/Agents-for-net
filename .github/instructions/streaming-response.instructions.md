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
| DeliveryMode.Stream (SSE transport) | 100ms | Pre-generated GUID |
| M365 Copilot with DeliveryMode.Stream | 100ms | Pre-generated GUID; includes keep-alive and timeout fallback |
| Other / ExpectReplies | N/A | Non-streaming fallback |

## Error Scenarios

- **ContentStreamNotAllowed + "Content stream finished due to exceeded streaming time."** → disables streaming, updates the existing activity with a timeout checkpoint, and later updates it with the final response
- **Other ContentStreamNotAllowed on Teams** → service rejection, not evidence of a user pressing Stop; stops the stream and reports `Error` when ending the failed stream
- **Other ContentStreamNotAllowed on non-Teams channels** → retains the existing client-cancellation interpretation and `UserCancelled` result
- **BadArgument + "streaming api is not enabled"** → disables streaming for this turn (does not cancel)
- **Other errors** → cancels stream; returns `Error`
- **M365 Copilot** → while the queue is empty, sends an inactivity notice after 35 seconds from stream start or the last informative update; at 1 minute 45 seconds it stops streaming and falls back to normal final delivery

## Separate Timing Contracts

- Teams enforces a ten-minute streaming window from the first informative or content update. The SDK reacts to the service timeout; it does not impose a separate Teams lifetime timer.
- `EndStreamTimeout` defaults to two minutes and bounds only the wait for queued updates during `EndStreamAsync` / `ResetAsync`. Do not change it to match the Teams service window.
- Teams no longer exposes the streaming UI Stop feature. Keep application cancellation tokens and the shared result enum; do not infer Teams user intent from service rejection codes.
- M365 Copilot's 35-second notice and 1-minute-45-second fallback are independent of Teams service timing.

## Related Source Files

| Component | Path |
|-----------|------|
| StreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` |
| IStreamingResponse | `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponse.cs` |
| TurnContext | `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` |
| Sequence Diagrams | `docs/streaming-response-sequence-diagram.md` |
