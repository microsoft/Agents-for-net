# Task 3 Report

## Outcome

Implemented presentation-neutral chat records and recursive adaptive-card `Action.OpenUrl` extraction for the Copilot Studio terminal client.

## Files

- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Models/ChatEntry.cs`
- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Protocol/AdaptiveCardLinkExtractor.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/AdaptiveCardLinkExtractorTests.cs`

## Decisions

- Added the requested internal chat record types in a single model file so the terminal client can keep presentation-neutral state without extra wiring.
- Implemented `AdaptiveCardLinkExtractor.Extract(object?)` to normalize content from `string`, `JsonElement`, `JsonDocument`, or any other serializable runtime object through `ProtocolJsonSerializer.SerializationOptions`.
- Traversal is fully recursive across arrays and objects, so nested `ActionSet` and top-level card actions both contribute links.
- Link detection is case-insensitive for `type`, `title`, and `url`, but only accepts absolute `http` or `https` targets.
- Malformed JSON and unsupported runtime values return a `LinkExtractionResult.Error` message; valid action-free adaptive cards succeed with zero links and no error.

## Exact Tests and Outcomes

- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~AdaptiveCardLinkExtractorTests"` — initially failed before implementation with missing `ChatLink`, `LinkExtractionResult`, and `AdaptiveCardLinkExtractor`; passed after the fix.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — passed, 15 tests total.

## Commit

- `76354481a1f5dba629dab35682b8dbe0a3783906` — `feat: extract adaptive card links`

## Self-Review

- Verified nested `Action.OpenUrl` entries are discovered in both `body` and top-level `actions`.
- Verified action-free adaptive cards containing `TextBlock` and `FactSet` elements return an empty link set with no error.
- Verified malformed JSON produces an error result instead of throwing.
- Verified runtime-object normalization uses the protocol serializer path, not a string-only shortcut.

## Concerns

- None for this task.

## Round 1/5 Fix

- Hardened `AdaptiveCardLinkExtractor` so `type`, `url`, and `title` are only read when the JSON value is actually a string.
- Malformed `type` or `url` values now skip just that action object instead of throwing; malformed `title` values fall back to the URL text.
- Added regression coverage for non-string `type`, `url`, and `title`, including a malformed nested action alongside valid `Action.OpenUrl` entries.

## Exact Tests and Outcomes

- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~AdaptiveCardLinkExtractorTests"` — passed, 7 tests total.
- `dotnet test C:\s\gh\an\5\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — passed, 18 tests total.

## Commit

- `d3597838797796053a67fafb805b4e3811f2870e` — `fix: harden adaptive card link extraction`

## Self-Review

- Verified malformed action objects are skipped without aborting recursive traversal.
- Verified valid links still surface when a nested sibling action is malformed.
- Verified title fallback preserves a useful link label without throwing.

## Concerns

- None for this round.
