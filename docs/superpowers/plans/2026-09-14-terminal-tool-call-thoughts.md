# Terminal Tool-Call Thoughts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Project inbound `toolCall` entities into one structured Thoughts block that updates from Running to Completed and shows visible parameters plus adaptive elapsed time.

**Architecture:** Extend `ChatEntry` with an optional structured tool-call payload, parse each `toolCall` entity in `ActivityInterpreter`, and upsert it by `toolCallId`. Keep protocol data structured through state projection, then add a dedicated `TerminalTimelineLayout` path for the approved Timeline Narrative presentation.

**Tech Stack:** .NET 10, C#, Microsoft Agents Activity Protocol models, `System.Text.Json`, Terminal.Gui 2.5.0, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-14-terminal-tool-call-thoughts-design.md`

## Global Constraints

- Tool-call entity type, property names, and known statuses are matched case-insensitively.
- Correlate lifecycle updates with the stable key `tool:{toolCallId}`.
- Completion payload parameters replace the start payload parameters; do not merge stale values.
- Tool-call blocks appear on F2 Thoughts and not on Chat.
- Never copy `hiddenFilledParameters`, `hiddenUnfilledParameters`, or `result` into the Thoughts model, rendered lines, or diagnostics.
- The existing Activities inspector remains an intentional raw-protocol view.
- Use the completion payload's `durationMs`; do not measure elapsed time locally.
- Preserve existing streaming sequence, status, attachment, action, and ordinary-thought behavior.
- Preserve terminal-cell wrapping, grapheme handling, control escaping, Unicode/ASCII fallback, and narrow viewport behavior.
- Do not modify or stage `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\appsettings.json`.
- Follow TDD for every behavior change and include the required `Co-authored-by` trailer in every commit.

---

## File Structure

- Create `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ToolCallDetails.cs`
  - Owns the structured, presentation-independent tool-call payload.
- Modify `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ChatEntry.cs`
  - Adds `ToolCall` kind and optional details on entries.
- Modify `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityInterpreter.cs`
  - Detects, validates, and projects tool-call entities for ordinary and streaming activities.
- Modify `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs`
  - Routes tool entries to Thoughts only.
- Create `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalToolCallFormatting.cs`
  - Formats duration and structured tool content into semantic timeline blocks.
- Modify `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs`
  - Dispatches tool entries to the structured formatter and adds tool-specific header semantics.
- Modify `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs`
  - Covers entity parsing, privacy exclusions, correlation, streaming, and malformed input.
- Modify `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`
  - Covers Chat/Thoughts surface routing.
- Modify `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs`
  - Covers the narrative layout, duration thresholds, wrapping, sanitation, and glyph fallbacks.
- Create `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalToolCallFormattingTests.cs`
  - Covers focused duration and structured-value formatting.

---

### Task 1: Structured Tool-Call Model and Entity Parsing

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ToolCallDetails.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ChatEntry.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityInterpreter.cs`
- Test: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs`

**Interfaces:**
- Produces:
  - `ToolCallParameter(string Name, JsonElement Value)`
  - `ToolCallDetails(string Id, string Name, string? DisplayName, string? Category, string Status, IReadOnlyList<ToolCallParameter> FilledParameters, IReadOnlyList<string> UnfilledParameters, long? DurationMs)`
  - `ChatEntryKind.ToolCall`
  - `ChatEntry.ToolCall`
- Consumes: `Entity.Properties`, `ChatChange`, and existing keyed upsert behavior.

- [ ] **Step 1: Add failing tests for started and completed entity parsing**

Add helpers that build representative entities matching the supplied payloads:

```csharp
private static Entity ToolCallEntity(
    string status,
    JsonElement filledParameters,
    JsonElement unfilledParameters,
    long? durationMs = null)
{
    Entity entity = new("toolCall")
    {
        Properties =
        {
            ["toolCallId"] = JsonSerializer.SerializeToElement("toolu_01EAp1krYNiK2odqQv9mu7hn"),
            ["toolName"] = JsonSerializer.SerializeToElement("current_weather"),
            ["toolDisplayName"] = JsonSerializer.SerializeToElement("Get current weather"),
            ["toolCategory"] = JsonSerializer.SerializeToElement("Connector"),
            ["status"] = JsonSerializer.SerializeToElement(status),
            ["filledParameters"] = filledParameters,
            ["unfilledParameters"] = unfilledParameters,
            ["hiddenFilledParameters"] = JsonSerializer.SerializeToElement(
                new { apiKey = "must-not-render" }),
            ["hiddenUnfilledParameters"] = JsonSerializer.SerializeToElement(
                new[] { "secret" }),
            ["result"] = JsonSerializer.SerializeToElement("must-not-render")
        }
    };

    if (durationMs is not null)
    {
        entity.Properties["durationMs"] = JsonSerializer.SerializeToElement(durationMs.Value);
    }

    return entity;
}
```

Add:

```csharp
[Fact]
public void Process_StartedToolCall_CreatesStructuredRunningEntry()
{
    Activity activity = StreamActivityWithEntity(
        ToolCallEntity(
            "started",
            JsonSerializer.SerializeToElement(new { Location = "Seattle, WA, USA", units = "I" }),
            JsonSerializer.SerializeToElement(Array.Empty<string>())));

    ChatEntry entry = Assert.Single(
        new ActivityInterpreter()
            .Process(activity, ActivityDirection.Inbound),
        change => change.Entry?.Kind == ChatEntryKind.ToolCall).Entry!;

    Assert.Equal("tool:toolu_01EAp1krYNiK2odqQv9mu7hn", entry.Key);
    Assert.Equal("current_weather", entry.ToolCall!.Name);
    Assert.Equal("started", entry.ToolCall.Status);
    Assert.Collection(
        entry.ToolCall.FilledParameters,
        value => Assert.Equal("Location", value.Name),
        value => Assert.Equal("units", value.Name));
    Assert.Null(entry.ToolCall.DurationMs);
}

[Fact]
public void Process_CompletedToolCall_UsesSameKeyAndFinalSnapshot()
{
    ActivityInterpreter interpreter = new();
    ChatEntry started = SingleToolEntry(interpreter.Process(
        StreamActivityWithEntity(ToolCallEntity(
            "started",
            JsonSerializer.SerializeToElement(new { Location = "Seattle" }),
            JsonSerializer.SerializeToElement(new[] { "units" }))),
        ActivityDirection.Inbound));
    ChatEntry completed = SingleToolEntry(interpreter.Process(
        StreamActivityWithEntity(ToolCallEntity(
            "completed",
            JsonSerializer.SerializeToElement(new { Location = "Seattle, WA, USA", units = "I" }),
            JsonSerializer.SerializeToElement(Array.Empty<string>()),
            durationMs: 2971)),
        ActivityDirection.Inbound));

    Assert.Equal(started.Key, completed.Key);
    Assert.Equal(2971, completed.ToolCall!.DurationMs);
    Assert.DoesNotContain(
        completed.ToolCall.UnfilledParameters,
        value => value == "units");
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~StartedToolCall|FullyQualifiedName~CompletedToolCall" --nologo
```

Expected: compilation fails because `ChatEntryKind.ToolCall`, `ToolCallDetails`,
and `ChatEntry.ToolCall` do not exist.

- [ ] **Step 3: Add the structured model**

Create:

```csharp
#nullable enable

using System.Collections.Generic;
using System.Text.Json;

internal sealed record ToolCallParameter(string Name, JsonElement Value);

internal sealed record ToolCallDetails(
    string Id,
    string Name,
    string? DisplayName,
    string? Category,
    string Status,
    IReadOnlyList<ToolCallParameter> FilledParameters,
    IReadOnlyList<string> UnfilledParameters,
    long? DurationMs);
```

Extend `ChatEntryKind` with `ToolCall`, and append an optional parameter to
`ChatEntry` so existing call sites remain source compatible:

```csharp
internal sealed record ChatEntry(
    string Key,
    ChatEntryKind Kind,
    string Author,
    string Text,
    bool IsTransient,
    IReadOnlyList<ChatLink> Links,
    IReadOnlyList<ChatAction> SuggestedActions,
    string ActionGroupKey,
    DiagnosticSeverity? Severity = null,
    ToolCallDetails? ToolCall = null);
```

- [ ] **Step 4: Implement case-insensitive entity parsing**

Add `AddToolCallEntries` and call it for both ordinary and streaming activity
paths. Keep it independent of stream mutation so informative messages still
produce their existing status change.

Use these signatures:

```csharp
private void AddToolCallEntries(
    List<ChatChange> changes,
    Activity activity,
    ActivityDirection direction,
    string actionGroupKey);

private static bool IsToolCallEntity(Entity entity);

private static ToolCallDetails? ParseToolCall(
    Entity entity,
    out string? error);
```

Parsing rules:

```csharp
string id = GetOptionalString(entity, "toolCallId")?.Trim() ?? string.Empty;
if (id.Length == 0)
{
    error = "Tool call entity has no toolCallId.";
    return null;
}

string name = GetOptionalString(entity, "toolName")
    ?? GetOptionalString(entity, "toolDisplayName")
    ?? "tool";
string status = GetOptionalString(entity, "status") ?? "unknown";
long? durationMs = TryGetNonnegativeInt64(entity, "durationMs");
```

For a JSON object `filledParameters`, enumerate properties in payload order and
store `property.Value.Clone()`. For a present non-object value, store one
fallback parameter named `parameters`. For an array `unfilledParameters`, use
string items directly and compact-serialize other item kinds. For a present
non-array value, compact-serialize it as one unfilled item.

Do not read hidden parameter fields or `result`.

Create the entry directly so it can carry structured details:

```csharp
string key = $"tool:{details.Id}";
changes.Add(new ChatChange(
    ChatChangeKind.Upsert,
    key,
    new ChatEntry(
        key,
        ChatEntryKind.ToolCall,
        GetAuthor(activity, ChatEntryKind.ToolCall, direction),
        string.Empty,
        IsTransientToolStatus(details.Status),
        [],
        [],
        actionGroupKey,
        ToolCall: details)));
```

Use `IsTransientToolStatus` only to express lifecycle state; `completed`
returns `false`, all other statuses return `true`.

- [ ] **Step 5: Add privacy, malformed-input, and completion-only tests**

Add tests that assert:

```csharp
[Fact]
public void Process_ToolCall_DoesNotProjectHiddenParametersOrResult()
{
    ChatEntry entry = SingleToolEntry(/* entity containing sentinel values */);
    string projected = JsonSerializer.Serialize(entry.ToolCall);

    Assert.DoesNotContain("must-not-render", projected, StringComparison.Ordinal);
    Assert.DoesNotContain("apiKey", projected, StringComparison.OrdinalIgnoreCase);
}

[Fact]
public void Process_ToolCallWithoutId_AddsWarningDiagnostic()
{
    ChatEntry diagnostic = Assert.Single(
        new ActivityInterpreter().Process(/* tool entity without id */, ActivityDirection.Inbound),
        change => change.Entry?.Kind == ChatEntryKind.Diagnostic).Entry!;

    Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    Assert.DoesNotContain("must-not-render", diagnostic.Text, StringComparison.Ordinal);
}

[Fact]
public void Process_CompletedToolCallWithoutStart_StillCreatesCompletedEntry()
{
    ChatEntry completed = SingleToolEntry(/* completed payload only */);
    Assert.Equal("completed", completed.ToolCall!.Status);
    Assert.Equal(2971, completed.ToolCall.DurationMs);
}
```

Also cover case-insensitive `TOOLCALL`, `STATUS`, and property lookup; invalid
filled/unfilled shapes; and negative/nonnumeric durations being omitted.

- [ ] **Step 6: Run interpreter tests and verify GREEN**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityInterpreterTests" --nologo
```

Expected: all `ActivityInterpreterTests` pass with zero warnings.

- [ ] **Step 7: Commit Task 1**

```powershell
git add -- `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ToolCallDetails.cs `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ChatEntry.cs `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityInterpreter.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs
git commit -m "feat: interpret tool call lifecycle entities" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Route Tool Calls to Thoughts Only

**Files:**
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs`
- Test: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatStateTests.cs`
- Test: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`

**Interfaces:**
- Consumes: `ChatEntryKind.ToolCall` and `ChatEntry.ToolCall` from Task 1.
- Produces: Chat and Thoughts state filters with explicit tool-call routing.

- [ ] **Step 1: Add failing state-filter and integration tests**

Add a tool entry helper:

```csharp
private static ChatEntry ToolEntry(string id, string status) =>
    new(
        $"tool:{id}",
        ChatEntryKind.ToolCall,
        "Agent",
        string.Empty,
        !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase),
        [],
        [],
        "stream-1",
        ToolCall: new ToolCallDetails(
            id,
            "current_weather",
            "Get current weather",
            "Connector",
            status,
            [],
            [],
            null));
```

Add tests proving the intended filters:

```csharp
[Fact]
public void ChatAndThoughtStates_RouteToolCallOnlyToThoughts()
{
    TerminalChatState chat = new(
        entry => entry.Kind is not ChatEntryKind.Thought and not ChatEntryKind.ToolCall);
    TerminalChatState thoughts = new(
        entry => entry.Kind is ChatEntryKind.Thought or ChatEntryKind.ToolCall);
    ChatChange change = new(ChatChangeKind.Upsert, "tool:1", ToolEntry("1", "started"));

    chat.Apply([change]);
    thoughts.Apply([change]);

    Assert.Empty(chat.Entries);
    Assert.Single(thoughts.Entries);
}
```

Add an application integration test that sends started and completed tool
changes through `ApplyChatChanges`, then asserts the Thoughts timeline contains
one updated entry and the Chat timeline contains none.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~RouteToolCallOnlyToThoughts|FullyQualifiedName~ToolCallAppearsOnlyInThoughts" --nologo
```

Expected: the Chat state contains the tool entry because its current filter
includes every kind.

- [ ] **Step 3: Make both filters explicit**

Replace the current state initialization with:

```csharp
private readonly TerminalChatState _chatState =
    new(entry => entry.Kind is not ChatEntryKind.Thought and not ChatEntryKind.ToolCall);

private readonly TerminalChatState _thoughtState =
    new(entry => entry.Kind is ChatEntryKind.Thought or ChatEntryKind.ToolCall);
```

Do not special-case tool entries in `ApplyChatChanges`; both states continue to
consume the same changes and rely on keyed upsert behavior.

- [ ] **Step 4: Run state and application tests and verify GREEN**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalChatStateTests|FullyQualifiedName~TerminalChatApplicationTests" --nologo
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatStateTests.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs
git commit -m "feat: route tool calls to terminal thoughts" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Timeline Narrative Formatter

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalToolCallFormatting.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs`
- Test: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalToolCallFormattingTests.cs`
- Test: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs`

**Interfaces:**
- Consumes: `ToolCallDetails`, `ToolCallParameter`, `TimelineBlock`,
  `TimelineSpan`, `TimelineRole`, and `TimelineGlyphSet`.
- Produces:
  - `TerminalToolCallFormatting.FormatDuration(long? durationMs)`
  - `TerminalToolCallFormatting.BuildBlocks(ToolCallDetails details)`

- [ ] **Step 1: Add failing duration boundary tests**

Create:

```csharp
public sealed class TerminalToolCallFormattingTests
{
    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(842, "842 ms")]
    [InlineData(1000, "1 s")]
    [InlineData(2971, "2.97 s")]
    [InlineData(12400, "12.4 s")]
    [InlineData(59999, "60 s")]
    [InlineData(60000, "1m")]
    [InlineData(133000, "2m 13s")]
    public void FormatDuration_UsesAdaptiveUnits(long durationMs, string expected)
    {
        Assert.Equal(expected, TerminalToolCallFormatting.FormatDuration(durationMs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void FormatDuration_OmitsMissingOrNegativeValues(long? durationMs)
    {
        Assert.Null(TerminalToolCallFormatting.FormatDuration(durationMs));
    }
}
```

- [ ] **Step 2: Run duration tests and verify RED**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalToolCallFormattingTests" --nologo
```

Expected: compilation fails because `TerminalToolCallFormatting` does not
exist.

- [ ] **Step 3: Implement duration formatting**

Create the formatter with invariant culture:

```csharp
internal static string? FormatDuration(long? durationMs)
{
    if (durationMs is null || durationMs < 0)
    {
        return null;
    }

    if (durationMs < 1000)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{durationMs.Value} ms");
    }

    if (durationMs < 60000)
    {
        decimal seconds = durationMs.Value / 1000m;
        return string.Concat(
            seconds.ToString("0.##", CultureInfo.InvariantCulture),
            " s");
    }

    long minutes = durationMs.Value / 60000;
    long secondsRemainder = durationMs.Value % 60000 / 1000;
    return secondsRemainder == 0
        ? string.Create(CultureInfo.InvariantCulture, $"{minutes}m")
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{minutes}m {secondsRemainder}s");
}
```

- [ ] **Step 4: Add failing narrative block tests**

Build a completed `ToolCallDetails` and assert semantic content rather than one
fragile concatenated string:

```csharp
[Fact]
public void BuildBlocks_CompletedCallUsesTimelineNarrative()
{
    ToolCallDetails details = WeatherTool(
        status: "completed",
        durationMs: 2971,
        filled:
        [
            Parameter("Location", "Seattle, WA, USA"),
            Parameter("units", "I")
        ],
        unfilled: []);

    IReadOnlyList<TimelineBlock> blocks =
        TerminalToolCallFormatting.BuildBlocks(details);
    string text = string.Join(
        "\n",
        blocks.Select(block => string.Concat(block.Spans.Select(span => span.Text))));

    Assert.Contains("Connector · Get current weather", text);
    Assert.Contains("✓ Completed in 2.97 s", text);
    Assert.Contains("Called with", text);
    Assert.Contains("Location = Seattle, WA, USA", text);
    Assert.Contains("units = I", text);
    Assert.Contains("No parameters were left unfilled.", text);
}
```

Add a Running test with filled and unfilled parameters, an unknown-status test,
and a test where display/category are absent.

- [ ] **Step 5: Implement semantic block construction**

`BuildBlocks` returns paragraph blocks in this order:

1. Optional category/display subtitle.
2. Blank paragraph.
3. Status line.
4. Blank paragraph.
5. Filled parameter heading or no-parameters sentence.
6. One paragraph per parameter.
7. Blank paragraph.
8. Unfilled heading/items or natural none sentence.

Use semantic spans:

```csharp
new TimelineSpan("✓ Completed", TimelineRole.Agent, TimelineTextStyle.Bold)
new TimelineSpan($" in {duration}", TimelineRole.Muted)
new TimelineSpan("Called with", TimelineRole.Muted)
new TimelineSpan(parameter.Name, TimelineRole.User)
new TimelineSpan(" = ", TimelineRole.Muted)
new TimelineSpan(FormatJsonValue(parameter.Value), TimelineRole.Primary)
```

Use `JsonSerializer.Serialize(value)` for non-string values. Return
`value.GetString() ?? string.Empty` for strings.

- [ ] **Step 6: Add failing timeline integration tests**

Add tests that build a `ChatEntryKind.ToolCall` entry and assert:

- The header uses the Thought glyph.
- Tool name is the header author and uses the adaptive tool/user role.
- Tool blocks are not collapsed when `collapseCompletedThoughts` is `true`.
- Unicode and ASCII glyph sets both work.
- A narrow width wraps long parameter values without exceeding display width.
- Control characters become the existing visible escape notation.
- Serialized `result` and hidden sentinels are absent from all rendered spans.

Example:

```csharp
[Fact]
public void Build_CompletedToolCall_RemainsExpandedInCollapsedChatMode()
{
    ChatEntry entry = ToolEntry(WeatherTool("completed", 2971));

    TimelineLayoutResult layout = TerminalTimelineLayout.Build(
        [entry],
        40,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: true);
    string rendered = string.Join(
        "\n",
        layout.Lines.Select(line => string.Concat(line.Spans.Select(span => span.Text))));

    Assert.Contains("◆  current_weather", rendered);
    Assert.Contains("Completed in 2.97 s", rendered);
    Assert.DoesNotContain("F2 for details", rendered);
}
```

- [ ] **Step 7: Dispatch tool entries through the structured layout path**

Update header and role switches:

```csharp
ChatEntryKind.ToolCall => (glyphs.Thought, TimelineRole.User)
```

When building the header, select the displayed author independently from the
protocol author:

```csharp
string headerAuthor = entry.Kind == ChatEntryKind.ToolCall
    ? entry.ToolCall?.Name ?? entry.Author
    : entry.Author;
string headerText = BuildHeaderText(headerGlyph, headerAuthor, contentWidth);
```

In `GetBodyBlocks`, before Thought collapsing or Markdown:

```csharp
if (entry.Kind == ChatEntryKind.ToolCall)
{
    return entry.ToolCall is null
        ? CreateLiteralBodyBlocks("Tool call details unavailable.", TimelineRole.Warning)
        : TerminalToolCallFormatting.BuildBlocks(entry.ToolCall);
}
```

Tool data must not pass through `TerminalMarkdown.Parse`.

- [ ] **Step 8: Run formatter and layout tests and verify GREEN**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalToolCallFormattingTests|FullyQualifiedName~TerminalTimelineLayoutTests" --nologo
```

Expected: all selected tests pass.

- [ ] **Step 9: Commit Task 3**

```powershell
git add -- `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalToolCallFormatting.cs `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalTimelineLayout.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalToolCallFormattingTests.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineLayoutTests.cs
git commit -m "feat: render tool calls in terminal thoughts" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: End-to-End Lifecycle and Regression Coverage

**Files:**
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs`
- Modify: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs`
- Modify: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md`

**Interfaces:**
- Consumes: the full interpreter → keyed state → Thoughts timeline pipeline from
  Tasks 1–3.
- Produces: acceptance coverage and user-facing documentation.

- [ ] **Step 1: Add a failing end-to-end lifecycle regression**

Use representative started and completed activities and process them through
one interpreter and one Thoughts state:

```csharp
[Fact]
public void ToolCallLifecycle_UpdatesOneThoughtBlockFromRunningToCompleted()
{
    ActivityInterpreter interpreter = new();
    TerminalChatState thoughts = new(
        entry => entry.Kind is ChatEntryKind.Thought or ChatEntryKind.ToolCall);

    thoughts.Apply(interpreter.Process(StartedWeatherActivity(), ActivityDirection.Inbound));
    Assert.Single(thoughts.Entries);
    Assert.Equal("started", thoughts.Entries[0].ToolCall!.Status);

    thoughts.Apply(interpreter.Process(CompletedWeatherActivity(), ActivityDirection.Inbound));
    ChatEntry completed = Assert.Single(thoughts.Entries);
    Assert.Equal("completed", completed.ToolCall!.Status);
    Assert.Equal(2971, completed.ToolCall.DurationMs);

    TimelineLayoutResult layout = TerminalTimelineLayout.Build(
        thoughts.Entries,
        80,
        TimelineGlyphSet.Unicode,
        collapseCompletedThoughts: false);
    string rendered = Render(layout);
    Assert.Contains("Completed in 2.97 s", rendered);
    Assert.DoesNotContain("must-not-render", rendered, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the end-to-end test and verify RED if any integration is missing**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ToolCallLifecycle_UpdatesOneThoughtBlock" --nologo
```

Expected: PASS if Tasks 1–3 are fully wired; otherwise fail at the missing
integration boundary and fix only that boundary before continuing.

- [ ] **Step 3: Add regression cases for stream and ordinary activity coexistence**

Add assertions that a streaming informative tool-call activity produces both:

- The existing transient Status change (`Calling current_weather...`).
- One `ToolCall` upsert.

Also assert ordinary Thought entities in the same activity continue to produce
their existing entries and attachments still project normally.

- [ ] **Step 4: Document the Thoughts tool-call block**

Update the terminal sample README's keyboard/Thoughts section with:

```markdown
- **F2 Thoughts** shows agent reasoning and structured tool-call lifecycle
  blocks. Tool calls update in place from Running to Completed and show the
  visible parameter snapshot plus elapsed time. Hidden parameters and raw tool
  results are not shown there; use Activities when raw protocol inspection is
  required.
```

Do not claim support for statuses or fields not covered by the parser tests.

- [ ] **Step 5: Run complete verification**

Run:

```powershell
dotnet test .\src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --nologo
dotnet build .\src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --nologo --no-restore
git diff --check -- `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests `
  docs\superpowers
git status --short
```

Expected:

- All terminal tests pass.
- Build succeeds with zero warnings and zero errors.
- No whitespace errors in changed source, tests, or docs.
- Only the user's pre-existing `appsettings.json` modification remains outside
  the feature changes.

- [ ] **Step 6: Request focused code review**

Review the complete feature diff against:

- Stable lifecycle correlation.
- Completion snapshot replacement.
- Streaming coexistence.
- Hidden/result exclusion.
- Duration boundaries.
- Tool-only Thoughts routing.
- Terminal wrapping and sanitation.

Resolve every Critical, High, or Medium finding before committing.

- [ ] **Step 7: Commit Task 4**

```powershell
git add -- `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalChatApplicationTests.cs `
  src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalTimelineViewTests.cs `
  src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md
git commit -m "test: cover terminal tool call thoughts" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```
