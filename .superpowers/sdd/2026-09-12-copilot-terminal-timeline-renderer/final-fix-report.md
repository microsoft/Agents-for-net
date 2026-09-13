# Final Fix Wave Report

## Status

Complete. All three final-review findings are fixed with focused red/green
coverage. The complete terminal test project passes, the terminal sample builds
with zero warnings, and `appsettings.json` is unchanged.

## Inputs reviewed

- `docs/superpowers/specs/2026-09-12-copilot-terminal-timeline-renderer-design.md`
- `docs/superpowers/plans/2026-09-12-copilot-terminal-timeline-renderer.md`
- `.superpowers/sdd/2026-09-12-copilot-terminal-timeline-renderer/progress.md`
- Task briefs and reports for Tasks 1 through 5
- The cumulative branch review package
  `review-3166a29e..5253a34a.diff`
- Current terminal production code and terminal test project

The worktree started clean on
`users/mbarbour/interactiveCLI-timeline`. Its baseline terminal suite passed
139 tests.

## Finding 1: Chrome-free default timeline

### Root cause

`TerminalOptions.Parse([])` selected `TerminalLayout.Tabs`, and
`TerminalChatApplication.CreateWindow` implemented that mode with a
Terminal.Gui `Tabs` control containing Chat, Thoughts, Activities, and Help.
The custom timeline existed inside Chat, but the default top-level composition
still rendered the tab strip and its three rows of chrome.

### Red tests

Added:

- `CreateWindow_DefaultLayoutUsesChromeFreeTimelineAndHiddenInspectors`
- `DefaultLayout_ShortcutsSwitchVisibleSurfaceAndFocusItsPrimaryControl`

Before the production change, the first test found a `Tabs` descendant in the
default window. The shortcut test observed the old tab collection rather than
one chrome-free surface container with exactly one visible child.

### Fix

- Replaced the default top-level `Tabs` with a focusable, borderless, full-size
  `View`.
- Added Chat, Thoughts, Activities, and Help as full-size child surfaces.
- Made Chat initially visible and the three inspectors initially hidden.
- Added one visibility switch that shows only the requested surface.
- Routed `Ctrl+1` through `Ctrl+4` through that switch, then focused the
  surface's primary control:
  - Chat composer when enabled, otherwise the conversation timeline
  - Thoughts timeline
  - Activities list
  - Help surface
- Made the overlay container focusable so newly revealed children can acquire
  focus in a live Terminal.Gui run.
- Preserved split mode's existing Chat/Thoughts tabs, Activities pane, and Help
  dialog behavior.
- Kept `--layout tabs` command-line compatibility; it now selects the
  chrome-free timeline mode and does not create a tab strip.
- Updated application tests and README wording to describe the acceptance
  behavior rather than the removed default tabs.

## Finding 2: ASCII collapsed-thought output

### Root cause

The collapsed thought summary was assembled with a hard-coded Unicode middle
dot even when `TimelineGlyphSet.Ascii` had been selected for a non-Unicode
output encoding.

### Red test

Added `Build_CollapsedThoughtSummaryUsesAsciiOnlyWithAsciiGlyphs`.

Before the production change, the expected ASCII summary
`Reasoning complete - Ctrl+2 for details` instead contained the Unicode middle
dot. The test also checks every output character is in the ASCII range.

### Fix

Added `DetailSeparator` to `TimelineGlyphSet`:

- Unicode glyphs use the middle dot.
- ASCII glyphs use `-`.

Collapsed thought projection now obtains its separator from the active glyph
set. Unicode output remains unchanged.

## Finding 3: Terminal-cell-aware wrapping

### Root cause

Header truncation, token measurement, and hard slicing counted one column per
`StringInfo` text element. That preserved UTF-16 and grapheme boundaries, but
it treated full-width CJK and emoji graphemes as one terminal cell. Layout
therefore emitted overwide rows and undercounted the timeline's scroll height.

### Red tests

Added:

- `Build_WrapsCjkAtTerminalCellWidth`
- `Build_WrapsEmojiWithoutSplittingCombiningGraphemes`
- `Build_ReplacesSingleGraphemeThatCannotFitOneTerminalCell`

Before the production change:

- three CJK graphemes remained on one four-cell line even though they occupy
  six terminal cells;
- combining graphemes and emoji remained on one overwide line;
- a two-cell CJK grapheme was emitted into a one-cell line.

Each test measures final rows with Terminal.Gui's own `GetColumns()` API and
requires every emitted row to fit the requested width.

### Fix

- Kept `StringInfo.GetTextElementEnumerator` as the only slicing boundary.
- Replaced grapheme-count measurement with `Terminal.Gui.Text.GetColumns()`,
  matching Terminal.Gui drawing behavior for combining marks, full-width CJK,
  emoji, and joined emoji sequences.
- Updated inline normalization column tracking so tabs following wide
  graphemes use the correct tab stop.
- Updated header truncation to stop only between grapheme clusters and by
  terminal-cell width.
- Updated hard token slicing to accumulate terminal cells while preserving
  complete graphemes.
- When a single grapheme is wider than an otherwise empty one-cell viewport,
  emit `?` rather than split the grapheme or produce an overwide row.
- Retained control escaping, literal `\uXXXX` text, tab expansion, newline
  normalization, and surrogate safety.

## TDD evidence

### Focused red

Command:

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~CreateWindow_DefaultLayoutUsesChromeFreeTimelineAndHiddenInspectors|FullyQualifiedName~DefaultLayout_ShortcutsSwitchVisibleSurfaceAndFocusItsPrimaryControl|FullyQualifiedName~Build_CollapsedThoughtSummaryUsesAsciiOnlyWithAsciiGlyphs|FullyQualifiedName~Build_WrapsCjkAtTerminalCellWidth|FullyQualifiedName~Build_WrapsEmojiWithoutSplittingCombiningGraphemes|FullyQualifiedName~Build_ReplacesSingleGraphemeThatCannotFitOneTerminalCell" --no-restore --nologo --verbosity minimal
```

Outcome before production edits: 6 failed, 0 passed. Failures matched the
existing tab strip, Unicode separator, CJK/emoji overflow, and unsplittable
one-cell cases.

### Focused green

The same command after the fixes passed 6 tests with 0 failures.

### Affected-area green

Command:

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalChatApplicationTests|FullyQualifiedName~TerminalTimelineLayoutTests|FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo --verbosity minimal
```

Outcome: 53 passed, 0 failed.

## Final verification

Full terminal test project:

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --no-restore --nologo --verbosity minimal
```

Outcome: 145 passed, 0 failed, 0 skipped.

Terminal sample build:

```powershell
dotnet build src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --no-restore --nologo --verbosity minimal
```

Outcome: build succeeded with 0 warnings and 0 errors.

Repository checks:

- `git diff --check`: clean after line-ending normalization.
- `git diff -- appsettings.json`: empty.
- Changed files are limited to terminal composition, timeline layout, their
  focused tests, the terminal README, and this report.

## Self-review

- The default view tree contains no `Tabs`; split mode still contains the two
  intended Chat/Thoughts tabs.
- All default surfaces use full-size dimensions and exactly one is visible.
- Shortcut behavior is exercised inside a live Terminal.Gui application
  iteration, so focus assertions are not based on post-shutdown state.
- Help is a hidden full-size default surface and remains a modal dialog in
  split mode.
- Display width is measured by the same Terminal.Gui API used to reason about
  drawing, while all cuts remain at .NET grapheme boundaries.
- The one-cell fallback prevents impossible wide-glyph overflow without
  emitting malformed UTF-16 or a partial grapheme.
- Existing split layout, composer, activity inspector, thought projection,
  scrolling, controls, tabs, and literal escape tests remain green.

## Concerns

None open.
