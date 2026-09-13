# Copilot Studio Terminal Shell Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the stock Terminal.Gui chrome with the approved Copilot-style shell, portable F-key navigation, explicit adaptive colors, rich Markdown timeline rendering, and a usable activity inspector.

**Architecture:** Keep Terminal.Gui 2.5.0 for the application lifecycle, keyboard routing, focus, resizing, input editing, and drawing. Add focused palette, Markdown, chrome, shell-layout, and activity-layout units around the existing keyed timeline and state models, then compose them from `TerminalChatApplication`.

**Tech Stack:** .NET 10, C#, Terminal.Gui 2.5.0, xUnit

**Spec:** `docs\superpowers\specs\2026-09-13-terminal-shell-redesign-design.md`

## Global Constraints

- Keep Terminal.Gui at version `2.5.0`; do not add a console UI or Markdown package.
- Keep `ChatEntry.Key` as the streaming replacement identity.
- Preserve terminal-cell measurement, grapheme boundaries, tab expansion, control-character escaping, and ASCII fallback.
- Use `F1`, `F2`, `F3`, `F4`, `Esc`, `Ctrl+C`, and `Ctrl+Q`; remove `Ctrl+1` through `Ctrl+4` from the shell and documentation.
- The persistent navigation header must remain outside all switchable content.
- Do not use `Window`, `FrameView`, `StatusBar`, or `Tabs` for the default shell chrome.
- Keep `TextField` for editing and `TextView` for the read-only JSON inspector.
- Do not interpret raw HTML or implement unsupported CommonMark features.
- Do not modify or stage `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\appsettings.json`.
- All sample builds must complete with zero warnings and errors, and the complete terminal test project must pass.

## File Structure

### New production files

- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalPalette.cs`
  - Owns dark, light, and limited-color semantic attributes and Terminal.Gui control schemes.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalMarkdown.cs`
  - Parses the supported Markdown subset into immutable block and span metadata.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalNavigationView.cs`
  - Draws the persistent F1-F4 navigation row and active-view marker.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalComposerView.cs`
  - Draws the compact Unicode/ASCII input border and owns the existing `TextField`.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalFooterView.cs`
  - Draws the non-interactive muted shortcut footer.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalShellView.cs`
  - Provides the borderless `Runnable`, persistent navigation, surface switching, root key bindings, and focus policy.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalActivityView.cs`
  - Owns responsive list/JSON-pane allocation and semantic selection styling.

### New test files

- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalPaletteTests.cs`
- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalMarkdownTests.cs`
- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalNavigationViewTests.cs`
- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalComposerViewTests.cs`
- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalShellViewTests.cs`
- `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalActivityViewTests.cs`

### Existing files to modify

- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs`
  - Expand semantic roles and preserve styled spans while wrapping Markdown blocks.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineView.cs`
  - Draw explicit palette attributes and text styles.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs`
  - Compose the new shell and remove stock chrome and old key handlers.
- `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md`
  - Document the redesigned shell and portable key map.
- Existing timeline and application test files receive integration and regression coverage.

---

### Task 1: Adaptive Semantic Palette

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalPalette.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalPaletteTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs:9-16`

**Interfaces:**
- Consumes: Terminal.Gui `Attribute`, `Color`, `ColorName16`, `Scheme`, `TextStyle`, `IDriver.DefaultAttribute`, and `IDriver.SupportsTrueColor`.
- Produces:

```csharp
internal enum TimelineRole
{
    Primary,
    Muted,
    User,
    Agent,
    Thought,
    Link,
    ActiveNavigation,
    Warning,
    Error,
    Code
}

[Flags]
internal enum TimelineTextStyle
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Underline = 4
}

internal sealed class TerminalPalette
{
    internal static TerminalPalette Create(
        Terminal.Gui.Drawing.Attribute? terminalDefault,
        bool supportsTrueColor);

    internal bool IsDark { get; }
    internal bool UsesTrueColor { get; }
    internal Terminal.Gui.Drawing.Attribute Get(
        TimelineRole role,
        TimelineTextStyle style = TimelineTextStyle.None);
    internal Scheme CreateControlScheme();
}
```

- `TerminalPalette.Create` treats a non-null `terminalDefault.Background.IsDarkColor()` as the dark/light signal. A null default or `supportsTrueColor == false` uses readable 16-color accents while preserving the supplied/default background.
- `Get` applies `TextStyle.Bold`, `Italic`, and `Underline` without changing the semantic foreground/background.

- [ ] **Step 1: Write failing palette tests**

```csharp
[Fact]
public void Create_UsesDarkTrueColorPaletteForDarkDetectedBackground()
{
    TerminalPalette palette = TerminalPalette.Create(
        new Terminal.Gui.Drawing.Attribute(new Color("#c9d1d9"), new Color("#0d1117")),
        supportsTrueColor: true);

    Assert.True(palette.IsDark);
    Assert.True(palette.UsesTrueColor);
    Assert.NotEqual(palette.Get(TimelineRole.User), palette.Get(TimelineRole.Agent));
    Assert.NotEqual(palette.Get(TimelineRole.Thought), palette.Get(TimelineRole.Warning));
}

[Fact]
public void Create_UsesLightTrueColorPaletteForLightDetectedBackground()
{
    TerminalPalette palette = TerminalPalette.Create(
        new Terminal.Gui.Drawing.Attribute(new Color("#24292f"), new Color("#ffffff")),
        supportsTrueColor: true);

    Assert.False(palette.IsDark);
    Assert.NotEqual(palette.Get(TimelineRole.Primary), palette.Get(TimelineRole.Muted));
    Assert.NotEqual(palette.Get(TimelineRole.ActiveNavigation), palette.Get(TimelineRole.Link));
}

[Theory]
[InlineData(false)]
[InlineData(true)]
public void Create_UsesReadableSixteenColorFallbackWhenDetectionOrTrueColorIsUnavailable(
    bool hasDefault)
{
    Terminal.Gui.Drawing.Attribute? terminalDefault = hasDefault
        ? new Terminal.Gui.Drawing.Attribute(ColorName16.White, ColorName16.Black)
        : null;

    TerminalPalette palette = TerminalPalette.Create(terminalDefault, supportsTrueColor: false);

    Assert.False(palette.UsesTrueColor);
    Assert.All(
        Enum.GetValues<TimelineRole>(),
        role => Assert.True(palette.Get(role).Foreground.IsClosestToNamedColor16(out _)));
}

[Fact]
public void Get_CombinesSemanticColorWithRequestedTextStyle()
{
    TerminalPalette palette = TerminalPalette.Create(null, supportsTrueColor: false);

    Terminal.Gui.Drawing.Attribute value = palette.Get(
        TimelineRole.Link,
        TimelineTextStyle.Bold | TimelineTextStyle.Underline);

    Assert.True(value.Style.HasFlag(TextStyle.Bold));
    Assert.True(value.Style.HasFlag(TextStyle.Underline));
}
```

- [ ] **Step 2: Run the palette tests and verify they fail**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalPaletteTests"
```

Expected: FAIL because `TerminalPalette`, the expanded `TimelineRole`, and `TimelineTextStyle` do not exist.

- [ ] **Step 3: Implement the palette**

Use exact GitHub-inspired RGB values for true color and explicit 16-color values for fallback:

```csharp
private static readonly IReadOnlyDictionary<TimelineRole, string> DarkColors =
    new Dictionary<TimelineRole, string>
    {
        [TimelineRole.Primary] = "#c9d1d9",
        [TimelineRole.Muted] = "#8b949e",
        [TimelineRole.User] = "#58a6ff",
        [TimelineRole.Agent] = "#3fb950",
        [TimelineRole.Thought] = "#bc8cff",
        [TimelineRole.Link] = "#58a6ff",
        [TimelineRole.ActiveNavigation] = "#f78166",
        [TimelineRole.Warning] = "#d29922",
        [TimelineRole.Error] = "#f85149",
        [TimelineRole.Code] = "#e6edf3"
    };

private static readonly IReadOnlyDictionary<TimelineRole, string> LightColors =
    new Dictionary<TimelineRole, string>
    {
        [TimelineRole.Primary] = "#24292f",
        [TimelineRole.Muted] = "#57606a",
        [TimelineRole.User] = "#0969da",
        [TimelineRole.Agent] = "#1a7f37",
        [TimelineRole.Thought] = "#8250df",
        [TimelineRole.Link] = "#0969da",
        [TimelineRole.ActiveNavigation] = "#cf222e",
        [TimelineRole.Warning] = "#9a6700",
        [TimelineRole.Error] = "#cf222e",
        [TimelineRole.Code] = "#24292f"
    };
```

Use `ColorName16.Gray`, `DarkGray`, `Blue`, `Green`, `Magenta`, `Cyan`, `Yellow`, `Red`, `White`, and `Black` for the limited-color mapping. Build `CreateControlScheme()` with explicit `Normal`, `Focus`, `Active`, `HotNormal`, `HotFocus`, `HotActive`, `Editable`, `ReadOnly`, `Disabled`, and `Code` attributes so stock input/list controls inherit the same visual language.

- [ ] **Step 4: Run the palette tests and terminal project build**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalPaletteTests"
dotnet build .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --no-restore
```

Expected: PASS; build completes with zero warnings and errors.

- [ ] **Step 5: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalPalette.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalPaletteTests.cs
git commit -m "feat: add adaptive terminal palette" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Focused Markdown Parser

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalMarkdown.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalMarkdownTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs:17-20`

**Interfaces:**
- Consumes: `TimelineRole`, `TimelineTextStyle`, and `Uri`.
- Produces:

```csharp
internal enum TimelineBlockKind
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    UnorderedListItem,
    OrderedListItem
}

internal sealed record TimelineSpan(
    string Text,
    TimelineRole Role,
    TimelineTextStyle Style = TimelineTextStyle.None,
    Uri? LinkTarget = null);

internal sealed record TimelineBlock(
    TimelineBlockKind Kind,
    IReadOnlyList<TimelineSpan> Spans,
    int? Ordinal = null);

internal static class TerminalMarkdown
{
    internal static IReadOnlyList<TimelineBlock> Parse(string value, TimelineRole baseRole);
}
```

- The parser handles ATX headings 1-3, `**bold**`, `*italic*`, `_italic_`, backtick code, unordered markers, ordered markers, links with absolute `http`/`https` URLs, paragraphs, and explicit newlines.
- Recognized delimiters are omitted from span text.
- Invalid delimiters, invalid/non-HTTP links, unsupported syntax, and raw HTML remain visible as sanitized plain text. Control characters become literal `\uXXXX` text before tokenization.

- [ ] **Step 1: Write failing parser tests**

Add focused tests containing exact expected spans:

```csharp
[Fact]
public void Parse_EmitsHeadingAndInlineStylesWithoutDelimiters()
{
    IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(
        "# Result\nThis is **bold**, *italic*, and `code`.",
        TimelineRole.Agent);

    Assert.Equal(TimelineBlockKind.Heading1, blocks[0].Kind);
    Assert.Equal("Result", Assert.Single(blocks[0].Spans).Text);
    Assert.Equal(TimelineTextStyle.Bold, blocks[0].Spans[0].Style);
    Assert.Contains(blocks[1].Spans, span =>
        span.Text == "bold" && span.Style.HasFlag(TimelineTextStyle.Bold));
    Assert.Contains(blocks[1].Spans, span =>
        span.Text == "italic" && span.Style.HasFlag(TimelineTextStyle.Italic));
    Assert.Contains(blocks[1].Spans, span =>
        span.Text == "code" && span.Role == TimelineRole.Code);
    Assert.DoesNotContain("**", PlainText(blocks));
}

[Fact]
public void Parse_EmitsListMetadataAndSafeLinkTarget()
{
    IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(
        "- first\n2. [Docs](https://example.com/docs)",
        TimelineRole.Agent);

    Assert.Equal(TimelineBlockKind.UnorderedListItem, blocks[0].Kind);
    Assert.Equal(TimelineBlockKind.OrderedListItem, blocks[1].Kind);
    Assert.Equal(2, blocks[1].Ordinal);
    TimelineSpan link = Assert.Single(blocks[1].Spans, span => span.LinkTarget is not null);
    Assert.Equal("Docs", link.Text);
    Assert.Equal("https://example.com/docs", link.LinkTarget!.AbsoluteUri);
    Assert.Equal(TimelineRole.Link, link.Role);
    Assert.True(link.Style.HasFlag(TimelineTextStyle.Underline));
}

[Theory]
[InlineData("**unclosed")]
[InlineData("[unsafe](file:///C:/secret.txt)")]
[InlineData("<b>not interpreted</b>")]
public void Parse_MalformedOrUnsupportedInputFallsBackToSanitizedPlainText(string input)
{
    IReadOnlyList<TimelineBlock> blocks = TerminalMarkdown.Parse(input, TimelineRole.Agent);

    Assert.Equal(input, PlainText(blocks));
    Assert.All(blocks.SelectMany(block => block.Spans), span => Assert.Null(span.LinkTarget));
}

[Fact]
public void Parse_EscapesControlsBeforeReturningSpans()
{
    IReadOnlyList<TimelineBlock> blocks =
        TerminalMarkdown.Parse("alpha\u001Bbeta", TimelineRole.Agent);

    Assert.Equal("alpha\\u001Bbeta", PlainText(blocks));
}
```

- [ ] **Step 2: Run parser tests and verify they fail**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalMarkdownTests"
```

Expected: FAIL because the parser and block model do not exist.

- [ ] **Step 3: Implement a deterministic line/block parser**

Implement parsing without regular-expression backtracking:

```csharp
internal static IReadOnlyList<TimelineBlock> Parse(string value, TimelineRole baseRole)
{
    ArgumentNullException.ThrowIfNull(value);
    string normalized = Sanitize(value).Replace("\r\n", "\n").Replace('\r', '\n');
    List<TimelineBlock> blocks = [];

    foreach (string line in normalized.Split('\n'))
    {
        (TimelineBlockKind kind, string content, int? ordinal) = ParseBlockPrefix(line);
        blocks.Add(new TimelineBlock(kind, ParseInline(content, baseRole), ordinal));
    }

    return Array.AsReadOnly(blocks.ToArray());
}
```

`ParseInline` scans left-to-right, accepts only a closing delimiter found on the same logical line, and emits plain text for the whole candidate when a construct is malformed. Parse links only when `Uri.TryCreate(..., UriKind.Absolute, out Uri? target)` succeeds and the scheme is `http` or `https`. Do not decode or strip HTML tags.

- [ ] **Step 4: Run parser tests and existing timeline tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalMarkdownTests|FullyQualifiedName~TerminalTimelineLayoutTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalMarkdown.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalMarkdownTests.cs
git commit -m "feat: parse terminal markdown spans" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Styled Timeline Projection and Drawing

**Files:**
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineView.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs`

**Interfaces:**
- Consumes: `TerminalMarkdown.Parse`, `TerminalPalette.Get`, existing `ChatEntry`, `TimelineGlyphSet`, and `TimelineScrollState`.
- Produces:

```csharp
internal static TimelineLayoutResult Build(
    IReadOnlyList<ChatEntry> entries,
    int width,
    TimelineGlyphSet glyphs,
    bool collapseCompletedThoughts);

internal static IReadOnlyList<TimelineLine> WrapBlocks(
    IReadOnlyList<TimelineBlock> blocks,
    int width);

internal TerminalPalette Palette { get; init; }
```

- `TerminalTimelineLayout.Build` keeps author headers separate from body Markdown, maps each entry kind to a semantic role, and changes collapsed thought copy to `F2 for details`.
- `WrapBlocks` prepends `- ` or `<ordinal>. `, applies heading bold style, wraps across span boundaries by terminal cells, and preserves every span's role, style, and `LinkTarget`.
- `TerminalTimelineView.DrawLine` uses `Palette.Get(span.Role, span.Style)`.

- [ ] **Step 1: Extend layout tests with styled and width-sensitive cases**

```csharp
[Fact]
public void Build_StylesMarkdownBodyButNeverParsesAuthorHeader()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("a", ChatEntryKind.Agent, "**Agent**", "**Answer** and `code`")],
        width: 40,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: true);

    Assert.Equal("●  **Agent**", PlainText(result.Lines[0]));
    Assert.Equal("Answer and code", PlainText(result.Lines[1]));
    Assert.Contains(result.Lines[1].Spans, span =>
        span.Text == "Answer" && span.Style == TimelineTextStyle.Bold);
    Assert.Contains(result.Lines[1].Spans, span =>
        span.Text == "code" && span.Role == TimelineRole.Code);
}

[Fact]
public void Build_WrapsStyledCjkAndEmojiAtCellWidthWithoutLosingMetadata()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("a", ChatEntryKind.Agent, "A", "**界界**🙂🙂")],
        width: 4,
        TimelineGlyphSet.Ascii,
        collapseCompletedThoughts: true);

    Assert.All(result.Lines, line => Assert.True(PlainText(line).GetColumns() <= 4));
    Assert.All(
        result.Lines.SelectMany(line => line.Spans).Where(span => span.Text.Contains('界')),
        span => Assert.True(span.Style.HasFlag(TimelineTextStyle.Bold)));
    Assert.All(result.Lines, line => Assert.True(IsWellFormedUtf16(PlainText(line))));
}

[Fact]
public void Build_CollapsedThoughtUsesPortableF2Hint()
{
    TimelineLayoutResult result = TerminalTimelineLayout.Build(
        [Entry("done", ChatEntryKind.Thought, "Reasoning", "Complete")],
        width: 80,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: true);

    Assert.Contains(result.Lines, line => PlainText(line) == "Reasoning complete · F2 for details");
}
```

Update prior expected roles to `User`, `Agent`, `Thought`, `Muted`, `Warning`, and `Error`.

- [ ] **Step 2: Run the timeline layout tests and verify failure**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests"
```

Expected: FAIL because body text is still flattened and Ctrl+2 remains in the thought hint.

- [ ] **Step 3: Replace plain-token wrapping with styled-token wrapping**

Retain `StringInfo.GetTextElementEnumerator` and `GetColumns()`. Change the wrapping accumulator from one `StringBuilder` to adjacent compatible span builders:

```csharp
private static void Append(
    List<TimelineSpan> line,
    string text,
    TimelineRole role,
    TimelineTextStyle style,
    Uri? linkTarget)
{
    if (line.Count > 0
        && line[^1].Role == role
        && line[^1].Style == style
        && Equals(line[^1].LinkTarget, linkTarget))
    {
        TimelineSpan previous = line[^1];
        line[^1] = previous with { Text = previous.Text + text };
        return;
    }

    line.Add(new TimelineSpan(text, role, style, linkTarget));
}
```

Treat tabs and escaped control text as plain spans with the enclosing style. Preserve the prior guarantees for zero/one-column widths, unrenderable wide graphemes, row ranges, separators, and immutable results.

- [ ] **Step 4: Write and run failing drawing tests**

Add a palette injection test that draws one line under the ANSI fake driver and inspects its cells:

```csharp
[Fact]
public void Drawing_UsesPaletteAndTextStyleForEachSpan()
{
    TerminalPalette palette = TerminalPalette.Create(
        new Terminal.Gui.Drawing.Attribute(new Color("#c9d1d9"), new Color("#0d1117")),
        supportsTrueColor: true);
    using TerminalTimelineView view = new()
    {
        Width = 40,
        Height = 4,
        Palette = palette
    };

    view.SetEntries([Entry("a", "**bold** and `code`")]);

    Assert.Contains(view.RenderedLines.SelectMany(line => line.Spans), span =>
        span.Text == "bold" && span.Style.HasFlag(TimelineTextStyle.Bold));
    Assert.Contains(view.RenderedLines.SelectMany(line => line.Spans), span =>
        span.Text == "code" && span.Role == TimelineRole.Code);
}
```

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineViewTests"
```

Expected: FAIL because `TerminalTimelineView` does not accept or use a palette.

- [ ] **Step 5: Draw every span with the explicit palette**

Replace `GetVisualRole` mapping with:

```csharp
private void DrawLine(TimelineLine line, int visibleRow)
{
    Move(0, visibleRow);
    foreach (TimelineSpan span in line.Spans)
    {
        SetAttribute(Palette.Get(span.Role, span.Style));
        AddStr(span.Text);
    }
}
```

Use `Palette.Get(TimelineRole.Primary)` when clearing rows. Preserve `SetEntries`, resize reflow, scrolling, follow-latest, and immutable snapshots.

- [ ] **Step 6: Run all timeline tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalTimelineLayoutTests|FullyQualifiedName~TerminalTimelineViewTests|FullyQualifiedName~TimelineScrollStateTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineView.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs
git commit -m "feat: render styled terminal timeline" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Custom Navigation and Composer Chrome

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalNavigationView.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalComposerView.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalFooterView.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalNavigationViewTests.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalComposerViewTests.cs`

**Interfaces:**
- Consumes: `TerminalPalette`, `TimelineGlyphSet`, Terminal.Gui `View`, and `TextField`.
- Produces:

```csharp
internal enum TerminalSurface
{
    Chat,
    Thoughts,
    Activities,
    Help
}

internal sealed class TerminalNavigationView : View
{
    internal TerminalPalette Palette { get; init; }
    internal TerminalSurface ActiveSurface { get; set; }
    internal IReadOnlyList<TimelineSpan> GetSpans();
}

internal sealed class TerminalComposerView : View
{
    internal TerminalComposerView(
        TextField input,
        TerminalPalette palette,
        bool useUnicode);

    internal TextField Input { get; }
    internal string[] GetBorderRows();
}

internal sealed class TerminalFooterView : View
{
    internal TerminalFooterView(TerminalPalette palette);
    internal string Text { get; }
    internal TimelineRole Role { get; }
}
```

- The navigation text is `F1 Chat   F2 Thoughts   F3 Activities   F4 Help`. The active segment uses `ActiveNavigation` plus `Underline`; inactive shortcut labels use `Link`, names use `Muted`.
- The composer is exactly three rows high. Unicode border is `╭─╮│╰─╯`; ASCII border is `+-+|+-+`. The prompt `>` uses `User`; the embedded `TextField` occupies row 1 beginning at column 2.
- The footer draws `Enter send · F1–F4 views · Esc back · Ctrl+C copy · Ctrl+Q quit`, using ASCII separators when Unicode is unavailable. It cannot focus.

- [ ] **Step 1: Write failing navigation and composer tests**

```csharp
[Fact]
public void Navigation_DrawModelMarksOnlyTheActiveSurface()
{
    using TerminalNavigationView view = new()
    {
        Width = 80,
        Height = 1,
        Palette = LimitedPalette(),
        ActiveSurface = TerminalSurface.Activities
    };

    IReadOnlyList<TimelineSpan> spans = view.GetSpans();

    Assert.Single(spans, span =>
        span.Text.Contains("Activities", StringComparison.Ordinal)
        && span.Role == TimelineRole.ActiveNavigation
        && span.Style.HasFlag(TimelineTextStyle.Underline));
}

[Theory]
[InlineData(true, "╭", "╯")]
[InlineData(false, "+", "+")]
public void Composer_UsesRequestedBorderSet(bool unicode, string first, string last)
{
    using TextField input = new();
    using TerminalComposerView view = new(input, LimitedPalette(), unicode)
    {
        Width = 20,
        Height = 3
    };

    string[] rows = view.GetBorderRows();

    Assert.StartsWith(first, rows[0]);
    Assert.EndsWith(last, rows[2]);
    Assert.Same(input, view.Input);
    Assert.Equal(new System.Drawing.Rectangle(2, 1, 16, 1), input.Frame);
}

[Fact]
public void Footer_IsMutedAndCannotReceiveFocus()
{
    using TerminalFooterView footer = new(LimitedPalette());

    Assert.False(footer.CanFocus);
    Assert.Contains("F1", footer.Text);
    Assert.Equal(TimelineRole.Muted, footer.Role);
}
```

- [ ] **Step 2: Run the new view tests and verify failure**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalNavigationViewTests|FullyQualifiedName~TerminalComposerViewTests"
```

Expected: FAIL because the custom chrome views do not exist.

- [ ] **Step 3: Implement custom drawing and deterministic layout**

Implement `OnDrawingContent` in each view using `Move`, `SetAttribute`, and `AddStr`. Expose internal pure helpers (`GetSpans`, `GetBorderRows`) only where needed to test drawing decisions without terminal snapshots. Clamp all drawing and the input frame for widths from 0 upward; never create negative dimensions.

- [ ] **Step 4: Run custom chrome tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalNavigationViewTests|FullyQualifiedName~TerminalComposerViewTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalNavigationView.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalComposerView.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalFooterView.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalNavigationViewTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalComposerViewTests.cs
git commit -m "feat: add custom terminal chrome" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Borderless Root Shell and Portable Navigation

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalShellView.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalShellViewTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs:278-840`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs:80-290,540-590`

**Interfaces:**
- Consumes: Terminal.Gui `Runnable`, `IApplication.Keyboard.KeyBindings.AddApp`, `View.AddCommand`, `Key.F1` through `Key.F4`, `Key.Esc`, `Key.C.WithCtrl`, and `Key.Q.WithCtrl`.
- Produces:

```csharp
internal sealed class TerminalShellView : Runnable
{
    internal TerminalShellView(
        TerminalNavigationView navigation,
        IReadOnlyDictionary<TerminalSurface, View> surfaces,
        Func<TerminalSurface, View?> resolveFocusTarget,
        Action copy,
        Action quit,
        Action<Exception> reportNavigationFailure);

    internal TerminalSurface ActiveSurface { get; }
    internal TerminalNavigationView Navigation { get; }
    internal View ContentRegion { get; }
    internal void RegisterApplicationBindings(IApplication application);
    internal void Show(TerminalSurface surface);
    internal void ReturnToChat();
}

internal Runnable CreateShell(
    IApplication application,
    TerminalPresenter presenter,
    CancellationTokenSource shutdownSource);
```

- `TerminalShellView` has no title, uses `BorderStyle = LineStyle.None`, and lays out a one-row navigation at `Y = 0` with `ContentRegion` filling below it.
- Root commands use application-scoped bindings:

```csharp
AddCommand(Command.Home, () => { Show(TerminalSurface.Chat); return true; });
AddCommand(Command.Find, () => { Show(TerminalSurface.Thoughts); return true; });
AddCommand(Command.Open, () => { Show(TerminalSurface.Activities); return true; });
AddCommand(Command.Context, () => { Show(TerminalSurface.Help); return true; });
AddCommand(Command.Cancel, () => { ReturnToChat(); return true; });
AddCommand(Command.Copy, () => { copy(); return true; });
AddCommand(Command.Quit, () => { quit(); return true; });

application.Keyboard.KeyBindings.AddApp(Key.F1, this, [Command.Home]);
application.Keyboard.KeyBindings.AddApp(Key.F2, this, [Command.Find]);
application.Keyboard.KeyBindings.AddApp(Key.F3, this, [Command.Open]);
application.Keyboard.KeyBindings.AddApp(Key.F4, this, [Command.Context]);
application.Keyboard.KeyBindings.AddApp(Key.Esc, this, [Command.Cancel]);
application.Keyboard.KeyBindings.AddApp(Key.C.WithCtrl, this, [Command.Copy]);
application.Keyboard.KeyBindings.AddApp(Key.Q.WithCtrl, this, [Command.Quit]);
```

- Focus resolver rules:
  - Chat: enabled composer, otherwise conversation timeline.
  - Thoughts: thought timeline.
  - Activities: activity list.
  - Help: help content.
- `Esc` changes the surface only when not already in Chat; otherwise it remains available to the focused control.
- Navigation catches only `InvalidOperationException` and `ObjectDisposedException` from surface/focus transitions, forwards them to `reportNavigationFailure`, and marks the command handled. Other exceptions propagate.
- `RunAsync` creates and runs/disposes `Runnable`, not `Window`.

- [ ] **Step 1: Write failing shell structure tests**

```csharp
[Fact]
public void Shell_IsBorderlessAndKeepsNavigationOutsideSwitchableContent()
{
    using TerminalShellView shell = CreateShell();

    Assert.Equal(LineStyle.None, shell.BorderStyle);
    Assert.Same(shell.Navigation, shell.SubViews[0]);
    Assert.Contains(shell.ContentRegion, shell.SubViews);

    foreach (TerminalSurface surface in Enum.GetValues<TerminalSurface>())
    {
        shell.Show(surface);
        Assert.True(shell.Navigation.Visible);
        Assert.Single(shell.ContentRegion.SubViews, view => view.Visible);
    }
}
```

- [ ] **Step 2: Write application-level key injection tests**

Replace synthetic `window.NewKeyDownEvent(Key.D2.WithCtrl)` tests. Initialize the ANSI driver, run one iteration, focus each of the composer, timeline, activity list, and JSON inspector, then inject through the actual application keyboard:

```csharp
private static void RaiseTerminalKey(IApplication application, Key key)
{
    bool handled = application.Keyboard.RaiseKeyDownEvent(key);
    Assert.True(handled, $"{key} was not handled by the application binding.");
}
```

For every starting focus, assert:

```csharp
RaiseTerminalKey(application, Key.F2);
Assert.Equal(TerminalSurface.Thoughts, shell.ActiveSurface);
Assert.True(thoughtTimeline.HasFocus);

RaiseTerminalKey(application, Key.F3);
Assert.Equal(TerminalSurface.Activities, shell.ActiveSurface);
Assert.True(activityList.HasFocus);

RaiseTerminalKey(application, Key.F4);
Assert.Equal(TerminalSurface.Help, shell.ActiveSurface);
Assert.True(help.HasFocus);

RaiseTerminalKey(application, Key.Esc);
Assert.Equal(TerminalSurface.Chat, shell.ActiveSurface);
Assert.True(composer.HasFocus);
```

Also assert `Key.D1.WithCtrl` through `Key.D4.WithCtrl` are absent from `application.Keyboard.KeyBindings`.
Inject `Key.C.WithCtrl` and `Key.Q.WithCtrl` through `application.Keyboard.RaiseKeyDownEvent` and assert the existing copy and shutdown callbacks run. From each of Thoughts, Activities, and Help, inject `Key.Esc` and assert Chat and its focus target are restored.

Add one focused failure-path test:

```csharp
[Fact]
public void Navigation_InvalidFocusTransitionReportsFailure()
{
    InvalidOperationException failure = new("focus failed");
    Exception? reported = null;
    using TerminalShellView shell = CreateShell(
        resolveFocusTarget: _ => throw failure,
        reportNavigationFailure: exception => reported = exception);

    shell.Show(TerminalSurface.Thoughts);

    Assert.Same(failure, reported);
}
```

- [ ] **Step 3: Run shell/application tests and verify failure**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalShellViewTests|FullyQualifiedName~TerminalChatApplicationTests"
```

Expected: FAIL because the application still returns a bordered `Window`, uses local `KeyDown`, and exposes `Ctrl+number` shortcuts.

- [ ] **Step 4: Implement `TerminalShellView` and switch application composition**

In `TerminalChatApplication`:

1. Rename `CreateWindow` to `CreateShell` and return `Runnable`.
2. Construct `TerminalPalette` after `application.Init()` from:

```csharp
TerminalPalette palette = TerminalPalette.Create(
    application.Driver.DefaultAttribute,
    application.Driver.SupportsTrueColor && !application.Driver.Force16Colors);
```

3. Remove `BuildStatusBar`, `OnWindowKeyDown`, and `StatusBar`.
4. Remove stock `Window`, `FrameView`, and default-layout `Tabs`.
5. Build all four surfaces once and pass them to `TerminalShellView`.
6. Register application bindings before `application.Run(shell)`.
7. Set the palette control scheme on `TextField`, `ListView`, `TextView`, links, buttons, and surface containers.
8. Replace Help text with F-key/Esc copy.
9. Pass `exception => SetStatus($"Navigation failed: {exception.Message}", DiagnosticSeverity.Error)` as the navigation failure reporter.

For split mode, put a borderless left content container and borderless Activities view in the shell content region; do not reintroduce `Tabs`. `F1` and `F2` choose which left timeline is visible, `F3` focuses Activities, and `F4` temporarily shows Help in the full content region. `Esc` returns to Chat.

- [ ] **Step 5: Run shell/application tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalShellViewTests|FullyQualifiedName~TerminalChatApplicationTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalShellView.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalShellViewTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs
git commit -m "feat: add borderless terminal shell" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Integrate Composer, Footer, and Connection Status

**Files:**
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs:537-630`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`

**Interfaces:**
- Consumes: `TerminalComposerView`, `TerminalFooterView`, `TerminalPalette`, existing `_composer`, `_status`, `_actionBar`, and `ChatTranscriptLayoutView`.
- Produces: Chat surface with custom composer/footer, semantically styled connection/status text, and no stock chrome.

- [ ] **Step 1: Replace old structural assertions with failing approved-shell assertions**

```csharp
[Fact]
public void CreateShell_DefaultChatUsesOnlyCustomChrome()
{
    using IApplication application = Application.Create();
    application.Init(DriverRegistry.Names.ANSI);
    using CancellationTokenSource shutdown = new();
    TerminalChatApplication terminal = new(TerminalOptions.Parse([]));
    using TerminalPresenter presenter = CreatePresenter(terminal);

    using Runnable shell = terminal.CreateShell(application, presenter, shutdown);
    IReadOnlyList<View> descendants = Descendants(shell).ToArray();

    Assert.DoesNotContain(descendants, view => view is Window);
    Assert.DoesNotContain(descendants, view => view is FrameView);
    Assert.DoesNotContain(descendants, view => view is StatusBar);
    Assert.DoesNotContain(descendants, view => view is Tabs);
    Assert.Single(descendants.OfType<TerminalNavigationView>());
    Assert.Single(descendants.OfType<TerminalComposerView>());
    Assert.Single(descendants.OfType<TerminalFooterView>());
}
```

Update the short-height test to identify `TerminalComposerView` and `TerminalFooterView`, preserve a minimum one-row transcript when possible, and assert non-overlap from heights 1 through 10.

- [ ] **Step 2: Run the application tests and verify failure**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalChatApplicationTests"
```

Expected: FAIL while Chat still constructs the old frame/footer or does not apply semantic status attributes.

- [ ] **Step 3: Compose the custom Chat bottom stack**

Construct the `TextField` first, pass it to `TerminalComposerView`, and keep the existing `Accepting` handler unchanged. Replace the old footer with `TerminalFooterView`. Change `TimelineRoleLabel` to accept a `TerminalPalette` and use direct semantic roles:

```csharp
TimelineRoleLabel header = new(palette)
{
    Content = "● Copilot Studio  connected",
    Role = TimelineRole.Agent
};
```

Use `TimelineRole.Error` for error status, `Warning` for warnings, and `Muted` for normal status. Keep actions and links behavior intact.

- [ ] **Step 4: Run application and presenter tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalChatApplicationTests|FullyQualifiedName~TerminalPresenterTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs
git commit -m "feat: integrate custom chat composer" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: Responsive Activities Inspector

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalActivityView.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalActivityViewTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs:643-682`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`

**Interfaces:**
- Consumes: `ListView<ActivityRecord>`, read-only scrolling `TextView`, `TerminalPalette`, and `ActivityRecord`.
- Produces:

```csharp
internal sealed class TerminalActivityView : View
{
    internal TerminalActivityView(
        ListView<ActivityRecord> activityList,
        TextView json,
        TerminalPalette palette);

    internal ListView<ActivityRecord> ActivityList { get; }
    internal TextView Json { get; }
}
```

- At widths `>= 72`, `TerminalActivityView` lays out the list on the left at `max(28, floor(width * 0.36))` and JSON on the right.
- Below 72 columns, it stacks the list above JSON: list uses at least 3 rows and at most 40% of available height.
- The list receives at least 28 columns whenever the terminal is wide enough. Terminal.Gui clips longer `ActivityRecord.ToString()` summaries at the list viewport instead of narrowing the list into one-word columns.
- JSON remains read-only with `ScrollBars = true` and `WordWrap = false`.

- [ ] **Step 1: Write failing activity layout and truncation tests**

```csharp
[Theory]
[InlineData(100, 24, false)]
[InlineData(60, 24, true)]
[InlineData(30, 8, true)]
public void Layout_PreservesUsableListAndJsonPanes(int width, int height, bool stacked)
{
    using TerminalActivityView view = CreateActivityView(width, height);

    view.Layout();

    Assert.True(view.ActivityList.Frame.Width >= Math.Min(width, 28));
    Assert.True(view.ActivityList.Frame.Height >= Math.Min(height, 3));
    Assert.Equal(stacked, view.Json.Frame.Y > 0);
    Assert.True(view.Json.Frame.Width > 0);
    Assert.True(view.Json.Frame.Height > 0);
}

[Fact]
public void Layout_LongSummaryKeepsUsableListWidth()
{
    using TerminalActivityView view = CreateActivityView(
        width: 60,
        height: 20,
        summary: "A long activity summary with 界 and 🙂 content");

    view.Layout();

    Assert.Equal(60, view.ActivityList.Frame.Width);
    Assert.Equal(60, view.Json.Frame.Width);
}
```

- [ ] **Step 2: Run activity tests and verify failure**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalActivityViewTests"
```

Expected: FAIL because responsive activity layout does not exist.

- [ ] **Step 3: Implement responsive layout and safe summary truncation**

Use `OnSubViewLayout` to set exact frames. The wide layout gives the list a stable minimum width before assigning the remaining columns to JSON. The narrow layout gives both controls the full viewport width, so Terminal.Gui performs ordinary row clipping rather than forcing content into a narrow vertical column.

Configure `ListView` and `TextView` with `palette.CreateControlScheme()`. Keep selection state and `ValueChanged` behavior in `TerminalChatApplication`.

- [ ] **Step 4: Integrate `TerminalActivityView` and run activity/application tests**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalActivityViewTests|FullyQualifiedName~ActivityRecordTests|FullyQualifiedName~TerminalActivityStateTests|FullyQualifiedName~TerminalChatApplicationTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalActivityView.cs .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalActivityViewTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs
git commit -m "feat: improve terminal activity inspector" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: Integration Regressions and Documentation

**Files:**
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md`

**Interfaces:**
- Consumes: all redesigned shell APIs and existing presenter/session behavior.
- Produces: complete acceptance coverage and user-facing instructions matching the runtime.

- [ ] **Step 1: Add end-to-end shell regression tests**

Add tests that cover these exact sequences:

```csharp
[Fact]
public async Task StreamingMarkdownReplacementKeepsOneStyledEntryAndFollowsLatest()
{
    // Start with "**Hel", replace the same ChatEntry.Key with "**Hello**",
    // assert one entry range, visible text "Hello", Bold style, and bottom follow.
}

[Fact]
public void SplitLayoutUsesPersistentNavigationAndBorderlessSurfaces()
{
    // Assert navigation remains visible while F1/F2 focus left timelines,
    // F3 focuses the right activity list, F4 shows Help, and Esc restores Chat.
}

[Fact]
public void NarrowResizeKeepsChatAndActivitiesWithinViewport()
{
    // Exercise widths 20, 40, 71, 72, and 100 and heights 6, 10, and 24.
    // Assert all child rectangles are non-negative, inside their parent,
    // timeline lines fit terminal-cell width, and both activity panes remain usable.
}
```

Also retain coverage for startup success/failure, empty send, busy state, suggested actions, received-link confirmation, copy fallback, activity selection, shutdown, Unicode/ASCII glyphs, CJK, emoji, tabs, and control escaping.

- [ ] **Step 2: Run the complete terminal tests and fix only redesign regressions**

Run:

```powershell
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj
```

Expected: PASS. If a failure predates this branch and is unrelated to the redesigned files, document it rather than changing unrelated code.

- [ ] **Step 3: Update the README**

Replace the old layout and keyboard sections with:

```markdown
## Keyboard shortcuts

- `F1`: show **Chat**
- `F2`: show **Thoughts**
- `F3`: show **Activities**
- `F4`: show **Help**
- `Esc`: return to **Chat**
- `Ctrl+C`: copy the focused link or selected activity JSON
- `Ctrl+Q`: cancel active work and quit
- `Enter`: send the composer text or activate the focused link/action
```

Describe the persistent navigation, rendered Markdown subset, adaptive dark/light/limited-color palette, borderless default and split layouts, responsive Activities inspector, and ASCII glyph fallback. Remove all claims that `Ctrl+1` through `Ctrl+4`, `StatusBar`, `FrameView`, or stock tab chrome are part of the UX.

- [ ] **Step 4: Build the sample and run the complete terminal tests**

Run:

```powershell
dotnet build .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --no-restore
dotnet test .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --no-restore
```

Expected: build succeeds with zero warnings/errors and all terminal tests pass.

- [ ] **Step 5: Inspect the final diff and protect local configuration**

Run:

```powershell
git status --short
git diff --check
git diff -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\appsettings.json
```

Expected: `git diff --check` emits no output; `appsettings.json` still contains only the user's pre-existing local changes and is not staged.

- [ ] **Step 6: Commit**

```powershell
git add -- .\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs .\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs
git commit -m "docs: describe redesigned terminal shell" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
