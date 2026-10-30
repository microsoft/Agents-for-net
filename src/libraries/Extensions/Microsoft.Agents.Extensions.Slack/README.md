# Microsoft.Agents.Extensions.Slack

## About

Contains the definitions for SlackAgentExtension and slack API client.

## Native streaming

`ITurnContext.StreamingResponse` selects the native Slack response through
`IStreamingResponseFactory`. It uses `SlackStream` and serializes calls to `chat.startStream`,
`chat.appendStream`, and `chat.stopStream`; it does not inherit the Activity Protocol interval loop.
`Interval`, `InitialDelay`, and the former `Slack:Streaming` interval configuration are not used.

Channel streams require recipient user/team IDs from the incoming Slack payload. Native requests
share rate-aware pacing per app/workspace/method. Explicit throttling is retried up to three
attempts using `Retry-After`. Other failures are exposed through `SlackResponseException`, including
`Method`, `StatusCode`, `ErrorCode`, and `RetryAfter`; ambiguous failed POSTs are not blindly retried.

The response rolls over into another message in the same thread before 12,000 accumulated markdown
characters or a nine-minute safety window below Slack's ten-minute lifetime. Intermediate closures
keep the agent session `processing`.
Completion still transitions that session to `active` if an idle rollover left no open stream
or remaining final blocks. Streams resolve the turn-bound `SlackApi` when opened so native
requests retain the extension's typing-timer reset callback.

### Portable output

- `AddAttachment` supports publicly accessible PNG/JPEG/GIF images, basic Hero/Thumbnail cards with
  OpenUrl buttons, and download links for other URL-backed files. Unsupported cards/actions are
  rejected explicitly. Card-wide tap actions, native file uploads, and Adaptive Card conversion are
  not supported.
- `AddCitation` produces stable numbered references and a deduplicated Sources block. Only referenced
  sources are included when answer text exists; citation-only responses display their supplied sources.
  Split citation markers are normalized before emission. No Activity Protocol AI entity is sent.
- `FeedbackLoopEnabled` adds optional feedback buttons alongside attachments and source blocks.
- Attachment-only and citation-only responses can complete without queuing dummy text.

Use `AddAttachment` and `AddCitation` instead of the obsolete, Activity Protocol-specific
`FinalMessage` override. Activity Protocol AI-label and sensitivity-label metadata are not emitted.

Always await `EndStreamAsync`. A failed final stop retains its remote handle, allowing another
completion attempt. A drain timeout cancels and joins the writer and attempts to close any known
remote stream, but does not replay a canceled POST whose acceptance is unknown. `ResetAsync`
clears per-response content and metadata after the previous sequence has finished and known remote
resources have been closed. If Slack expires a message during finalization, uncommitted blocks move
to a new message without replaying accepted answer text; repeated expiry and unexpectedly closed
streams terminate with `Error` rather than retrying the same timestamp indefinitely.
