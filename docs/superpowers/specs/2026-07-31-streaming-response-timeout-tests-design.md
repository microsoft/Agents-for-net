# Streaming Response Timeout Tests Design

## Scope

Add focused unit coverage to `StreamingResponseTests.cs` for the behaviors identified in
PR #950's review comment. Production code remains unchanged.

## Test Strategy

The tests will use the existing `TurnContext.StreamingResponse` public surface and mocked
`IChannelAdapter` operations for observable assertions. Small test-only reflection helpers
will set private timeout state or duration fields where waiting for the real 35-second or
105-second thresholds would make the tests slow and unreliable.

Reflection will be limited to named fields on the concrete internal `StreamingResponse`
instance. Helpers will fail immediately if a field is renamed or cannot be found.

## Coverage

1. Verify `SendStreamTimedOutNotification` sends the supplied stop notification, disables
   streaming for the turn, and allows subsequently buffered content to be delivered when
   `EndStreamAsync` completes.
2. Simulate a channel streaming-timeout response and verify timeout checkpoints and the
   eventual final message use activity updates rather than creating additional sends.
3. Force the M365 Copilot working-notice threshold and verify the configured
   `StreamingTakingTooLongMessage` is emitted as an informative streaming activity.
4. Force the M365 Copilot overall timeout with no buffered text and verify the timeout
   message is sent and streaming is disabled.
5. Force the M365 Copilot overall timeout with buffered text and verify the terminating
   timeout activities are sent and the buffered final response remains deliverable through
   the non-streaming path.

## Synchronization and Assertions

Tests will use adapter callbacks and the existing condition-based wait helpers rather than
fixed sleeps. Assertions will distinguish `SendActivitiesAsync` from `UpdateActivityAsync`,
inspect activity type and `StreamInfo`, and confirm `IsStreamingChannel` transitions.

## Validation

Run the targeted `Microsoft.Agents.Builder.Tests` project filtered to
`StreamingResponseTests`. If the target frameworks require separate execution, run the
supported .NET target that exercises the modified test file.
