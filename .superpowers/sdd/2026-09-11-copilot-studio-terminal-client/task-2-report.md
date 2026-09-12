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

## Fix Round 2/5

**Finding addressed:** Snapshot cloning now happens inside the journal's guarded path, and clone failures emit the activity record plus a diagnostic instead of aborting before anything is recorded.

**What changed**

- Added an injectable `cloner` delegate to `ActivityJournal`, defaulting to `ProtocolJsonSerializer.CloneTo<Activity>()`.
- Moved snapshot creation inside the guarded serialization path so clone failures are caught with the formatter failures.
- Kept successful clones immutable by storing the cloned `Activity` in the journal record.
- On clone failure, the journal still records the activity metadata, leaves `Activity` null, and appends a diagnostic record.
- Added a regression test that injects a cloner throwing `JsonException` and verifies the activity record, diagnostic record, and non-throwing append behavior.

**Exact Tests and Outcomes**

- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJournalTests.Append_UnsupportedSnapshotPayload_EmitsActivityAndDiagnosticWithoutThrowing"` — initially failed with `System.InvalidOperationException : Timeouts are not supported on this stream.` from `ProtocolJsonSerializer.CloneTo<Activity>()`; passed after the fix.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJournalTests"` — passed.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — passed.

**Commit**

- `8d5fced45a56e1f6ebac106ca223705eaa6078bb` — `fix: guard activity snapshot failures`

**Self-Review**

- Verified clone failures are caught without using a broad catch.
- Verified a clone failure still emits the activity record and diagnostic record in order.
- Verified successful clone paths still keep a frozen `Activity` snapshot in the journal record.
- Verified the journal no longer aborts before recording when snapshot cloning fails.

**Concerns**

- None.
