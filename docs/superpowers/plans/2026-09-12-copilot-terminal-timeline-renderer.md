# Copilot-Style Terminal Timeline Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the default Terminal.Gui Markdown dashboard with the approved adaptive, glyph-led Copilot-style conversation timeline.

**Architecture:** Keep protocol interpretation and keyed conversation state unchanged, add a pure layout/projection layer, and render its output through a focused custom Terminal.Gui `View`. Keep scrolling state separate from drawing so wrapping, streaming replacement, resize, and follow-latest behavior are deterministic and unit-testable.

**Tech Stack:** .NET 10, C#, Terminal.Gui 2.5.0, xUnit

**Spec:** `docs/superpowers/specs/2026-09-12-copilot-terminal-timeline-renderer-design.md`

## Global Constraints

- Keep `TerminalPresenter`, `ActivityInterpreter`, and `ActivityJournal` contracts unchanged.
- Do not add a new terminal UI dependency.
- Derive colors from Terminal.Gui semantic roles; do not force a dark palette.
- Keep full Thoughts and Activities inspectors available through `Ctrl+2` and `Ctrl+3`.
- Show only meaningful interpreter-promoted events inline, while retaining every record in Activities.
- Preserve update-by-key streaming behavior and active suggested-action behavior.
- Escape or replace control characters before measuring or drawing.
- Build the terminal sample with zero warnings and errors.

## File structure

- Create `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalTimelineLayout.cs`
  - Pure projection, sanitization, wrapping, glyph selection, thought collapsing, and semantic roles.
- Create `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TimelineScrollState.cs`
  - Pure scroll/follow-latest calculations.
- Create `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalTimelineView.cs`
  - Terminal.Gui drawing, keyboard/mouse input, viewport layout, and adaptive attributes.
- Modify `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalChatApplication.cs`
  - Compose timeline views, unboxed conversation surface, composer, status/footer, and inspector navigation.
- Modify `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/Models/ChatEntry.cs`
  - Add no protocol fields; only add a rendering-neutral helper record if required by the final projection boundary.
- Create `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalTimelineLayoutTests.cs`
  - Projection, sanitization, wrapping, glyph fallback, and thought behavior.
- Create `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TimelineScrollStateTests.cs`
  - Scroll clamping and follow-latest behavior.
- Create `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalTimelineViewTests.cs`
  - View integration, resize, key/mouse scrolling, and update behavior.
- Modify `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalChatApplicationTests.cs`
  - Application composition, keyboard navigation, composer, and inspector regressions.
- Modify `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalChatStateTests.cs`
  - Remove Markdown-output assertions and retain keyed-state/action tests.
- Modify `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/README.md`
  - Describe the actual timeline, controls, theme behavior, and inspector access.

---

### Task 1: Pure timeline projection and wrapping

**Files:**
- Create: `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalTimelineLayout.cs`
- Create: `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalTimelineLayoutTests.cs`

**Interfaces:**
- Consumes: `ChatEntry`, `ChatEntryKind`, and `ChatEntry.IsTransient`.
- Produces:
  - `internal enum TimelineRole { Normal, Accent, Success, Muted, Warning }`
  - `internal sealed record TimelineSpan(string Text, TimelineRole Role)`
  - `internal sealed record TimelineLine(string EntryKey, IReadOnlyList<TimelineSpan> Spans)`
  - `internal sealed record TimelineLayoutResult(IReadOnlyList<TimelineLine> Lines, IReadOnlyDictionary<string, TimelineRowRange> EntryRows)`
  - `internal readonly record struct TimelineRowRange(int Start, int Count)`
  - `internal sealed record TimelineGlyphSet(string User, string Agent, string Status, string Thought, string Event, string Attachment, string Diagnostic)`
  - `internal static TimelineGlyphSet ForEncoding(Encoding encoding)`
  - `TerminalTimelineLayout.Build(IReadOnlyList<ChatEntry> entries, int width, TimelineGlyphSet glyphs, bool collapseCompletedThoughts)`

- [ ] **Step 1: Write failing semantic-projection tests**

Create `TerminalTimelineLayoutTests.cs` with focused tests:

```csharp
[Fact]
public void Build_AssignsSemanticGlyphsAndRoles()
{
    ChatEntry[] entries =
    [
        Entry("u", ChatEntryKind.User, "You", "Question"),
        Entry("a", ChatEntryKind.Agent, "Agent", "Answer"),
        Entry("s", ChatEntryKind.Status, "Status", "Working", isTransient: true),
        Entry("e", ChatEntryKind.Event, "Event", "Tool finished"),
        Entry("x", ChatEntryKind.Diagnostic, "Error", "Failed")
    ];

    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        entries,
        width: 40,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: true);

    Assert.Equal(
        [">  You", "Question", "", "●  Agent", "Answer", "", "○  Status", "Working",
         "", "↗  Event", "Tool finished", "", "!  Error", "Failed"],
        result.Lines.Select(PlainText));
    Assert.Equal(TimelineRole.Accent, result.Lines[0].Spans[0].Role);
    Assert.Equal(TimelineRole.Success, result.Lines[3].Spans[0].Role);
    Assert.Equal(TimelineRole.Muted, result.Lines[6].Spans[0].Role);
    Assert.Equal(TimelineRole.Warning, result.Lines[12].Spans[0].Role);
}

[Fact]
public void Build_UsesAsciiGlyphSetWhenRequested()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("a", ChatEntryKind.Agent, "Agent", "Answer")],
        width: 40,
        TimelineGlyphSet.Ascii,
        collapseCompletedThoughts: true);

    Assert.Equal("*  Agent", PlainText(result.Lines[0]));
}

[Fact]
public void ForEncoding_UsesAsciiFallbackForNonUnicodeOutput()
{
    Assert.Equal(TimelineGlyphSet.Ascii, TimelineGlyphSet.ForEncoding(Encoding.ASCII));
    Assert.Equal(TimelineGlyphSet.Unicode, TimelineGlyphSet.ForEncoding(Encoding.UTF8));
}
```

Define test-local `Entry` and `PlainText` helpers with concrete `ChatEntry`
construction and `string.Concat(line.Spans.Select(span => span.Text))`.

- [ ] **Step 2: Run the semantic tests and verify they fail**

Run:

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests.Build_AssignsSemanticGlyphsAndRoles|FullyQualifiedName~TerminalTimelineLayoutTests.Build_UsesAsciiGlyphSetWhenRequested" --no-restore --nologo
```

Expected: compilation fails because `TerminalTimelineLayout` and its result
types do not exist.

- [ ] **Step 3: Implement the minimal projection types**

Create `TerminalTimelineLayout.cs` with the interfaces above. Implement a
kind-to-header mapping:

```csharp
private static (string Glyph, TimelineRole Role) GetHeader(
    ChatEntryKind kind,
    TimelineGlyphSet glyphs) => kind switch
{
    ChatEntryKind.User => (glyphs.User, TimelineRole.Accent),
    ChatEntryKind.Agent => (glyphs.Agent, TimelineRole.Success),
    ChatEntryKind.Status => (glyphs.Status, TimelineRole.Muted),
    ChatEntryKind.Thought => (glyphs.Thought, TimelineRole.Accent),
    ChatEntryKind.Event => (glyphs.Event, TimelineRole.Muted),
    ChatEntryKind.Attachment => (glyphs.Attachment, TimelineRole.Accent),
    ChatEntryKind.Diagnostic => (glyphs.Diagnostic, TimelineRole.Warning),
    _ => (glyphs.Event, TimelineRole.Normal)
};
```

Emit one header line, one body line, and one blank separator per
entry, omitting the final separator. Define `Unicode` and `Ascii` static glyph
sets exactly as asserted by the tests.
`ForEncoding` selects `Unicode` only for UTF-8 output and otherwise selects
`Ascii`; `TerminalChatApplication` will pass `Console.OutputEncoding`.

- [ ] **Step 4: Run the semantic tests and verify they pass**

Run the command from Step 2. Expected: both tests pass.

- [ ] **Step 5: Write failing sanitization, wrapping, and thought tests**

Add:

```csharp
[Fact]
public void Build_SanitizesControlsAndWrapsBodyToWidth()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("a", ChatEntryKind.Agent, "Agent", "alpha beta\u001B gamma")],
        width: 12,
        TimelineGlyphSet.Ascii,
        collapseCompletedThoughts: true);

    Assert.Equal(
        ["*  Agent", "alpha beta", "\\u001B", "gamma"],
        result.Lines.Select(PlainText));
    Assert.All(result.Lines, line => Assert.True(PlainText(line).Length <= 12));
}

[Fact]
public void Build_CollapsesCompletedThoughtButKeepsActiveThoughtExpanded()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [
            Entry("active", ChatEntryKind.Thought, "Reasoning", "Checking account", isTransient: true),
            Entry("done", ChatEntryKind.Thought, "Reasoning", "Compared all records", isTransient: false)
        ],
        width: 80,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: true);

    Assert.Contains(result.Lines, line => PlainText(line) == "Checking account");
    Assert.DoesNotContain(result.Lines, line => PlainText(line) == "Compared all records");
    Assert.Contains(
        result.Lines,
        line => PlainText(line) == "Reasoning complete · Ctrl+2 for details");
}

[Fact]
public void Build_ThoughtInspectorKeepsCompletedThoughtExpanded()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("done", ChatEntryKind.Thought, "Reasoning", "Compared all records")],
        width: 80,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: false);

    Assert.Contains(result.Lines, line => PlainText(line) == "Compared all records");
}

[Theory]
[InlineData(0)]
[InlineData(1)]
[InlineData(2)]
public void Build_NarrowWidthsNeverProduceOverwideLinesOrThrow(int width)
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("a", ChatEntryKind.Agent, "Agent", "longcontent")],
        width,
        TimelineGlyphSet.Ascii,
        collapseCompletedThoughts: true);

    Assert.All(result.Lines, line =>
        Assert.True(PlainText(line).Length <= Math.Max(1, width)));
}
```

- [ ] **Step 6: Run the new tests and verify expected failures**

Run:

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests" --no-restore --nologo
```

Expected: the original tests pass; wrapping, sanitization, and thought-collapse
tests fail because projection still emits raw single-line bodies.

- [ ] **Step 7: Implement sanitization, word wrapping, and thought collapse**

Add private helpers:

```csharp
private static string Sanitize(string value)
private static IEnumerable<string> Wrap(string value, int width)
private static string GetBody(ChatEntry entry, bool collapseCompletedThoughts)
```

`Sanitize` preserves CR/LF/TAB semantics, renders other control characters as
`\uXXXX`, and normalizes line endings. `Wrap` uses words when they fit and
hard-splits a token only when the token itself exceeds the available width.
Clamp width to at least `1`.

Record each entry's row range in `EntryRows`; blank separators are not included
in the entry's `Count`.

- [ ] **Step 8: Run all layout tests**

Run:

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests" --no-restore --nologo
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit the pure projection**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs
git commit -m "feat: add terminal timeline projection" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 2: Deterministic scrolling state

**Files:**
- Create: `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TimelineScrollState.cs`
- Create: `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TimelineScrollStateTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class TimelineScrollState`
  - `internal int Offset { get; }`
  - `internal int MaximumOffset { get; }`
  - `internal bool IsFollowingLatest { get; }`
  - `internal void SetDimensions(int contentHeight, int viewportHeight)`
  - `internal void ScrollBy(int delta)`
  - `internal void ScrollToStart()`
  - `internal void ScrollToEnd()`

- [ ] **Step 1: Write failing scroll-state tests**

```csharp
[Fact]
public void SetDimensions_FollowsNewBottomWhenAlreadyFollowing()
{
    TimelineScrollState state = new();
    state.SetDimensions(contentHeight: 20, viewportHeight: 10);
    state.ScrollToEnd();

    state.SetDimensions(contentHeight: 25, viewportHeight: 10);

    Assert.Equal(15, state.Offset);
    Assert.True(state.IsFollowingLatest);
}

[Fact]
public void SetDimensions_PreservesManualScrollWhenContentGrows()
{
    TimelineScrollState state = new();
    state.SetDimensions(contentHeight: 20, viewportHeight: 10);
    state.ScrollToEnd();
    state.ScrollBy(-4);

    state.SetDimensions(contentHeight: 25, viewportHeight: 10);

    Assert.Equal(6, state.Offset);
    Assert.False(state.IsFollowingLatest);
}

[Theory]
[InlineData(-100, 0)]
[InlineData(100, 10)]
public void ScrollBy_ClampsToValidRange(int delta, int expected)
{
    TimelineScrollState state = new();
    state.SetDimensions(contentHeight: 20, viewportHeight: 10);

    state.ScrollBy(delta);

    Assert.Equal(expected, state.Offset);
}
```

- [ ] **Step 2: Run tests and verify missing-type failure**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TimelineScrollStateTests" --no-restore --nologo
```

Expected: compilation fails because `TimelineScrollState` does not exist.

- [ ] **Step 3: Implement scroll calculations**

Implement all dimensions with `Math.Max(0, ...)`. Capture
`wasFollowingLatest` before recalculating `MaximumOffset`. When following,
assign the new maximum; otherwise clamp the old offset. `ScrollBy` updates
`IsFollowingLatest` by comparing the clamped offset with `MaximumOffset`.

- [ ] **Step 4: Run scroll-state tests**

Run the command from Step 2. Expected: all tests pass.

- [ ] **Step 5: Commit scrolling state**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TimelineScrollState.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TimelineScrollStateTests.cs
git commit -m "feat: add timeline scroll state" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 3: Custom Terminal.Gui timeline view

**Files:**
- Create: `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalTimelineView.cs`
- Create: `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalTimelineViewTests.cs`

**Interfaces:**
- Consumes:
  - `TerminalTimelineLayout.Build(...)`
  - `TimelineScrollState`
- Produces:
  - `internal sealed class TerminalTimelineView : View`
  - `internal bool CollapseCompletedThoughts { get; init; }`
  - `internal TimelineGlyphSet Glyphs { get; init; }`
  - `internal int ScrollOffset { get; }`
  - `internal int MaximumScrollOffset { get; }`
  - `internal IReadOnlyList<TimelineLine> RenderedLines { get; }`
  - `internal void SetEntries(IReadOnlyList<ChatEntry> entries)`

- [ ] **Step 1: Write failing view update and resize tests**

```csharp
[Fact]
public void SetEntries_ReplacesRenderedStreamingEntryByKey()
{
    using TerminalTimelineView view = new() { Width = 40, Height = 10 };
    view.SetEntries([Entry("stream", "Hel", isTransient: true)]);
    view.SetEntries([Entry("stream", "Hello", isTransient: true)]);

    Assert.Single(view.RenderedLines, line => line.EntryKey == "stream"
        && PlainText(line) == "Hello");
    Assert.DoesNotContain(view.RenderedLines, line => PlainText(line) == "Hel");
}

[Fact]
public void Layout_RewrapsWhenViewportNarrows()
{
    using TerminalTimelineView view = new() { Width = 30, Height = 10 };
    view.SetEntries([Entry("a", "one two three four")]);
    int wideLineCount = view.RenderedLines.Count;

    view.Width = 10;
    view.Layout();

    Assert.True(view.RenderedLines.Count > wideLineCount);
}
```

Expose `RenderedLines` only as an internal immutable test seam representing the
same lines used by drawing; do not maintain a separate test-only render path.

- [ ] **Step 2: Run tests and verify missing-type failure**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests.SetEntries_ReplacesRenderedStreamingEntryByKey|FullyQualifiedName~TerminalTimelineViewTests.Layout_RewrapsWhenViewportNarrows" --no-restore --nologo
```

Expected: compilation fails because `TerminalTimelineView` does not exist.

- [ ] **Step 3: Implement view projection and resize behavior**

Store the latest entries, last layout width, current `TimelineLayoutResult`,
and `TimelineScrollState`. `SetEntries` copies the supplied list with
`ToArray()`, rebuilds layout, updates scroll dimensions, and calls
`SetNeedsDraw()`.

Override `OnDrawingContent(DrawContext context)` and rebuild when
`Viewport.Width` differs from the last layout width. Draw only rows from
`ScrollOffset` through `ScrollOffset + Viewport.Height`. For each span:

```csharp
SetAttribute(GetAttribute(span.Role));
AddStr(span.Text);
```

Use `Move(column, visibleRow)` before each line and clear unused viewport rows
with spaces in the Normal role. Return `true`.

- [ ] **Step 4: Write failing semantic-attribute and scrolling-input tests**

Add:

```csharp
[Fact]
public void SemanticRoles_MapToAdaptiveTerminalRoles()
{
    using TerminalTimelineView view = new();

    Assert.Equal(VisualRole.HotNormal, view.GetVisualRole(TimelineRole.Accent));
    Assert.Equal(VisualRole.Active, view.GetVisualRole(TimelineRole.Success));
    Assert.Equal(VisualRole.Disabled, view.GetVisualRole(TimelineRole.Muted));
    Assert.Equal(VisualRole.HotActive, view.GetVisualRole(TimelineRole.Warning));
}

[Fact]
public void KeyboardScrolling_PreservesManualPositionAcrossUpdate()
{
    using TerminalTimelineView view = new() { Width = 20, Height = 4 };
    view.SetEntries(ManyEntries(10));
    view.NewKeyDownEvent(Key.End);
    view.NewKeyDownEvent(Key.CursorUp);
    int manualOffset = view.ScrollOffset;

    view.SetEntries(ManyEntries(11));

    Assert.Equal(manualOffset, view.ScrollOffset);
}

[Fact]
public void UpdatingLastStreamKeepsLatestLineVisibleWhenFollowing()
{
    using TerminalTimelineView view = new() { Width = 12, Height = 4 };
    view.SetEntries(ManyEntries(8));
    view.NewKeyDownEvent(Key.End);

    view.SetEntries(ManyEntries(8, finalText: "long streaming update wraps"));

    Assert.Equal(view.MaximumScrollOffset, view.ScrollOffset);
}
```

- [ ] **Step 5: Run tests and verify expected failures**

Run:

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo
```

Expected: projection/resize tests pass; semantic-role and input tests fail
because role mapping and key handling are not implemented.

- [ ] **Step 6: Implement adaptive attributes and input**

Map roles through Terminal.Gui `VisualRole` values:

```csharp
internal VisualRole GetVisualRole(TimelineRole role) => role switch
{
    TimelineRole.Accent => VisualRole.HotNormal,
    TimelineRole.Success => VisualRole.Active,
    TimelineRole.Muted => VisualRole.Disabled,
    TimelineRole.Warning => VisualRole.HotActive,
    _ => VisualRole.Normal
};
```

Resolve attributes at draw time with `GetAttributeForRole`, allowing the active
Terminal.Gui scheme and terminal capabilities to determine actual colors.

Override `OnKeyDown(Key key)` for `CursorUp`, `CursorDown`, `PageUp`,
`PageDown`, `Home`, and `End`. Override `OnMouseEvent(Mouse mouse)` for
`WheeledUp` and `WheeledDown`. Each handled action updates
`TimelineScrollState`, calls `SetNeedsDraw()`, sets/returns handled state, and
leaves unrelated input to the base implementation.

- [ ] **Step 7: Run all timeline unit tests**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests|FullyQualifiedName~TimelineScrollStateTests|FullyQualifiedName~TerminalTimelineViewTests" --no-restore --nologo
```

Expected: all selected tests pass.

- [ ] **Step 8: Commit the custom view**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineView.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs
git commit -m "feat: render custom terminal timeline" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 4: Integrate the renderer and approved layout

**Files:**
- Modify: `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/UI/TerminalChatApplication.cs`
- Modify: `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalChatApplicationTests.cs`
- Modify: `src/tests/Microsoft.Agents.CopilotStudio.Terminal.Tests/TerminalChatStateTests.cs`

**Interfaces:**
- Consumes: `TerminalTimelineView.SetEntries(...)`.
- Preserves: `ITerminalView`, `TerminalPresenter`, all existing link/action
  behavior, and `TerminalLayout`.

- [ ] **Step 1: Write failing composition tests**

Replace the earlier superficial timeline assertions with:

```csharp
[Fact]
public void CreateWindow_DefaultLayoutBuildsUnboxedTimelineAndBorderedComposer()
{
    using IApplication application = Application.Create();
    using CancellationTokenSource shutdown = new();
    TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
    using TerminalPresenter presenter = CreatePresenter(terminal);

    using Window window = terminal.CreateWindow(application, presenter, shutdown);

    TerminalTimelineView timeline = Assert.Single(
        Descendants(window).OfType<TerminalTimelineView>(),
        view => view.CollapseCompletedThoughts);
    Assert.DoesNotContain(
        window.SubViews,
        view => view is Tabs || view is FrameView { Title: "_Conversation" });
    Assert.Contains(
        Descendants(window).OfType<FrameView>(),
        frame => frame.Title == "_Message");
    Assert.NotNull(timeline);
}

[Fact]
public void ApplyChatChanges_ShowsActiveThoughtInlineAndInThoughtInspector()
{
    using IApplication application = Application.Create();
    application.Init(DriverRegistry.Names.ANSI);
    using CancellationTokenSource shutdown = new();
    TerminalChatApplication terminal = new(new TerminalOptions(TerminalLayout.Tabs, false));
    using TerminalPresenter presenter = CreatePresenter(terminal);
    using Window window = terminal.CreateWindow(application, presenter, shutdown);

    terminal.ApplyChatChanges(
    [
        new ChatChange(
            ChatChangeKind.Upsert,
            "thought",
            new ChatEntry(
                "thought",
                ChatEntryKind.Thought,
                "Reasoning",
                "Checking account",
                true,
                [],
                [],
                "thought"))
    ]);
    RunOneIteration(application, window);

    TerminalTimelineView[] timelines =
        Descendants(window).OfType<TerminalTimelineView>().ToArray();
    TerminalTimelineView conversation =
        Assert.Single(timelines, view => view.CollapseCompletedThoughts);
    TerminalTimelineView thoughts =
        Assert.Single(timelines, view => !view.CollapseCompletedThoughts);
    Assert.Contains(conversation.RenderedLines, line => PlainText(line) == "Checking account");
    Assert.Contains(thoughts.RenderedLines, line => PlainText(line) == "Checking account");
}
```

Add a test-local `PlainText(TimelineLine line)` helper implemented as
`string.Concat(line.Spans.Select(span => span.Text))`.

- [ ] **Step 2: Run composition tests and verify failures**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~CreateWindow_DefaultLayoutBuildsUnboxedTimelineAndBorderedComposer|FullyQualifiedName~ApplyChatChanges_ShowsActiveThoughtInlineAndInThoughtInspector" --no-restore --nologo
```

Expected: failures show the conversation still uses `FrameView`/`Markdown` and
thoughts are excluded from its state.

- [ ] **Step 3: Replace conversation Markdown with timeline views**

Change fields:

```csharp
private TerminalTimelineView? _transcript;
private TerminalTimelineView? _thoughtTranscript;
```

Construct the main view with `CollapseCompletedThoughts = true` and the Thoughts
inspector with `CollapseCompletedThoughts = false`. Assign both views
`Glyphs = TimelineGlyphSet.ForEncoding(Console.OutputEncoding)`.

Make `_chatState` include every `ChatEntry`. Keep `_thoughtState` filtered to
`ChatEntryKind.Thought`. In `ApplyChatChanges`, call:

```csharp
_transcript?.SetEntries(_chatState.Entries);
_thoughtTranscript?.SetEntries(_thoughtState.Entries);
```

Do not duplicate activities from `ActivityJournal`; only entries emitted by
the interpreter belong in the conversation timeline.

Delete `MarkdownPunctuation`, `Markdown`, `EscapeMarkdown`, and the temporary
Markdown glyph helper from `TerminalChatState`. Update
`TerminalChatStateTests` to retain keyed-state, filtering, links, and actions
coverage; projection and escaping now belong to `TerminalTimelineLayoutTests`.

- [ ] **Step 4: Build the unboxed conversation composition**

Return a plain `View` titled `_Conversation` from `BuildChatView`. It contains:

- a one-line product/connection header using the Accent scheme;
- the `TerminalTimelineView` filling the central area;
- the existing action surface;
- the existing diagnostic status line;
- a `FrameView` titled `_Message` containing the `>` prompt and `TextField`;
- the existing `StatusBar` as the subdued shortcut footer.

When there are no entries, the timeline draws:

```text
Copilot Studio
Connected conversations and streaming activity appear here.
Type a message below to begin.
```

Remove the large Conversation border. Keep Thoughts and Activities as hidden
full-size inspector surfaces in default mode and preserve split-mode geometry.

- [ ] **Step 5: Run composition and navigation tests**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalChatApplicationTests" --no-restore --nologo
```

Expected: all application tests pass, including composer, actions, links,
startup, `Ctrl+1`/`Ctrl+2`/`Ctrl+3`, and inspector behavior.

- [ ] **Step 6: Commit application integration**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatStateTests.cs
git commit -m "feat: integrate Copilot-style terminal timeline" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

### Task 5: Documentation and final verification

**Files:**
- Modify: `src/samples/CopilotStudioClient/CopilotStudioClient.Terminal/README.md`

**Interfaces:**
- Verifies all interfaces produced by Tasks 1-4.
- Produces no new production API.

- [ ] **Step 1: Run the full terminal test project**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --no-restore --nologo
```

Expected: all tests pass with zero warnings. This includes narrow widths,
resize, follow-latest, manual scrolling, sanitization, glyph fallback,
streaming replacement, thought collapse, navigation, composer, actions, links,
startup, and inspector coverage introduced in Tasks 1-4.

- [ ] **Step 2: Update the README to match the implemented UI**

Document:

- the glyph-led conversation timeline;
- adaptive terminal colors;
- active-thought inline display and completed-thought summary;
- full Thoughts and Activities inspectors;
- scroll keys and mouse wheel;
- `--layout split`;
- composer and shortcut keys.

Remove wording that describes the default as tabs or as a Markdown transcript.

- [ ] **Step 3: Build the sample**

```powershell
dotnet build samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --no-restore --nologo
```

Expected: build succeeds with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Run the complete terminal test project again after docs and cleanup**

```powershell
dotnet test tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --no-build --no-restore --nologo
```

Expected: all tests pass.

- [ ] **Step 5: Confirm the final diff excludes local configuration**

```powershell
git status --short
git diff --check
git diff -- src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\appsettings.json
```

Do not stage or modify the user's `appsettings.json`. Confirm every other
changed file belongs to this plan.

- [ ] **Step 6: Commit final documentation**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md
git commit -m "docs: describe terminal timeline experience" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
