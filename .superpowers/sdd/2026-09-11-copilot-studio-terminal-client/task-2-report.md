# Task 2 Report

## Outcome

Implemented the terminal journal and formatter for Copilot Studio client activity capture.

## Files

- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Models/ActivityRecord.cs`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Protocol/ActivityJsonFormatter.cs`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Protocol/ActivityJournal.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/ActivityJsonFormatterTests.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/ActivityJournalTests.cs`

## Decisions

- Added `ActivityRecord` as an immutable internal record with sequence, direction, timestamp, type, summary, activity payload, JSON payload, and diagnostic severity.
- Implemented `ActivityJsonFormatter.Format(Activity)` by serializing through `ProtocolJsonSerializer.ToJson(...)` and reformatting the parsed JSON with local indented `JsonSerializerOptions`.
- Implemented `ActivityJournal` with a single lock for sequence allocation and list mutation, and raised `RecordAdded` only after the lock is released.
- Kept the event API name `RecordAdded` and used a custom delegate shape so handlers can use the brief's `(_, record)` subscription pattern.
- On serialization failure, the journal still records the activity with `Json == null` and appends a diagnostic record containing the formatter error message.

## Exact Tests and Outcomes

- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJsonFormatterTests|FullyQualifiedName~ActivityJournalTests"` — passed
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — passed

## Commit

- `92ddec65d3c1801e813b2670335d354da6b0903a` — `feat: add terminal activity journal`

## Self-Review

- Verified sequence numbers stay stable under concurrent appends.
- Verified the formatter emits protocol field names and indented JSON.
- Verified a formatter failure preserves the activity record and emits a diagnostic record afterward.
- Verified `Snapshot()` returns a copy of the journal contents rather than the live list.

## Concerns

- None for this round.

## Fix Round 1/5

**Finding addressed:** `ActivityRecord` now stores a frozen `Activity` snapshot instead of the caller's original mutable instance, and the journal formats and summarizes the snapshot copy.

**Commands and outcomes**

- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJournalTests.Append_SnapshotsActivityBeforeCallerMutatesIt"` — failed before the fix with `Assert.NotSame() Failure: Values are the same instance`.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJsonFormatterTests|FullyQualifiedName~ActivityJournalTests"` — passed after the fix.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — passed after the fix.

**Commit**

- `24cfab1bd18da252051bb5a52dd692352711fcf5` — `fix: snapshot terminal activities before journaling`

**Self-Review**

- Verified `ActivityJournal.Append` clones the incoming `Activity` before formatting or storing it.
- Verified the regression test covers caller mutation after append, including the emitted record and the journal snapshot.
- Verified the existing serialization-failure diagnostic path still records a diagnostic activity instead of silently succeeding.
