# Copilot Studio Terminal Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a cross-platform, full-screen Copilot Studio terminal client sample with Web Chat-style conversation rendering, Teams-compatible streaming updates, and formatted inbound/outbound activity inspection.

**Architecture:** Keep Copilot Studio transport, activity journaling, protocol interpretation, presentation state, and Terminal.Gui controls in separate units. A `ConversationSession` publishes activities into an immutable journal and stateful interpreter; `TerminalChatApplication` observes their events and marshals updates through Terminal.Gui's `IApplication.Invoke`.

**Tech Stack:** .NET 10, Microsoft 365 Agents SDK project references, Terminal.Gui 2.5.0, System.Text.Json, ProtocolJsonSerializer, Microsoft.Extensions.Hosting/DI, MSAL, xUnit.

**Spec:** `docs\superpowers\specs\2026-09-11-copilot-studio-terminal-client-design.md`

## Global Constraints

- Create the sample at `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\`.
- Create tests at `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\`.
- Target `net10.0`.
- Support Windows, macOS, and Linux interactive terminals.
- Use Terminal.Gui as the only terminal UI framework.
- Use `ProtocolJsonSerializer` and System.Text.Json; do not add Newtonsoft.Json.
- Manage the Terminal.Gui version centrally in `Directory.Packages.props`.
- Default to `--layout tabs`; support `--layout split` and `--help`.
- Retain inbound and outbound activities for the process lifetime.
- Never open a received URL without an explicit user action.
- Do not connect automated tests to Copilot Studio or require credentials.
- Follow TDD for protocol, state, options, and session behavior.

---

## File Structure

### Sample project

- `CopilotStudioClient.Terminal.csproj` — executable definition and dependencies.
- `Program.cs` — option parsing, DI registration, host lifetime, and exit-code mapping.
- `TerminalOptions.cs` — command-line model, parser, and usage text.
- `SampleConnectionSettings.cs` — Copilot Studio and authentication configuration.
- `Authentication\AddTokenHandler.cs` — interactive MSAL authentication.
- `Authentication\AddTokenHandlerS2S.cs` — service-principal authentication.
- `Conversation\ICopilotConversationClient.cs` — test seam around `CopilotClient`.
- `Conversation\CopilotConversationClient.cs` — SDK adapter.
- `Conversation\ConversationSession.cs` — serialized start/send lifecycle and events.
- `Models\ActivityRecord.cs` — immutable activity/diagnostic journal item.
- `Models\ChatEntry.cs` — presentation-neutral chat item and mutations.
- `Protocol\ActivityJsonFormatter.cs` — SDK activity to indented JSON.
- `Protocol\ActivityJournal.cs` — ordered, thread-safe process-lifetime record store.
- `Protocol\AdaptiveCardLinkExtractor.cs` — normalized card traversal.
- `Protocol\ActivityInterpreter.cs` — ordinary activity, entity, attachment, and stream interpretation.
- `UI\ITerminalView.cs` — presenter-facing UI contract.
- `UI\TerminalPresenter.cs` — session/journal/interpreter orchestration independent of widgets.
- `UI\TerminalChatApplication.cs` — Terminal.Gui v2 controls, layouts, and key bindings.
- `Properties\AssemblyInfo.cs` — friend access for the unsigned test assembly.
- `appsettings.json` — sample configuration template.
- `README.md` — setup, operation, semantics, and test documentation.

### Test project

- `Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj` — unsigned net10.0 xUnit project.
- `TerminalOptionsTests.cs` — option/default/help/error behavior.
- `ActivityJsonFormatterTests.cs` — indented complete activity representation.
- `ActivityJournalTests.cs` — ordering, direction, diagnostics, and concurrency.
- `AdaptiveCardLinkExtractorTests.cs` — cards with links, cards without actions, and malformed content.
- `ActivityInterpreterTests.cs` — messages, events, thoughts, suggested actions, attachments, and streams.
- `ConversationSessionTests.cs` — start/send serialization, publication ordering, cancellation, and failures.
- `TerminalPresenterTests.cs` — UI-independent presentation orchestration.
- `Fakes\FakeCopilotConversationClient.cs` — deterministic async transport.
- `Fakes\FakeTerminalView.cs` — captures presenter commands.

---

### Task 1: Project skeleton and command-line options

**Files:**
- Modify: `Directory.Packages.props`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\TerminalOptions.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Properties\AssemblyInfo.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalOptionsTests.cs`

**Interfaces:**
- Produces: `TerminalLayout`, `TerminalOptions`, and `TerminalOptions.Parse(string[])`.
- Produces: centrally managed `Terminal.Gui` version `2.5.0`.
- Consumes: no earlier task.

- [ ] **Step 1: Add the test project and failing option tests**

Create an unsigned test project so the sample can grant friend access without
copying the repository signing key:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ComponentAreaName>CplTests.CopilotStudioTerminal</ComponentAreaName>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <Import Project="..\..\Build.Common.core.props" />
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
</Project>
```

Create tests with these cases:

```csharp
[Fact]
public void Parse_NoArguments_UsesTabs()
{
    TerminalOptions result = TerminalOptions.Parse([]);
    Assert.Equal(TerminalLayout.Tabs, result.Layout);
    Assert.False(result.ShowHelp);
}

[Theory]
[InlineData("tabs", TerminalLayout.Tabs)]
[InlineData("split", TerminalLayout.Split)]
public void Parse_Layout_UsesRequestedLayout(string value, TerminalLayout expected)
{
    TerminalOptions result = TerminalOptions.Parse(["--layout", value]);
    Assert.Equal(expected, result.Layout);
}

[Fact]
public void Parse_Help_DoesNotRequireLayout()
{
    TerminalOptions result = TerminalOptions.Parse(["--help"]);
    Assert.True(result.ShowHelp);
}

[Fact]
public void Parse_UnknownLayout_ThrowsOptionException()
{
    TerminalOptionException error = Assert.Throws<TerminalOptionException>(
        () => TerminalOptions.Parse(["--layout", "drawer"]));
    Assert.Contains("tabs", error.Message);
    Assert.Contains("split", error.Message);
}
```

- [ ] **Step 2: Run the tests and verify the project fails to compile**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalOptionsTests"
```

Expected: FAIL because the sample project and option types do not exist.

- [ ] **Step 3: Add the centrally managed package and sample project**

Add this package version to the non-test package section of
`Directory.Packages.props`:

```xml
<PackageVersion Include="Terminal.Gui" Version="2.5.0" />
```

Create the executable project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IncludeAspNetSampleHelpers>false</IncludeAspNetSampleHelpers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Microsoft.Identity.Client.Extensions.Msal" />
    <PackageReference Include="Terminal.Gui" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\libraries\Client\Microsoft.Agents.CopilotStudio.Client\Microsoft.Agents.CopilotStudio.Client.csproj" />
  </ItemGroup>
</Project>
```

Grant test access in `Properties\AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Microsoft.Agents.CopilotStudio.Terminal.Tests")]
```

- [ ] **Step 4: Implement strict option parsing**

Use these exact types:

```csharp
internal enum TerminalLayout
{
    Tabs,
    Split
}

internal sealed record TerminalOptions(TerminalLayout Layout, bool ShowHelp)
{
    internal const string Usage =
        "Usage: dotnet run --project CopilotStudioClient.Terminal.csproj -- [--layout tabs|split] [--help]";

    public static TerminalOptions Parse(string[] args)
    {
        TerminalLayout layout = TerminalLayout.Tabs;
        bool showHelp = false;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--layout" when index + 1 < args.Length:
                    layout = args[++index].ToLowerInvariant() switch
                    {
                        "tabs" => TerminalLayout.Tabs,
                        "split" => TerminalLayout.Split,
                        string value => throw new TerminalOptionException(
                            $"Unsupported layout '{value}'. Expected tabs or split.")
                    };
                    break;
                case "--layout":
                    throw new TerminalOptionException("--layout requires tabs or split.");
                default:
                    throw new TerminalOptionException($"Unknown option '{args[index]}'.");
            }
        }

        return new TerminalOptions(layout, showHelp);
    }
}

internal sealed class TerminalOptionException(string message) : Exception(message);
```

- [ ] **Step 5: Run the option tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalOptionsTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the project skeleton**

```powershell
git add Directory.Packages.props src\samples\CopilotStudioClient\CopilotStudioClient.Terminal src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests
git commit -m "feat: scaffold Copilot Studio terminal sample" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 2: Activity JSON and ordered journal

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ActivityRecord.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityJsonFormatter.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityJournal.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityJsonFormatterTests.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityJournalTests.cs`

**Interfaces:**
- Produces: `ActivityJsonFormatter.Format(Activity)`.
- Produces: `ActivityJournal.Append(Activity, ActivityDirection)` and `AppendDiagnostic`.
- Produces: `ActivityRecordAdded` event and immutable snapshots.
- Consumes: `Microsoft.Agents.Core.Models.Activity`.

- [ ] **Step 1: Write failing formatter and journal tests**

```csharp
[Fact]
public void Format_UsesProtocolNamesAndIndentation()
{
    Activity activity = new()
    {
        Type = ActivityTypes.Message,
        Text = "hello",
        Entities = [new Entity("custom") { Properties = { ["answer"] = JsonSerializer.SerializeToElement(42) } }]
    };

    string json = ActivityJsonFormatter.Format(activity);

    Assert.Contains(Environment.NewLine, json);
    Assert.Contains("\"type\": \"message\"", json);
    Assert.Contains("\"answer\": 42", json);
}

[Fact]
public void Append_AssignsStableIncreasingSequenceAndDirection()
{
    ActivityJournal journal = new();
    ActivityRecord outbound = journal.Append(new Activity { Type = "message" }, ActivityDirection.Outbound);
    ActivityRecord inbound = journal.Append(new Activity { Type = "typing" }, ActivityDirection.Inbound);

    Assert.Equal(1, outbound.Sequence);
    Assert.Equal(2, inbound.Sequence);
    Assert.Equal(ActivityDirection.Outbound, outbound.Direction);
    Assert.Equal(ActivityDirection.Inbound, inbound.Direction);
    Assert.Equal([outbound, inbound], journal.Snapshot());
}

[Fact]
public async Task Append_ConcurrentCalls_DoNotDuplicateSequenceNumbers()
{
    ActivityJournal journal = new();
    await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(
        () => journal.Append(new Activity { Type = "event" }, ActivityDirection.Inbound))));

    long[] sequence = journal.Snapshot().Select(record => record.Sequence).Order().ToArray();
    Assert.Equal(Enumerable.Range(1, 100).Select(value => (long)value), sequence);
}

[Fact]
public void Append_SerializationFailure_PreservesActivityAndAddsDiagnostic()
{
    ActivityJournal journal = new(_ => throw new JsonException("bad payload"));
    List<ActivityRecord> added = [];
    journal.RecordAdded += (_, record) => added.Add(record);

    ActivityRecord activity = journal.Append(
        new Activity { Type = "message", Text = "hello" },
        ActivityDirection.Inbound);

    Assert.Null(activity.Json);
    Assert.Equal(2, added.Count);
    Assert.Equal(ActivityDirection.Inbound, added[0].Direction);
    Assert.Equal(ActivityDirection.Diagnostic, added[1].Direction);
    Assert.Contains("bad payload", added[1].Summary);
}
```

- [ ] **Step 2: Run the focused tests and verify failure**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJsonFormatterTests|FullyQualifiedName~ActivityJournalTests"
```

Expected: FAIL because journal and formatter types do not exist.

- [ ] **Step 3: Implement immutable records and formatting**

Define:

```csharp
internal enum ActivityDirection
{
    Inbound,
    Outbound,
    Diagnostic
}

internal enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

internal sealed record ActivityRecord(
    long Sequence,
    ActivityDirection Direction,
    DateTimeOffset Timestamp,
    string Type,
    string Summary,
    Activity? Activity,
    string? Json,
    DiagnosticSeverity? Severity);
```

Implement formatting without modifying global serializer options:

```csharp
internal static class ActivityJsonFormatter
{
    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        WriteIndented = true
    };

    public static string Format(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        string protocolJson = ProtocolJsonSerializer.ToJson(activity);
        using JsonDocument document = JsonDocument.Parse(protocolJson);
        return JsonSerializer.Serialize(document.RootElement, IndentedOptions);
    }
}
```

- [ ] **Step 4: Implement the locked journal**

Use one lock around sequence allocation and list mutation. Raise the event after
leaving the lock:

```csharp
internal sealed class ActivityJournal
{
    private readonly object _gate = new();
    private readonly List<ActivityRecord> _records = [];
    private readonly Func<Activity, string> _formatter;
    private long _nextSequence;

    public ActivityJournal(Func<Activity, string>? formatter = null)
    {
        _formatter = formatter ?? ActivityJsonFormatter.Format;
    }

    public event EventHandler<ActivityRecord>? RecordAdded;

    public ActivityRecord Append(Activity activity, ActivityDirection direction)
    {
        ArgumentNullException.ThrowIfNull(activity);
        string? json = null;
        Exception? serializationError = null;
        try
        {
            json = _formatter(activity);
        }
        catch (Exception exception) when (
            exception is JsonException
            or NotSupportedException
            or InvalidOperationException)
        {
            serializationError = exception;
        }

        ActivityRecord record = Add(direction, activity.Type ?? "unknown",
            activity.Text ?? activity.Name ?? string.Empty, activity, json, null);
        RecordAdded?.Invoke(this, record);
        if (serializationError is not null)
        {
            AppendDiagnostic(
                $"Unable to format activity JSON: {serializationError.Message}",
                DiagnosticSeverity.Error);
        }
        return record;
    }

    public ActivityRecord AppendDiagnostic(string message, DiagnosticSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ActivityRecord record = Add(ActivityDirection.Diagnostic, "diagnostic",
            message, null, null, severity);
        RecordAdded?.Invoke(this, record);
        return record;
    }

    public IReadOnlyList<ActivityRecord> Snapshot()
    {
        lock (_gate)
        {
            return _records.ToArray();
        }
    }

    private ActivityRecord Add(
        ActivityDirection direction,
        string type,
        string summary,
        Activity? activity,
        string? json,
        DiagnosticSeverity? severity)
    {
        lock (_gate)
        {
            ActivityRecord record = new(
                ++_nextSequence, direction, DateTimeOffset.UtcNow,
                type, summary, activity, json, severity);
            _records.Add(record);
            return record;
        }
    }
}
```

- [ ] **Step 5: Run formatter and journal tests**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityJsonFormatterTests|FullyQualifiedName~ActivityJournalTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the protocol journal**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests
git commit -m "feat: add activity journal and JSON formatting" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 3: Adaptive-card link extraction

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ChatEntry.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\AdaptiveCardLinkExtractor.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\AdaptiveCardLinkExtractorTests.cs`

**Interfaces:**
- Produces: `ChatLink`, `LinkExtractionResult`.
- Produces: `AdaptiveCardLinkExtractor.Extract(object?)`.
- Consumes: attachment `Content` as string, `JsonElement`, or another serializable object.

- [ ] **Step 1: Write failing extraction tests**

Cover a login-style card, a nested action, the observed action-free FactSet
card shape, and malformed JSON:

```csharp
[Fact]
public void Extract_ReturnsAllNestedOpenUrls()
{
    const string card = """
        {
          "type": "AdaptiveCard",
          "body": [
            {
              "type": "ActionSet",
              "actions": [
                { "type": "Action.OpenUrl", "title": "Sign in", "url": "https://login.example/" }
              ]
            }
          ],
          "actions": [
            { "type": "Action.OpenUrl", "title": "Help", "url": "https://help.example/" }
          ]
        }
        """;

    LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

    Assert.Null(result.Error);
    Assert.Equal(
        [new ChatLink("Sign in", new Uri("https://login.example/")),
         new ChatLink("Help", new Uri("https://help.example/"))],
        result.Links);
}

[Fact]
public void Extract_ActionFreeCard_IsSuccessfulWithNoLinks()
{
    JsonElement card = JsonSerializer.SerializeToElement(new
    {
        type = "AdaptiveCard",
        body = new object[]
        {
            new { type = "TextBlock", text = "Authentication status" },
            new { type = "FactSet", facts = new[] { new { title = "State", value = "Ready" } } }
        }
    });

    LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract(card);

    Assert.Empty(result.Links);
    Assert.Null(result.Error);
}

[Fact]
public void Extract_MalformedJson_ReturnsError()
{
    LinkExtractionResult result = AdaptiveCardLinkExtractor.Extract("{not-json");
    Assert.Empty(result.Links);
    Assert.NotNull(result.Error);
}
```

- [ ] **Step 2: Run the extractor tests and verify failure**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~AdaptiveCardLinkExtractorTests"
```

Expected: FAIL because the extractor and chat models do not exist.

- [ ] **Step 3: Add chat presentation records**

```csharp
internal enum ChatEntryKind
{
    User,
    Agent,
    Status,
    Event,
    Thought,
    Attachment,
    Diagnostic
}

internal sealed record ChatLink(string Title, Uri Url);

internal sealed record ChatAction(string Title, string? Value);

internal sealed record ChatEntry(
    string Key,
    ChatEntryKind Kind,
    string Author,
    string Text,
    bool IsTransient,
    IReadOnlyList<ChatLink> Links,
    IReadOnlyList<ChatAction> SuggestedActions);

internal enum ChatChangeKind
{
    Upsert,
    Remove
}

internal sealed record ChatChange(ChatChangeKind Kind, string Key, ChatEntry? Entry);

internal sealed record LinkExtractionResult(
    IReadOnlyList<ChatLink> Links,
    string? Error);
```

- [ ] **Step 4: Implement normalized recursive traversal**

Parse strings, clone `JsonElement`, and serialize other runtime objects with
the protocol options. Traverse arrays and objects recursively. Match
`Action.OpenUrl` case-insensitively, require an absolute HTTP or HTTPS URL, and
use `title` or the URL as display text:

```csharp
private static void Visit(JsonElement element, List<ChatLink> links)
{
    if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (JsonElement item in element.EnumerateArray())
        {
            Visit(item, links);
        }
        return;
    }

    if (element.ValueKind != JsonValueKind.Object)
    {
        return;
    }

    if (element.TryGetProperty("type", out JsonElement type)
        && string.Equals(type.GetString(), "Action.OpenUrl", StringComparison.OrdinalIgnoreCase)
        && element.TryGetProperty("url", out JsonElement urlElement)
        && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out Uri? url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
    {
        string title = element.TryGetProperty("title", out JsonElement titleElement)
            ? titleElement.GetString() ?? url.AbsoluteUri
            : url.AbsoluteUri;
        links.Add(new ChatLink(title, url));
    }

    foreach (JsonProperty property in element.EnumerateObject())
    {
        Visit(property.Value, links);
    }
}
```

Catch only `JsonException` and `NotSupportedException` at normalization and
return their message in `LinkExtractionResult.Error`.

- [ ] **Step 5: Run the extractor tests**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~AdaptiveCardLinkExtractorTests"
```

Expected: PASS.

- [ ] **Step 6: Commit adaptive-card handling**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Models\ChatEntry.cs src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\AdaptiveCardLinkExtractor.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\AdaptiveCardLinkExtractorTests.cs
git commit -m "feat: extract adaptive card links" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 4: Activity and stream interpretation

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityInterpreter.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs`

**Interfaces:**
- Produces: `ActivityInterpreter.Process(Activity, ActivityDirection): IReadOnlyList<ChatChange>`.
- Consumes: `ChatEntry`, `ChatChange`, `AdaptiveCardLinkExtractor`, and SDK `StreamInfo`.
- Maintains: open stream state by stream ID and last non-final sequence.

- [ ] **Step 1: Write failing ordinary activity tests**

```csharp
[Fact]
public void Process_ThoughtAndUnknownEntity_ShowsOnlyThought()
{
    Activity activity = new()
    {
        Type = ActivityTypes.Message,
        Text = "Answer",
        Entities =
        [
            new Entity("thoughts") { Properties = { ["text"] = JsonSerializer.SerializeToElement("Checking mail") } },
            new Entity("clientInfo") { Properties = { ["platform"] = JsonSerializer.SerializeToElement("Web") } }
        ]
    };

    IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Agent && change.Entry.Text == "Answer");
    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Thought && change.Entry.Text == "Checking mail");
    Assert.DoesNotContain(changes, change => change.Entry?.Text.Contains("clientInfo") == true);
}

[Fact]
public void Process_ActionFreeAdaptiveCard_AddsAttachmentWithoutDiagnostic()
{
    Activity activity = CreateMessageWithCard("""{"type":"AdaptiveCard","body":[{"type":"FactSet","facts":[]}]}""");
    IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Attachment);
    Assert.DoesNotContain(changes, change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
}

[Fact]
public void Process_MalformedAdaptiveCard_PreservesAttachmentAndAddsDiagnostic()
{
    Activity activity = CreateMessageWithCard("{bad");
    IReadOnlyList<ChatChange> changes = new ActivityInterpreter().Process(activity, ActivityDirection.Inbound);

    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Attachment);
    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
}
```

- [ ] **Step 2: Write transcript-derived failing stream tests**

Use synthetic IDs, not tenant/user data from the reference transcript:

```csharp
[Fact]
public void Process_TranscriptStream_UsesStartActivityIdAndFinalWithoutSequence()
{
    ActivityInterpreter interpreter = new();
    Activity start = StreamActivity("typing", "start-1", "Just a moment please..",
        streamId: null, StreamTypes.Informative, sequence: 1);
    Activity update = StreamActivity("typing", "update-1", "Loading tools...",
        streamId: "start-1", StreamTypes.Informative, sequence: 2);
    Activity chunk = StreamActivity("typing", "update-2", "Hello! How can I assist?",
        streamId: "start-1", StreamTypes.Streaming, sequence: 3);
    Activity final = StreamActivity("message", "final-1", "Hello! How can I assist?",
        streamId: "start-1", StreamTypes.Final, sequence: null);

    AssertStatus(interpreter.Process(start, ActivityDirection.Inbound), "Just a moment please..");
    AssertStatus(interpreter.Process(update, ActivityDirection.Inbound), "Loading tools...");
    AssertAgentUpsert(interpreter.Process(chunk, ActivityDirection.Inbound), "Hello! How can I assist?", transient: true);
    IReadOnlyList<ChatChange> finalChanges = interpreter.Process(final, ActivityDirection.Inbound);
    AssertAgentUpsert(finalChanges, "Hello! How can I assist?", transient: false);
    Assert.Contains(finalChanges, change => change.Kind == ChatChangeKind.Remove);
}

[Fact]
public void Process_StreamingText_ReplacesRatherThanAppends()
{
    ActivityInterpreter interpreter = new();
    interpreter.Process(StreamActivity("typing", "s", "A brown", null, StreamTypes.Streaming, 1), ActivityDirection.Inbound);
    IReadOnlyList<ChatChange> changes = interpreter.Process(
        StreamActivity("typing", "u", "A brown fox", "s", StreamTypes.Streaming, 2),
        ActivityDirection.Inbound);

    AssertAgentUpsert(changes, "A brown fox", transient: true);
}

[Fact]
public void Process_SequenceRegression_AddsDiagnosticWithoutReplacingResponse()
{
    ActivityInterpreter interpreter = new();
    interpreter.Process(StreamActivity("typing", "s", "First", null, StreamTypes.Streaming, 2), ActivityDirection.Inbound);
    IReadOnlyList<ChatChange> changes = interpreter.Process(
        StreamActivity("typing", "u", "Older", "s", StreamTypes.Streaming, 1),
        ActivityDirection.Inbound);

    Assert.Contains(changes, change => change.Entry?.Kind == ChatEntryKind.Diagnostic);
    Assert.DoesNotContain(changes, change => change.Entry?.Kind == ChatEntryKind.Agent);
}
```

Also add tests for separate stream IDs, an ambiguous update without either ID,
`streamResult: error`, ordinary typing, events, suggested actions, and both
`thought` and `thoughts` type spellings.

- [ ] **Step 3: Run interpreter tests and verify failure**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityInterpreterTests"
```

Expected: FAIL because `ActivityInterpreter` does not exist.

- [ ] **Step 4: Implement ordinary activity interpretation**

Implement `Process` in this order:

1. Detect `StreamInfo` with `activity.GetStreamingEntity()`.
2. If streaming metadata exists, delegate to `ProcessStream`.
3. Add the ordinary message/event/typing entry.
4. Add thought entries for entity types `thought` and `thoughts`,
   case-insensitively. Read the first string among `text`, `content`,
   `description`, and `value`; if none exists, use indented entity JSON.
5. Add attachment entries.
6. For adaptive-card attachments, call `AdaptiveCardLinkExtractor.Extract`,
   attach returned links, and add a diagnostic only when `Error` is non-null.
7. Convert `SuggestedActions.Actions` to `ChatAction`, preferring `Text`, then
   `Title`, and retaining `Value?.ToString()`.

Use deterministic keys based on `activity.Id` when present and a private
monotonic fallback when it is absent.

- [ ] **Step 5: Implement stream state and phase rules**

Use:

```csharp
private sealed record OpenStream(
    string Id,
    string ResponseKey,
    int? LastSequence);

private readonly Dictionary<string, OpenStream> _streams =
    new(StringComparer.Ordinal);
```

Resolution rules:

```csharp
string? streamId = streamInfo.StreamId;
if (string.IsNullOrWhiteSpace(streamId))
{
    streamId = activity.Id;
}
if (string.IsNullOrWhiteSpace(streamId) && _streams.Count == 1)
{
    streamId = _streams.Keys.Single();
}
if (string.IsNullOrWhiteSpace(streamId))
{
    return Diagnostic("Streaming activity has no unambiguous stream identifier.");
}
```

For starts, create `OpenStream(streamId, $"stream:{streamId}:response",
streamInfo.StreamSequence)`. For continuation activities, reject a non-final
sequence less than or equal to `LastSequence`. Informative updates upsert
`stream:{streamId}:status`. Streaming updates remove that status and upsert the
response as transient. Final updates are permitted to omit sequence; they
remove status, upsert
the response as non-transient, and remove the stream from `_streams`.

When `StreamResult` equals `StreamResults.Error`, add a diagnostic after
finalizing the visible text.

- [ ] **Step 6: Run all protocol tests**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ActivityInterpreterTests|FullyQualifiedName~AdaptiveCardLinkExtractorTests|FullyQualifiedName~ActivityJournalTests|FullyQualifiedName~ActivityJsonFormatterTests"
```

Expected: PASS.

- [ ] **Step 7: Commit activity interpretation**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Protocol\ActivityInterpreter.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ActivityInterpreterTests.cs
git commit -m "feat: interpret Copilot activity streams" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 5: Copilot transport and conversation session

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Conversation\ICopilotConversationClient.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Conversation\CopilotConversationClient.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Conversation\ConversationSession.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Fakes\FakeCopilotConversationClient.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ConversationSessionTests.cs`

**Interfaces:**
- Produces: `ICopilotConversationClient.StartAsync` and `ExecuteAsync`.
- Produces: `ConversationSession.StartAsync`, `SendAsync`, `ActivityPublished`, `BusyChanged`, and `Failed`.
- Consumes: `CopilotClient`, `ActivityJournal`, `ActivityInterpreter`.

- [ ] **Step 1: Write the fake transport and failing session tests**

Define the seam:

```csharp
internal interface ICopilotConversationClient
{
    IAsyncEnumerable<Activity> StartAsync(string conversationId, CancellationToken cancellationToken);
    IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        CancellationToken cancellationToken);
}
```

The fake stores execute requests and exposes configured async sequences:

```csharp
internal sealed class FakeCopilotConversationClient : ICopilotConversationClient
{
    public IReadOnlyList<Activity> StartActivities { get; init; } = [];
    public IReadOnlyList<Activity> ExecuteActivities { get; init; } = [];
    public List<Activity> Requests { get; } = [];

    public async IAsyncEnumerable<Activity> StartAsync(
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (Activity activity in StartActivities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return activity;
            await Task.Yield();
        }
    }

    public async IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Requests.Add(activity);
        foreach (Activity response in ExecuteActivities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return response;
            await Task.Yield();
        }
    }
}
```

Add tests proving start activities are inbound, outbound is published before
responses, a second send while busy throws `InvalidOperationException`,
cancellation is propagated without a failure event, and a transport exception
raises `Failed` after resetting busy state.

- [ ] **Step 2: Run session tests and verify failure**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ConversationSessionTests"
```

Expected: FAIL because `ConversationSession` and the adapter do not exist.

- [ ] **Step 3: Implement the SDK adapter**

```csharp
internal sealed class CopilotConversationClient(CopilotClient client)
    : ICopilotConversationClient
{
    public IAsyncEnumerable<Activity> StartAsync(
        string conversationId,
        CancellationToken cancellationToken) =>
        client.StartConversationAsync(
            new StartRequest
            {
                EmitStartConversationEvent = true,
                ConversationId = conversationId
            },
            cancellationToken);

    public IAsyncEnumerable<Activity> ExecuteAsync(
        string conversationId,
        Activity activity,
        CancellationToken cancellationToken) =>
        client.ExecuteAsync(conversationId, activity, cancellationToken);
}
```

- [ ] **Step 4: Implement serialized session lifecycle**

Construct one conversation ID:

```csharp
private readonly string _conversationId = $"Sample-{Guid.NewGuid():N}";
private int _busy;

public event EventHandler<PublishedActivityEventArgs>? ActivityPublished;
public event EventHandler<bool>? BusyChanged;
public event EventHandler<Exception>? Failed;
```

`StartAsync` enumerates transport start activities and publishes each inbound.
`SendAsync` rejects whitespace, atomically changes `_busy` from 0 to 1, creates
the outbound activity with `MessageFactory.CreateMessageActivity(text)`,
publishes it, then enumerates inbound responses. Catch
`OperationCanceledException` only when the supplied token is canceled; catch
other exceptions to raise `Failed` and rethrow. Reset `_busy` and raise
`BusyChanged(false)` in `finally`.

`PublishedActivityEventArgs` contains `Activity Activity` and
`ActivityDirection Direction`.

- [ ] **Step 5: Run session tests**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~ConversationSessionTests"
```

Expected: PASS.

- [ ] **Step 6: Commit conversation transport**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Conversation src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\ConversationSessionTests.cs src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Fakes
git commit -m "feat: add Copilot conversation session" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 6: Presenter orchestration

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\ITerminalView.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalPresenter.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Fakes\FakeTerminalView.cs`
- Create: `src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\TerminalPresenterTests.cs`

**Interfaces:**
- Produces: `ITerminalView`.
- Produces: `TerminalPresenter.StartAsync`, `SendAsync`, and event subscriptions.
- Consumes: `ConversationSession`, `ActivityJournal`, and `ActivityInterpreter`.

- [ ] **Step 1: Define the view seam and write failing presenter tests**

```csharp
internal interface ITerminalView
{
    void AddActivity(ActivityRecord record);
    void ApplyChatChanges(IReadOnlyList<ChatChange> changes);
    void SetBusy(bool isBusy);
    void SetStatus(string text, DiagnosticSeverity severity);
}
```

Test that a published activity is journaled before its chat changes reach the
view, busy events disable/enable input, transport failures become both a
journal diagnostic and visible error status, and whitespace send requests
produce an informational status without invoking the session.

- [ ] **Step 2: Run presenter tests and verify failure**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalPresenterTests"
```

Expected: FAIL because presenter and view contract do not exist.

- [ ] **Step 3: Implement presenter event ordering**

In the constructor, subscribe to:

```csharp
session.ActivityPublished += OnActivityPublished;
session.BusyChanged += (_, busy) => view.SetBusy(busy);
session.Failed += OnSessionFailed;
journal.RecordAdded += (_, record) => view.AddActivity(record);
```

Handle activities in this exact order:

```csharp
private void OnActivityPublished(object? sender, PublishedActivityEventArgs args)
{
    _journal.Append(args.Activity, args.Direction);
    IReadOnlyList<ChatChange> changes =
        _interpreter.Process(args.Activity, args.Direction);
    _view.ApplyChatChanges(changes);
}
```

On non-cancellation failure, append an error diagnostic and set error status.
`SendAsync` trims input, reports an informational status for empty input, and
otherwise awaits `ConversationSession.SendAsync`.

- [ ] **Step 4: Run presenter and lower-layer tests**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --filter "FullyQualifiedName~TerminalPresenterTests|FullyQualifiedName~ConversationSessionTests|FullyQualifiedName~ActivityInterpreterTests"
```

Expected: PASS.

- [ ] **Step 5: Commit the presenter**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests
git commit -m "feat: orchestrate terminal presentation state" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 7: Terminal.Gui tab and split applications

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\UI\TerminalChatApplication.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Program.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\SampleConnectionSettings.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Authentication\AddTokenHandler.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\Authentication\AddTokenHandlerS2S.cs`
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\appsettings.json`

**Interfaces:**
- Produces: executable tab/split UI.
- Consumes: `TerminalPresenter`, `TerminalOptions`, Terminal.Gui v2.5.0, and existing Copilot connection/auth patterns.

- [ ] **Step 1: Add configuration and authentication implementations**

Create `SampleConnectionSettings`, `AddTokenHandler`, and
`AddTokenHandlerS2S` from the existing source at:

- `src\samples\CopilotStudioClient\CopilotStudioClient\SampleConnectionSettings.cs`
- `src\samples\CopilotStudioClient\CopilotStudioClient\AddTokenHandler.cs`
- `src\samples\CopilotStudioClient\CopilotStudioClient\AddTokenHandlerS2S.cs`

Preserve their authentication behavior exactly and change only the namespace to
`CopilotStudioClient.Terminal`. Keep the token cache names so both samples can
reuse a valid cache. Copy the existing `appsettings.json` keys and logging
levels without values.

- [ ] **Step 2: Build the sample and isolate UI API errors**

Run:

```powershell
dotnet build src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj
```

Expected: FAIL because `Program.cs` and `TerminalChatApplication` do not exist.

- [ ] **Step 3: Implement the Terminal.Gui application lifecycle**

Use the non-legacy v2 namespaces and lifecycle:

```csharp
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

using IApplication application = Application.Create();
application.Init();
using Window window = new()
{
    Title = "Copilot Studio Terminal Client",
    Width = Dim.Fill(),
    Height = Dim.Fill()
};
application.Run(window);
```

Start the presenter after initialization with cancellable background work.
Every `ITerminalView` method calls `application.Invoke` before changing
controls. Dispose/cancel subscriptions before disposing `IApplication` so
background callbacks cannot invoke a stopped application.

- [ ] **Step 4: Build reusable chat and inspector panes**

Create:

- a read-only `Markdown` transcript;
- a status `Label`;
- a single-line `TextField` composer;
- `ListView<ActivityRecord>` for journal records;
- a read-only, scrollable `TextView` for formatted JSON;
- a `FrameView` around chat and inspector regions;
- a `StatusBar` listing active shortcuts.

Maintain chat entries in insertion order plus a key-to-index dictionary.
Apply `Upsert` by replacing the existing keyed entry or appending a new one;
apply `Remove` by deleting the keyed transient entry. Regenerate Markdown from
the presentation-neutral list, escaping received text so it cannot inject
Terminal.Gui formatting controls.

Activity selection looks up `ActivityRecord.Sequence`, then assigns
`record.Json ?? record.Summary` to the JSON view. Do not auto-select a new
record when the user has moved selection away from the latest item.

- [ ] **Step 5: Implement default tabs**

Use Terminal.Gui v2 `Tabs`:

```csharp
View chatTab = BuildChatView();
chatTab.Title = "_Chat";
View activitiesTab = BuildActivitiesView();
activitiesTab.Title = "_Activities";
View helpTab = BuildHelpView();
helpTab.Title = "_Help";

Tabs tabs = new()
{
    Width = Dim.Fill(),
    Height = Dim.Fill()
};
tabs.Add(chatTab, activitiesTab, helpTab);
tabs.Value = chatTab;
window.Add(tabs);
```

Bind Ctrl+1, Ctrl+2, and Ctrl+3 to set `tabs.Value` and focus the relevant
control. Enter in the composer captures and clears its text, then starts
`presenter.SendAsync` without blocking the UI loop. `SetBusy` disables only
message submission, not navigation or quit.

- [ ] **Step 6: Implement split layout**

For `TerminalLayout.Split`, omit `Tabs`. Place the chat frame at
`X = 0`, `Y = 0`, `Width = Dim.Percent(55)`, `Height = Dim.Fill()`. Place the
inspector frame at `X = Pos.Right(chatFrame)`, `Y = 0`,
`Width = Dim.Fill()`, `Height = Dim.Fill()`. Include help text in a modal
dialog opened from the status bar or Ctrl+3.

Ctrl+1 focuses the composer; Ctrl+2 focuses the activity list. Both layouts use
the same controls, models, and presenter methods.

- [ ] **Step 7: Implement links, actions, copy, and quit**

Render each `ChatLink` as a Terminal.Gui `Link`. Its accepted action prompts
for explicit confirmation before opening with:

```csharp
Process.Start(new ProcessStartInfo(link.Url.AbsoluteUri)
{
    UseShellExecute = true
});
```

If opening fails, route the exception to `SetStatus` as an error. Suggested
actions populate the composer; activate-and-send only when the action has a
nonempty value.

Ctrl+C copies the focused link URL or selected activity JSON through
Terminal.Gui clipboard support. If unavailable, show the value in a selectable
modal and set an informational status instead of silently failing.

Ctrl+Q cancels the linked token, requests application stop, and waits for
background work before disposal. Startup and shutdown must restore the
terminal in `finally`.

- [ ] **Step 8: Implement composition root and exit codes**

`Program.cs` must:

1. Parse options before configuration or Terminal.Gui initialization.
2. Print `TerminalOptions.Usage` and return 0 for help.
3. Print option errors plus usage and return 2.
4. Reject redirected input/output with a clear message and return 2.
5. Configure the named `mcs` HTTP client and existing token handlers.
6. Register settings, `CopilotClient`, transport, session, journal,
   interpreter, presenter, and terminal application.
7. Run the terminal and return 0 on normal quit/cancellation.
8. Print startup failures after terminal restoration and return 1.

Use `Host.CreateApplicationBuilder(args)` for configuration/DI, but resolve and
run `TerminalChatApplication` directly rather than registering it as
`IHostedService`.

- [ ] **Step 9: Build and run help smoke checks**

```powershell
dotnet build src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj
dotnet run --project src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj -- --help
```

Expected: build succeeds and help exits without authentication or terminal
initialization.

- [ ] **Step 10: Manually smoke-test both layouts**

With configured credentials:

```powershell
dotnet run --project src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj
dotnet run --project src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj -- --layout split
```

Confirm tab navigation, sending, streaming replacement, activity selection,
formatted JSON, explicit URL activation, copy fallback, cancellation, and
terminal restoration. Do not record credentials or transcript identifiers.

- [ ] **Step 11: Commit the terminal UI**

```powershell
git add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal
git commit -m "feat: add interactive Copilot terminal UI" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

### Task 8: Documentation, solution wiring, and final verification

**Files:**
- Create: `src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md`
- Modify: `src\Microsoft.Agents.SDK.sln`

**Interfaces:**
- Produces: discoverable sample and test projects in the repository solution.
- Consumes: all earlier tasks.

- [ ] **Step 1: Write the sample README**

Document:

```markdown
# Copilot Studio Interactive Terminal Client

## Run

dotnet run --project CopilotStudioClient.Terminal.csproj
dotnet run --project CopilotStudioClient.Terminal.csproj -- --layout split

## Views

- Chat: conversation, streaming status, thoughts, suggested actions, and card links.
- Activities: inbound/outbound timeline and formatted SDK activity JSON.
- Help: shortcuts and behavior.

The activity inspector displays the complete Activity object after SDK
deserialization. It is not a byte-for-byte capture of the SSE transport.
```

Include the full interactive and service-principal setup from the existing
sample README, controls, explicit-link safety, Teams stream behavior, known
`thought`/`thoughts` handling, action-free cards, configuration keys, and the
targeted test command.

- [ ] **Step 2: Add both projects to the solution**

Use the .NET CLI so solution configuration GUIDs and nesting are valid:

```powershell
dotnet sln src\Microsoft.Agents.SDK.sln add src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\CopilotStudioClient.Terminal.csproj --solution-folder "Samples\CopilotStudioClient"
dotnet sln src\Microsoft.Agents.SDK.sln add src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj --solution-folder "Tests"
```

- [ ] **Step 3: Run the targeted test project**

```powershell
dotnet test src\tests\Microsoft.Agents.CopilotStudio.Terminal.Tests\Microsoft.Agents.CopilotStudio.Terminal.Tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Build both new projects through the solution**

```powershell
dotnet build src\Microsoft.Agents.SDK.sln --no-restore
```

Expected: PASS with warnings treated as errors.

- [ ] **Step 5: Verify package and repository conventions**

```powershell
git grep -n "<PackageReference Include=\"Terminal.Gui\" Version=" -- src
git status --short
git --no-pager diff --check
```

Expected: no project-level Terminal.Gui version, no unexpected generated files,
and no whitespace errors. Ensure `.superpowers\` visual companion artifacts
are not staged.

- [ ] **Step 6: Commit repository integration**

```powershell
git add src\Microsoft.Agents.SDK.sln src\samples\CopilotStudioClient\CopilotStudioClient.Terminal\README.md
git commit -m "docs: add Copilot terminal sample guidance" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

- [ ] **Step 7: Request code review**

Invoke the repository's `review` agent against the complete branch diff. Fix
valid Critical, High, and Medium findings, rerun the targeted tests and solution
build, and keep any rebuttals for false positives with the branch handoff.
