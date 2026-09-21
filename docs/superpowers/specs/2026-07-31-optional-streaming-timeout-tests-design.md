# Optional Streaming Timeout Tests Design

## Scope

Make the five timeout and keep-alive tests added for PR #950 opt-in so they do not run
automatically with the normal `StreamingResponseTests` suite.

## Design

Add a test-local `OptionalStreamingTimeoutFactAttribute` derived from xUnit's
`FactAttribute`. The attribute will set `Skip` unless the environment variable
`XUNITSTREAMINGTIMEOUTTESTENABLED` is exactly `1`.

Apply the attribute to:

- `SendStreamTimedOutNotification_DisablesStreaming_AndFinalResponseIsStillSent`
- `ChannelStreamingTimeout_UpdatesCheckpointAndFinalMessage`
- `M365Copilot_IdleStream_SendsConfiguredWorkingNotice`
- `M365Copilot_TimeoutWithoutText_SendsFinalTimeoutMessage`
- `M365Copilot_TimeoutWithBufferedText_SendsTerminatingActivitiesAndFinalResponse`

No production code, project configuration, or CI configuration will change.

## Execution

Normal test runs discover the five tests as skipped. Developers can run them explicitly
from PowerShell with:

```powershell
$env:XUNITSTREAMINGTIMEOUTTESTENABLED = '1'
dotnet test src\tests\Microsoft.Agents.Builder.Tests\Microsoft.Agents.Builder.Tests.csproj `
  --filter "FullyQualifiedName~StreamingResponseTests"
```

Removing the environment variable restores the default skipped behavior.

## Validation

Run the filtered test class without the environment variable and verify five skipped tests.
Then run it with the environment variable set and verify all tests pass on both target
frameworks.
