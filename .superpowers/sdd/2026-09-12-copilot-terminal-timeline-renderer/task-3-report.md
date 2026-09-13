# Task 3 Report: Custom Terminal.Gui timeline view

## Status

Complete.

## Changed Files

- `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalTimelineView.cs`
- `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalTimelineViewTests.cs`
- `.superpowers/sdd/2026-09-12-copilot-terminal-timeline-renderer/task-3-report.md`

## Commits

- `900d14c0` - `feat: render custom terminal timeline`

## Red / Green

### Red 1

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests.SetEntries_ReplacesRenderedStreamingEntryByKey|FullyQualifiedName~TerminalTimelineViewTests.Layout_RewrapsWhenViewportNarrows" --no-restore --nologo`
- Outcome:
  failed at compile time with `CS0246` because `TerminalTimelineView` did not exist yet.

### Green 1

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests.SetEntries_ReplacesRenderedStreamingEntryByKey|FullyQualifiedName~TerminalTimelineViewTests.Layout_RewrapsWhenViewportNarrows" --no-restore --nologo`
- Outcome:
  passed with `Failed: 0, Passed: 2, Skipped: 0, Total: 2`.

### Red 2

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo`
- Outcome:
  two tests failed as expected:
  - `SemanticRoles_MapToAdaptiveTerminalRoles` returned `Normal` instead of the adaptive role mapping.
  - `KeyboardScrolling_PreservesManualPositionAcrossUpdate` snapped back to the bottom instead of preserving manual scroll.

### Red 3

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests.MouseWheelScrolling_MovesOneRowPerWheelEvent" --no-restore --nologo`
- Outcome:
  failed because `NewMouseEvent(...)` was not handled after removing the initial wheel implementation while checking behavior.

### Green 2 / Final Verification

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests|FullyQualifiedName~TimelineScrollStateTests|FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo`
- Outcome:
  passed with `Failed: 0, Passed: 27, Skipped: 0, Total: 27`.

## Self-review

- The view keeps a single `TimelineLayoutResult` as the source of truth for both drawing and the `RenderedLines` test seam, matching the requirement to avoid a separate render path.
- `SetEntries` snapshots the latest entry list with `ToArray()`, rebuilds layout immediately, updates scroll dimensions through `TimelineScrollState`, and requests redraw.
- Width changes rebuild the same layout in `OnViewportChanged` and `OnDrawingContent`, so both layout-driven tests and live drawing use the same wrapped output.
- Semantic colors are resolved through `VisualRole` and `GetAttributeForRole(...)`, which lets Terminal.Gui decide the concrete attributes from the active scheme.
- Keyboard and mouse scrolling both flow through `TimelineScrollState`, so follow-latest and manual scroll preservation stay consistent across updates.

## Concerns

- The view is intentionally not integrated into `TerminalChatApplication` yet, per task scope.
- Mouse wheel behavior is covered by an additional focused test because the required runtime behavior was part of the production contract even though the brief’s initial sample tests only exercised keyboard scrolling.

## Final Commit

- `900d14c0` - `feat: render custom terminal timeline`

## Round 1 Fixes

### Scope

- Addressed follow-latest recovery after resize clamping reaches the new bottom.
- Updated mouse wheel detection to honor combined modifier flags.
- Made the rendered line collection immutable at the storage seam used for drawing.

### Red 4

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~SetDimensions_WhenTallerViewportClampsToBottom_TracksLatestOnSubsequentGrowth|FullyQualifiedName~ResizeToTallerViewport_WhenClampReachesBottom_FollowsAppendedEntries|FullyQualifiedName~MouseWheelScrolling_HandlesWheelFlagsCombinedWithModifiers|FullyQualifiedName~RenderedLines_DoesNotAllowMutationThroughCollectionCast" --no-restore --nologo`
- Outcome:
  failed with 4 targeted regressions:
  - `SetDimensions_WhenTallerViewportClampsToBottom_TracksLatestOnSubsequentGrowth`: `IsFollowingLatest` stayed `false` after resize clamped offset to the new bottom.
  - `ResizeToTallerViewport_WhenClampReachesBottom_FollowsAppendedEntries`: append after taller resize left the view at a stale offset.
  - `MouseWheelScrolling_HandlesWheelFlagsCombinedWithModifiers`: wheel events with `Shift` / `Ctrl` flags were not handled.
  - `RenderedLines_DoesNotAllowMutationThroughCollectionCast`: the rendered line collection accepted mutation via `ICollection<TimelineLine>`.

### Green 3

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~SetDimensions_WhenTallerViewportClampsToBottom_TracksLatestOnSubsequentGrowth|FullyQualifiedName~ResizeToTallerViewport_WhenClampReachesBottom_FollowsAppendedEntries|FullyQualifiedName~MouseWheelScrolling_HandlesWheelFlagsCombinedWithModifiers|FullyQualifiedName~RenderedLines_DoesNotAllowMutationThroughCollectionCast" --no-restore --nologo`
- Outcome:
  passed with `Failed: 0, Passed: 4, Skipped: 0, Total: 4`.

### Green 4 / Layout-Scroll-View Verification

- Command:
  `dotnet test C:\s\gh\an\5\.worktrees\copilot-terminal-timeline\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests|FullyQualifiedName~TimelineScrollStateTests|FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo`
- Outcome:
  passed with `Failed: 0, Passed: 31, Skipped: 0, Total: 31`.

### Self-review

- `TimelineScrollState.SetDimensions(...)` now re-enters follow-latest mode whenever a manual offset is clamped onto the current bottom during resize, which keeps later appends anchored correctly without changing manual-scroll behavior away from the bottom.
- `TerminalTimelineView.OnMouseEvent(...)` now checks wheel flags bitwise, so modifier combinations still scroll while unrelated mouse events continue through the base path.
- `TerminalTimelineLayout.Build(...)` now stores rendered lines as the same array-backed immutable collection consumed by the view, closing the `ICollection<T>` mutation hole without introducing a second render representation.
