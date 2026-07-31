# Slack Outbound Parity and Adapter Refactoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Intercom-compatible Slack attachment and suggested-action rendering, remove `SlackAdapterOptions.BotName`, and reduce `SlackAdapter` to transport orchestration without changing AgentApplication behavior.

**Architecture:** Extract request validation/parsing, deduplication, inbound Activity conversion, outbound message conversion, attachment conversion, and file upload into focused internal components. Preserve the existing public adapter constructors while `AddSlack` composes the internal services. Outbound conversion may produce multiple Slack messages and returns the final Slack timestamp.

**Tech Stack:** C# 12, .NET 8, ASP.NET Core, Microsoft Agents SDK Activity models, System.Text.Json, Slack Web API, xUnit, Moq.

---

## File Structure

### New production files

- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestValidator.cs` — Slack HMAC and request-age validation.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestParser.cs` — Events API/interactivity request classification and parsing.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackEventDeduplicator.cs` — bounded event ID cache.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackActivityConverter.cs` — Slack event/interactivity to Activity conversion.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackMessageConverter.cs` — message Activity to one-or-more Slack messages.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAttachmentConverter.cs` — card and generic attachment conversion.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackFileUploader.cs` — Slack external file-upload sequence.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackMessagePayload.cs` — internal immutable outbound Slack DTOs.

### New test files

- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestValidatorTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestParserTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackEventDeduplicatorTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackActivityConverterTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackMessageConverterTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs`
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackFileUploaderTests.cs`

### Modified files

- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs` — retain HTTP/turn/send orchestration only.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterExtensions.cs` — register and compose internal components.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs` — remove `BotName`.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackApi.cs` — add raw external-upload HTTP operation.
- `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackChannelData.cs` — model `render_buttons_as_menu`.
- `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs` — preserve integration coverage and add multi-message behavior.
- `src\samples\SlackAgent\appsettings.json` — remove `BotName`.

## Task 1: Extract Slack Request Validation

**Files:**
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestValidator.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestValidatorTests.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`

- [ ] **Step 1: Write failing validator tests**

Create tests for a valid signature, a tampered signature, a stale timestamp, missing headers, and disabled verification:

```csharp
public class SlackRequestValidatorTests
{
    private const string Secret = "test-signing-secret";

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        const string body = """{"type":"event_callback"}""";
        var request = CreateRequest(body, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var validator = new SlackRequestValidator(new SlackAdapterOptions
        {
            SigningSecret = Secret,
            RequestMaxAgeSeconds = 300,
        });

        Assert.True(validator.Verify(request, body));
    }

    [Fact]
    public void Verify_StaleTimestamp_ReturnsFalse()
    {
        const string body = """{"type":"event_callback"}""";
        var request = CreateRequest(body, DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds());

        var validator = new SlackRequestValidator(new SlackAdapterOptions
        {
            SigningSecret = Secret,
            RequestMaxAgeSeconds = 300,
        });

        Assert.False(validator.Verify(request, body));
    }

    [Fact]
    public void Verify_EmptySigningSecret_ReturnsTrue()
    {
        var validator = new SlackRequestValidator(new SlackAdapterOptions());
        Assert.True(validator.Verify(new DefaultHttpContext().Request, "{}"));
    }

    private static HttpRequest CreateRequest(string body, long timestamp)
    {
        var request = new DefaultHttpContext().Request;
        var timestampText = timestamp.ToString();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestampText}:{body}"));
        request.Headers["X-Slack-Request-Timestamp"] = timestampText;
        request.Headers["X-Slack-Signature"] = $"v0={Convert.ToHexString(hash).ToLowerInvariant()}";
        return request;
    }
}
```

Add equivalent tests that replace the signature with `v0=deadbeef` and omit both headers.

- [ ] **Step 2: Run the validator tests and confirm the missing type failure**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackRequestValidatorTests" --no-restore
```

Expected: compilation fails because `SlackRequestValidator` does not exist.

- [ ] **Step 3: Implement `SlackRequestValidator`**

Move the complete signature logic from `SlackAdapter.VerifySignature` into:

```csharp
internal sealed class SlackRequestValidator
{
    private readonly SlackAdapterOptions _options;

    internal SlackRequestValidator(SlackAdapterOptions options)
        => _options = options ?? throw new ArgumentNullException(nameof(options));

    internal bool Verify(HttpRequest request, string body)
    {
        if (string.IsNullOrEmpty(_options.SigningSecret))
        {
            return true;
        }

        var signature = request.Headers["X-Slack-Signature"].ToString();
        var timestamp = request.Headers["X-Slack-Request-Timestamp"].ToString();
        if (string.IsNullOrEmpty(signature)
            || string.IsNullOrEmpty(timestamp)
            || !long.TryParse(timestamp, out var requestUnixTime))
        {
            return false;
        }

        var age = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - requestUnixTime);
        if (age > _options.RequestMaxAgeSeconds)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:{timestamp}:{body}"));
        var computed = $"v0={Convert.ToHexString(hash).ToLowerInvariant()}";

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature));
    }
}
```

Add a `_requestValidator` field to `SlackAdapter`, construct it from `_options`, replace `VerifySignature(...)` with `_requestValidator.Verify(...)`, and delete the old adapter method and unused cryptography imports.

- [ ] **Step 4: Run validator and adapter signature tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackRequestValidatorTests|FullyQualifiedName~SlackAdapterTests.ProcessAsync_InvalidSignature|FullyQualifiedName~SlackAdapterTests.ProcessAsync_NoSigningSecret" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit request validation extraction**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestValidator.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestValidatorTests.cs
git commit -m "Extract Slack request validation" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 2: Extract Request Parsing and Event Deduplication

**Files:**
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestParser.cs`
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackEventDeduplicator.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestParserTests.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackEventDeduplicatorTests.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`

- [ ] **Step 1: Write failing parser and deduplicator tests**

Define parser tests for URL verification, event callbacks, form-encoded interactivity, ignored envelopes, and malformed JSON:

```csharp
[Fact]
public void Parse_FormEncodedPayload_ReturnsInteractiveRequest()
{
    var payload = """{"type":"block_actions","team":{"id":"T1"}}""";
    var parsed = new SlackRequestParser().Parse(
        "payload=" + WebUtility.UrlEncode(payload),
        "application/x-www-form-urlencoded");

    Assert.Equal(SlackRequestKind.Interactive, parsed.Kind);
    Assert.Equal("block_actions", parsed.ActionPayload!.type);
    Assert.Equal(payload, parsed.PayloadJson);
}

[Fact]
public void Parse_UrlVerification_ReturnsChallenge()
{
    var parsed = new SlackRequestParser().Parse(
        """{"type":"url_verification","challenge":"abc123"}""",
        "application/json");

    Assert.Equal(SlackRequestKind.UrlVerification, parsed.Kind);
    Assert.Equal("abc123", parsed.Challenge);
}
```

Define deduplicator tests:

```csharp
[Fact]
public void TryAccept_DuplicateEvent_ReturnsFalseUntilRemoved()
{
    var deduplicator = new SlackEventDeduplicator();

    Assert.True(deduplicator.TryAccept("Ev1"));
    Assert.False(deduplicator.TryAccept("Ev1"));

    deduplicator.Remove("Ev1");
    Assert.True(deduplicator.TryAccept("Ev1"));
}
```

Also assert that null/empty event IDs are always accepted.

- [ ] **Step 2: Run the tests and confirm missing type failures**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackRequestParserTests|FullyQualifiedName~SlackEventDeduplicatorTests" --no-restore
```

Expected: compilation fails because the parser, parsed request, request kind, and deduplicator do not exist.

- [ ] **Step 3: Implement the request parser**

Create these complete internal types:

```csharp
internal enum SlackRequestKind
{
    Ignore,
    UrlVerification,
    Event,
    Interactive,
}

internal sealed record ParsedSlackRequest(
    SlackRequestKind Kind,
    string PayloadJson,
    string? Challenge = null,
    EventEnvelope? EventEnvelope = null,
    ActionPayload? ActionPayload = null);

internal sealed class SlackRequestParser
{
    internal ParsedSlackRequest Parse(string body, string? contentType)
    {
        if (contentType?.Contains(
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            var payloadJson = ExtractFormValue(body, "payload");
            if (string.IsNullOrEmpty(payloadJson))
            {
                return new ParsedSlackRequest(SlackRequestKind.Ignore, string.Empty);
            }

            return new ParsedSlackRequest(
                SlackRequestKind.Interactive,
                payloadJson,
                ActionPayload: ProtocolJsonSerializer.ToObject<ActionPayload>(payloadJson));
        }

        using var document = JsonDocument.Parse(body);
        var type = document.RootElement.TryGetProperty("type", out var typeElement)
            ? typeElement.GetString()
            : null;

        if (string.Equals(type, "url_verification", StringComparison.Ordinal))
        {
            var challenge = document.RootElement.TryGetProperty("challenge", out var element)
                ? element.GetString()
                : string.Empty;
            return new ParsedSlackRequest(
                SlackRequestKind.UrlVerification,
                body,
                Challenge: challenge);
        }

        if (!string.Equals(type, "event_callback", StringComparison.Ordinal))
        {
            return new ParsedSlackRequest(SlackRequestKind.Ignore, body);
        }

        return new ParsedSlackRequest(
            SlackRequestKind.Event,
            body,
            EventEnvelope: ProtocolJsonSerializer.ToObject<EventEnvelope>(body));
    }

    private static string? ExtractFormValue(string body, string key)
    {
        foreach (var pair in (body ?? string.Empty).Split('&'))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0
                && string.Equals(pair[..separator], key, StringComparison.Ordinal))
            {
                return WebUtility.UrlDecode(pair[(separator + 1)..]);
            }
        }

        return null;
    }
}
```

- [ ] **Step 4: Implement the event deduplicator**

Move `ShouldProcess`, `PruneDedupe`, retention, cap, and the dictionary into:

```csharp
internal sealed class SlackEventDeduplicator
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(10);
    private const int MaxEntries = 5000;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _events = new();

    internal bool TryAccept(string? eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return true;
        }

        Prune();
        return _events.TryAdd(eventId, DateTimeOffset.UtcNow);
    }

    internal void Remove(string? eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            _events.TryRemove(eventId, out _);
        }
    }

    private void Prune()
    {
        var cutoff = DateTimeOffset.UtcNow - Retention;
        foreach (var entry in _events)
        {
            if (entry.Value < cutoff)
            {
                _events.TryRemove(entry.Key, out _);
            }
        }

        if (_events.Count <= MaxEntries)
        {
            return;
        }

        foreach (var entry in _events)
        {
            _events.TryRemove(entry.Key, out _);
            if (_events.Count <= MaxEntries)
            {
                break;
            }
        }
    }
}
```

- [ ] **Step 5: Rewire `SlackAdapter.ProcessAsync`**

Add parser and deduplicator fields, parse once after signature validation, log `parsed.PayloadJson`, and switch on `parsed.Kind`:

```csharp
var parsed = _requestParser.Parse(body, httpRequest.ContentType);

if (parsed.Kind == SlackRequestKind.UrlVerification)
{
    httpResponse.StatusCode = StatusCodes.Status200OK;
    httpResponse.ContentType = "text/plain";
    await httpResponse.WriteAsync(parsed.Challenge ?? string.Empty, cancellationToken);
    return;
}

if (parsed.Kind == SlackRequestKind.Ignore)
{
    httpResponse.StatusCode = StatusCodes.Status200OK;
    return;
}

var eventId = parsed.EventEnvelope?.event_id;
if (parsed.Kind == SlackRequestKind.Event && !_eventDeduplicator.TryAccept(eventId))
{
    httpResponse.StatusCode = StatusCodes.Status200OK;
    return;
}
```

On queue rejection call `_eventDeduplicator.Remove(eventId)`. Delete `ShouldProcess`, `PruneDedupe`, `IsFormUrlEncoded`, `ExtractFormValue`, the adapter dictionary, and their imports.

- [ ] **Step 6: Run parser, dedupe, and adapter request tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackRequestParserTests|FullyQualifiedName~SlackEventDeduplicatorTests|FullyQualifiedName~SlackAdapterTests.ProcessAsync_UrlVerification|FullyQualifiedName~SlackAdapterTests.ProcessAsync_DuplicateEventId|FullyQualifiedName~SlackAdapterTests.ProcessAsync_QueueRejected" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit parser and deduplication extraction**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackRequestParser.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackEventDeduplicator.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackRequestParserTests.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackEventDeduplicatorTests.cs
git commit -m "Extract Slack request parsing" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 3: Extract Activity Conversion and Remove BotName

**Files:**
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackActivityConverter.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackActivityConverterTests.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`
- Modify: `src\samples\SlackAgent\appsettings.json`

- [ ] **Step 1: Write failing agent-name conversion tests**

Use direct converter tests so name resolution is independent of HTTP:

```csharp
[Agent("Configured Slack Agent")]
private sealed class NamedAgent : IAgent
{
    public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

private sealed class FallbackAgent : IAgent
{
    public Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

[Fact]
public void ConvertInteractive_Select_UsesAgentAttributeName()
{
    var converter = CreateConverter();
    var parsed = ParseInteractive("""
        {"type":"block_actions","user":{"id":"U1"},"team":{"id":"T1"},
         "channel":{"id":"C1"},"actions":[{"type":"select",
         "selected_options":[{"value":"choice"}]}]}
        """);

    var activity = converter.Convert(parsed, typeof(NamedAgent));

    Assert.Equal("Configured Slack Agent", activity!.Recipient.Name);
    Assert.Equal("@Configured Slack Agent", Assert.IsType<Mention>(Assert.Single(activity.Entities)).Text);
}

[Fact]
public void ConvertInteractive_Button_UsesShortClassNameFallback()
{
    var converter = CreateConverter();
    var parsed = ParseInteractive("""
        {"type":"block_actions","user":{"id":"U1"},"team":{"id":"T1"},
         "channel":{"id":"C1"},"actions":[{"type":"button","value":"go"}]}
        """);

    var activity = converter.Convert(parsed, typeof(FallbackAgent));

    Assert.Equal(nameof(FallbackAgent), activity!.Recipient.Name);
    Assert.Equal($"@{nameof(FallbackAgent)}", Assert.IsType<Mention>(Assert.Single(activity.Entities)).Text);
}
```

Also port representative tests for bot-message suppression, team fallback, feedback invoke, `message_action`, and unknown payload preservation from `SlackAdapterTests`.

- [ ] **Step 2: Run converter tests and confirm the missing type failure**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackActivityConverterTests" --no-restore
```

Expected: compilation fails because `SlackActivityConverter` does not exist.

- [ ] **Step 3: Implement `SlackActivityConverter`**

Move `CreateActivityFromEvent`, `CreateActivityFromInteractivePayload`, team resolution, and all interactivity conditionals into an internal converter:

```csharp
internal sealed class SlackActivityConverter
{
    private readonly SlackAdapterOptions _options;

    internal SlackActivityConverter(SlackAdapterOptions options)
        => _options = options ?? throw new ArgumentNullException(nameof(options));

    internal SlackActivity? Convert(ParsedSlackRequest request, Type agentType)
    {
        var agentName = agentType.GetCustomAttribute<AgentAttribute>()?.Name
            ?? agentType.Name;

        return request.Kind switch
        {
            SlackRequestKind.Event => ConvertEvent(request.EventEnvelope!, agentName),
            SlackRequestKind.Interactive => ConvertInteractive(request.ActionPayload!, agentName),
            _ => null,
        };
    }
}
```

In both event and interactive conversion, construct the recipient as:

```csharp
Recipient = new ChannelAccount(
    id: SlackHelpers.CreateAccountId(_options.BotId, teamId),
    name: agentName),
```

For select/button mentions use:

```csharp
activity.Entities =
[
    new Mention
    {
        Mentioned = activity.Recipient,
        Text = $"@{activity.Recipient.Name}",
    },
];
```

Keep every existing Activity type/name/value, channel data, identity, conversation ID, bot suppression, and Slack decoding rule unchanged.

- [ ] **Step 4: Remove `BotName` and rewire the adapter**

Delete `SlackAdapterOptions.BotName`, remove its two option tests, remove the `botName` argument from `CreateAdapter`, remove `BotName` from sample configuration, and call:

```csharp
activity = _activityConverter.Convert(parsed, agent.GetType());
```

Delete the old conversion methods from `SlackAdapter`.

- [ ] **Step 5: Run converter and Slack route integration tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackActivityConverterTests|FullyQualifiedName~SlackAdapterTests.ProcessAsync_MessageEvent|FullyQualifiedName~SlackAdapterTests.ProcessAsync_FeedbackButtons|FullyQualifiedName~SlackAdapterTests.ProcessAsync_BlockActionSelect|FullyQualifiedName~SlackAdapterTests.ProcessAsync_InteractiveMessageButton" --no-restore
```

Expected: all selected tests pass with mentions based on `AgentAttribute` or the runtime class name.

- [ ] **Step 6: Commit Activity conversion extraction**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackActivityConverter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackActivityConverterTests.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs src\samples\SlackAgent\appsettings.json
git commit -m "Extract Slack Activity conversion" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 4: Add Outbound Payload Models and Suggested Actions

**Files:**
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackMessagePayload.cs`
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackMessageConverter.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackMessageConverterTests.cs`

- [ ] **Step 1: Write failing text and SuggestedActions tests**

```csharp
[Fact]
public async Task ConvertAsync_TextAndSuggestedActions_ReturnsTwoThreadedMessages()
{
    var converter = new SlackMessageConverter();
    var activity = MessageFactory.Text("Choose");
    activity.SuggestedActions = new SuggestedActions(actions:
    [
        new CardAction(title: "One", value: "value-one"),
        new CardAction(title: "Two"),
    ]);

    var messages = await converter.ConvertAsync(
        activity,
        "C1",
        "1700000000.000100",
        "xoxb-token",
        CancellationToken.None);

    Assert.Collection(
        messages,
        message =>
        {
            Assert.Equal("Choose", message.Text);
            Assert.Equal("1700000000.000100", message.ThreadTs);
        },
        message =>
        {
            Assert.Equal("* value-one\n\n* Two", message.Text);
            Assert.Equal("1700000000.000100", message.ThreadTs);
        });
}
```

Add tests for non-message Activities returning no payloads and empty message Activities returning no payloads. Attachment-only behavior is added in Task 5 when attachment conversion is available.

- [ ] **Step 2: Run tests and confirm missing type failures**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackMessageConverterTests" --no-restore
```

Expected: compilation fails because `SlackMessageConverter` and Slack outbound DTOs do not exist.

- [ ] **Step 3: Add immutable outbound Slack DTOs**

Create `SlackMessagePayload.cs` with JSON names Slack expects:

```csharp
internal sealed record SlackMessagePayload
{
    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("thread_ts")]
    public string? ThreadTs { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<SlackPostAttachment>? Attachments { get; init; }
}

internal sealed record SlackPostAttachment
{
    [JsonPropertyName("pretext")] public string? Pretext { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("title_link")] public string? TitleLink { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("image_url")] public string? ImageUrl { get; init; }
    [JsonPropertyName("thumb_url")] public string? ThumbUrl { get; init; }
    [JsonPropertyName("fallback")] public string? Fallback { get; init; }
    [JsonPropertyName("callback_id")] public string? CallbackId { get; init; }
    [JsonPropertyName("attachment_type")] public string? AttachmentType { get; init; }
    [JsonPropertyName("actions")] public IReadOnlyList<SlackPostAction>? Actions { get; init; }
    [JsonPropertyName("fields")] public IReadOnlyList<SlackPostField>? Fields { get; init; }
    [JsonPropertyName("mrkdwn_in")] public IReadOnlyList<string>? MarkdownIn { get; init; }
}

internal sealed record SlackPostAction(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("style")] string? Style = null,
    [property: JsonPropertyName("options")] IReadOnlyList<SlackPostOption>? Options = null,
    [property: JsonPropertyName("selected_options")] IReadOnlyList<SlackPostOption>? SelectedOptions = null);

internal sealed record SlackPostOption(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("value")] string? Value);

internal sealed record SlackPostField(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("value")] string? Value);
```

- [ ] **Step 4: Implement text and SuggestedActions conversion**

Create:

```csharp
internal sealed class SlackMessageConverter
{
    internal Task<IReadOnlyList<SlackMessagePayload>> ConvertAsync(
        IActivity activity,
        string channel,
        string? threadTs,
        string token,
        CancellationToken cancellationToken)
    {
        if (!activity.IsType(ActivityTypes.Message))
        {
            return Task.FromResult<IReadOnlyList<SlackMessagePayload>>([]);
        }

        var messages = new List<SlackMessagePayload>();
        if (!string.IsNullOrEmpty(activity.Text))
        {
            messages.Add(new SlackMessagePayload
            {
                Channel = channel,
                Text = activity.Text?.SlackEncode(),
                ThreadTs = threadTs,
            });
        }

        if (activity.SuggestedActions?.Actions?.Count > 0)
        {
            var lines = activity.SuggestedActions.Actions.Select(action =>
            {
                var value = action.Type == ActionTypes.MessageBack
                    ? action.Text
                    : action.Value as string;
                return $"* {value ?? action.Title}";
            });

            messages.Add(new SlackMessagePayload
            {
                Channel = channel,
                Text = string.Join("\n\n", lines),
                ThreadTs = threadTs,
            });
        }

        return Task.FromResult<IReadOnlyList<SlackMessagePayload>>(messages);
    }
}
```

- [ ] **Step 5: Run converter tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackMessageConverterTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit outbound models and SuggestedActions**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackMessagePayload.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackMessageConverter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackMessageConverterTests.cs
git commit -m "Add Slack outbound message conversion" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 5: Convert SDK Cards and Card Actions

**Files:**
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAttachmentConverter.cs`
- Create: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackFileUploader.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackMessageConverter.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackChannelData.cs`

- [ ] **Step 1: Write failing card conversion tests**

Create focused tests with typed card content:

```csharp
[Fact]
public async Task ConvertAsync_HeroCard_MapsImageTextAndButtons()
{
    var card = new HeroCard(
        title: "Title",
        subtitle: "Subtitle",
        text: "Body",
        images: [new CardImage("https://example.test/hero.png")],
        buttons:
        [
            new CardAction(ActionTypes.ImBack, "Reply", value: "reply-value"),
            new CardAction(ActionTypes.OpenUrl, "Open", value: "https://example.test"),
        ]);

    var result = await CreateConverter().ConvertAsync(
        [card.ToAttachment()],
        "B1:T1",
        "C1",
        "xoxb-token",
        renderButtonsAsMenu: false,
        CancellationToken.None);

    Assert.Contains(result, item =>
        item.Pretext == "Title"
        && item.Title == "Subtitle"
        && item.Text == "Body"
        && item.ImageUrl == "https://example.test/hero.png");
    Assert.Contains(result.SelectMany(item => item.Actions ?? []), action =>
        action.Type == "button" && action.Value == "reply-value");
    Assert.Contains(result.SelectMany(item => item.Fields ?? []), field =>
        field.Value == "<https://example.test|Open>");
}
```

Add separate tests for:

- Thumbnail card uses `thumb_url`.
- Audio/Video cards link media; Animation GIF uses `image_url`.
- Receipt card maps items, facts, tax, VAT, total, and buttons.
- Sign-in and OAuth cards map text, image, and first button.
- Six interactive buttons split into attachments of five and one.
- `render_buttons_as_menu` creates one `select` action with options.
- Adaptive Card is omitted and logs a warning.
- Content supplied as `JsonElement` deserializes through `ProtocolJsonSerializer.ToObject<T>`.

- [ ] **Step 2: Run attachment tests and confirm missing type failure**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAttachmentConverterTests" --no-restore
```

Expected: compilation fails because `SlackAttachmentConverter` does not exist.

- [ ] **Step 3: Add `render_buttons_as_menu` channel data**

Add:

```csharp
[JsonPropertyName("render_buttons_as_menu")]
public bool? RenderButtonsAsMenu { get; set; }
```

to `SlackChannelData`.

- [ ] **Step 4: Define the internal uploader contract**

Create `SlackFileUploader.cs` with the contract card conversion will depend on:

```csharp
internal interface ISlackFileUploader
{
    Task<string?> UploadAsync(
        byte[] content,
        string fileName,
        string channel,
        string token,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement typed card conversion**

Create `SlackAttachmentConverter` with:

```csharp
internal sealed class SlackAttachmentConverter
{
    private readonly ISlackFileUploader _fileUploader;
    private readonly ILogger<SlackAttachmentConverter> _logger;

    internal SlackAttachmentConverter(
        ISlackFileUploader fileUploader,
        ILogger<SlackAttachmentConverter>? logger = null)
    {
        _fileUploader = fileUploader ?? throw new ArgumentNullException(nameof(fileUploader));
        _logger = logger ?? NullLogger<SlackAttachmentConverter>.Instance;
    }

    internal async Task<IReadOnlyList<SlackPostAttachment>> ConvertAsync(
        IList<Attachment>? attachments,
        string callbackId,
        string channel,
        string token,
        bool renderButtonsAsMenu,
        CancellationToken cancellationToken)
    {
        var result = new List<SlackPostAttachment>();
        if (attachments == null)
        {
            return result;
        }

        for (var index = 0; index < attachments.Count; index++)
        {
            var attachment = attachments[index];
            if (attachment == null)
            {
                _logger.LogWarning("Slack attachment {AttachmentIndex} is null.", index);
                continue;
            }

            try
            {
                result.AddRange(await ConvertOneAsync(
                    attachment,
                    callbackId,
                    channel,
                    token,
                    renderButtonsAsMenu,
                    index,
                    cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Unable to convert Slack attachment {AttachmentIndex} with content type {ContentType}.",
                    index,
                    attachment.ContentType);
            }
        }

        return result;
    }

    private static T? ContentAs<T>(Attachment attachment)
        => ProtocolJsonSerializer.ToObject<T>(attachment.Content);
}
```

Implement one branch for each approved content type. Port Intercom's field mapping exactly, but use immutable `with` expressions or local builders before constructing the immutable DTOs. For Adaptive Cards:

```csharp
if (string.Equals(attachment.ContentType, ContentTypes.AdaptiveCard, StringComparison.Ordinal))
{
    _logger.LogWarning(
        "Adaptive Card attachment {AttachmentIndex} is not supported by the direct Slack adapter.",
        attachmentIndex);
    return [];
}
```

Implement action rendering with these rules:

```csharp
private static SlackPostAction? ToInteractiveAction(CardAction action)
{
    if (action.Type != ActionTypes.ImBack
        && action.Type != ActionTypes.PostBack
        && action.Type != ActionTypes.MessageBack)
    {
        return null;
    }

    var value = action.Type == ActionTypes.MessageBack
        ? action.Text
        : action.Value as string;

    return new SlackPostAction(
        Name: action.Type,
        Text: action.Title,
        Type: "button",
        Value: value,
        Style: "default");
}
```

Split interactive actions with `Chunk(5)`. For menu rendering, create one select action whose options are the rendered actions and whose selected option is the first option. Render non-interactive actions as `SlackPostField(null, $"<{action.Value}|{action.Title}>")`.

Set `CallbackId = callbackId` on every produced attachment.

- [ ] **Step 6: Wire card conversion into `SlackMessageConverter`**

Inject `SlackAttachmentConverter`, read:

```csharp
var slackChannelData = activity.GetChannelData<SlackChannelData>();
var convertedAttachments = await _attachmentConverter.ConvertAsync(
    activity.Attachments,
    activity.From?.Id ?? string.Empty,
    channel,
    token,
    slackChannelData?.RenderButtonsAsMenu == true,
    cancellationToken);
```

Place `convertedAttachments` on the first Slack payload. Keep attachment-only Activities by creating the first payload whenever `convertedAttachments.Count > 0`.

- [ ] **Step 7: Run card and message converter tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAttachmentConverterTests|FullyQualifiedName~SlackMessageConverterTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 8: Commit SDK card conversion**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAttachmentConverter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackFileUploader.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackMessageConverter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackChannelData.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs
git commit -m "Render SDK cards for Slack" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 6: Add Modern Slack File Uploads and Generic Attachments

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackFileUploader.cs`
- Create: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackFileUploaderTests.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackApi.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAttachmentConverter.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs`

- [ ] **Step 1: Write failing external-upload sequence tests**

Use a recording `IHttpClientFactory` and return three responses:

```csharp
[Fact]
public async Task UploadAsync_UsesExternalUploadSequence()
{
    var requests = new List<(Uri Uri, string? Body, string? Authorization)>();
    var factory = CreateFactory(async (request, cancellationToken) =>
    {
        requests.Add((
            request.RequestUri!,
            request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            request.Headers.Authorization?.ToString()));

        return requests.Count switch
        {
            1 => JsonResponse("""{"ok":true,"upload_url":"https://uploads.slack.test/file","file_id":"F1"}"""),
            2 => new HttpResponseMessage(HttpStatusCode.OK),
            3 => JsonResponse("""{"ok":true,"files":[{"id":"F1","url_private":"https://files.slack.test/F1"}]}"""),
            _ => throw new InvalidOperationException(),
        };
    });

    var uploader = new SlackFileUploader(new SlackApi(factory.Object));
    var url = await uploader.UploadAsync(
        Encoding.UTF8.GetBytes("content"),
        "test.txt",
        "C1",
        "xoxb-token",
        CancellationToken.None);

    Assert.Equal("https://files.slack.test/F1", url);
    Assert.EndsWith("/files.getUploadURLExternal", requests[0].Uri.AbsoluteUri);
    Assert.Equal(new Uri("https://uploads.slack.test/file"), requests[1].Uri);
    Assert.EndsWith("/files.completeUploadExternal", requests[2].Uri.AbsoluteUri);
    Assert.Equal("Bearer xoxb-token", requests[0].Authorization);
    Assert.Null(requests[1].Authorization);
}
```

Add tests that cancellation propagates and a failed raw upload throws `SlackResponseException`.

- [ ] **Step 2: Write failing generic attachment tests**

Add tests for:

```csharp
[Fact]
public async Task ConvertAsync_InlineBytes_UploadsAndRendersFile()
{
    var uploader = new Mock<ISlackFileUploader>();
    uploader.Setup(item => item.UploadAsync(
            It.IsAny<byte[]>(),
            "report.txt",
            "C1",
            "xoxb-token",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("https://files.slack.test/F1");

    var converter = CreateConverter(uploader.Object);
    var attachment = new Attachment("text/plain", content: Encoding.UTF8.GetBytes("report"), name: "report.txt");

    var result = await converter.ConvertAsync(
        [attachment],
        "B1:T1",
        "C1",
        "xoxb-token",
        false,
        CancellationToken.None);

    var rendered = Assert.Single(result);
    Assert.Equal("report.txt", rendered.Title);
    Assert.Equal("https://files.slack.test/F1", rendered.ImageUrl);
    Assert.Equal("https://files.slack.test/F1", rendered.TitleLink);
}
```

Also cover a `data:` ContentUrl, ordinary HTTP ContentUrl/ThumbnailUrl without upload, generated attachment names, per-attachment upload failure with later attachments still returned, and cancellation propagation.

- [ ] **Step 3: Add a raw upload operation to `SlackApi`**

Add:

```csharp
internal async Task UploadContentAsync(
    string uploadUrl,
    byte[] content,
    CancellationToken cancellationToken)
{
    SlackLogSanitizer.ExecuteSafely(() =>
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            SlackApiLog.LogRequest(
                _logger,
                "files.externalUpload",
                $$"""{"length":{{content.Length}}}""");
        }
    });

    using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
    {
        Content = new ByteArrayContent(content),
    };
    using var client = _httpClientFactory.CreateClient(nameof(SlackApi));
    using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    SlackLogSanitizer.ExecuteSafely(() =>
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            SlackApiLog.LogResponse(
                _logger,
                "files.externalUpload",
                (int)response.StatusCode,
                "{}");
        }
    });

    if (!response.IsSuccessStatusCode)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new SlackResponseException(
            $"Slack external upload failed (HTTP {(int)response.StatusCode}):\n{body}");
    }
}
```

Do not attach the bot token to the pre-signed upload URL. Log only the byte length and status code; do not log the binary body or full signed URL.

- [ ] **Step 4: Implement `SlackFileUploader`**

```csharp
internal sealed class SlackFileUploader : ISlackFileUploader
{
    private readonly SlackApi _slackApi;

    internal SlackFileUploader(SlackApi slackApi)
        => _slackApi = slackApi ?? throw new ArgumentNullException(nameof(slackApi));

    public async Task<string?> UploadAsync(
        byte[] content,
        string fileName,
        string channel,
        string token,
        CancellationToken cancellationToken)
    {
        var start = await _slackApi.CallAsync(
            "files.getUploadURLExternal",
            new { filename = fileName, length = content.Length },
            token,
            cancellationToken);

        var uploadUrl = start.Get<string>("upload_url")
            ?? throw new SlackResponseException("Slack did not return upload_url.");
        var fileId = start.Get<string>("file_id")
            ?? throw new SlackResponseException("Slack did not return file_id.");

        await _slackApi.UploadContentAsync(uploadUrl, content, cancellationToken);

        var complete = await _slackApi.CallAsync(
            "files.completeUploadExternal",
            new
            {
                files = new[] { new { id = fileId, title = fileName } },
                channel_id = channel,
            },
            token,
            cancellationToken);

        return complete.Get<string>("files[0].url_private")
            ?? complete.Get<string>("files[0].permalink");
    }
}
```

- [ ] **Step 5: Implement generic attachment conversion**

Give `SlackAttachmentConverter` the channel, token, and uploader needed by generic attachments. Decode inline forms:

```csharp
private static bool TryDecodeDataUrl(string? value, out byte[] bytes)
{
    bytes = [];
    if (string.IsNullOrEmpty(value) || !value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var comma = value.IndexOf(',');
    if (comma < 0 || !value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    bytes = Convert.FromBase64String(value[(comma + 1)..]);
    return true;
}
```

Use `Attachment.Content` when it is `byte[]`; otherwise use a base64 data URL from `ContentUrl`. Upload inline data, but reference ordinary HTTP `ContentUrl` and `ThumbnailUrl` directly. Construct:

```csharp
new SlackPostAttachment
{
    Title = fileName,
    ImageUrl = contentUrl,
    TitleLink = contentUrl ?? thumbnailUrl,
    Fallback = contentUrl ?? thumbnailUrl,
    ThumbUrl = thumbnailUrl,
    CallbackId = callbackId,
};
```

Infer a generated filename from `ContentType` with a small explicit MIME map (`text/plain` → `.txt`, `application/pdf` → `.pdf`, `image/png` → `.png`, `image/jpeg` → `.jpg`, `image/gif` → `.gif`) and otherwise leave the generated name extensionless.

- [ ] **Step 6: Run upload and generic attachment tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackFileUploaderTests|FullyQualifiedName~SlackAttachmentConverterTests" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit file upload and generic attachments**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackFileUploader.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAttachmentConverter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackApi.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackFileUploaderTests.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs
git commit -m "Upload Slack Activity attachments" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 7: Wire Multi-Message Outbound Sending

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`

- [ ] **Step 1: Write failing adapter integration tests**

Add a test proving one Activity produces two ordered Slack calls and returns the last timestamp:

```csharp
[Fact]
public async Task SendActivitiesAsync_SuggestedActions_SendsSecondMessageAndReturnsLastTimestamp()
{
    var requests = new List<string>();
    var adapter = CreateAdapter(out _, async (request, cancellationToken) =>
    {
        requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
        var timestamp = requests.Count == 1 ? "1.001" : "1.002";
        return JsonResponse($$"""{"ok":true,"ts":"{{timestamp}}"}""");
    });
    var turnContext = CreateSlackTurnContext("B123:T1:C100:0.999");
    var activity = MessageFactory.Text("Choose");
    activity.SuggestedActions = new SuggestedActions(actions:
    [
        new CardAction(title: "One", value: "one"),
    ]);

    var responses = await adapter.SendActivitiesAsync(
        turnContext,
        [activity],
        CancellationToken.None);

    Assert.Equal(2, requests.Count);
    Assert.Contains("\"text\":\"Choose\"", requests[0]);
    Assert.Contains("* one", requests[1]);
    Assert.All(requests, body => Assert.Contains("\"thread_ts\":\"0.999\"", body));
    Assert.Equal("1.002", Assert.Single(responses).Id);
}
```

Add an attachment-only integration test and retain the existing failed-Slack-call/no-success-log test.

- [ ] **Step 2: Run the new integration tests and verify failure**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.SendActivitiesAsync_SuggestedActions|FullyQualifiedName~SlackAdapterTests.SendActivitiesAsync_AttachmentOnly" --no-restore
```

Expected: tests fail because `SlackAdapter` still sends only one non-empty text message.

- [ ] **Step 3: Replace inline outbound mapping with `SlackMessageConverter`**

Inject/store `_messageConverter` and replace the current text-only block with:

```csharp
var messages = await _messageConverter.ConvertAsync(
    activity,
    channel,
    threadTs,
    channelData?.ApiToken ?? _options.BotToken,
    cancellationToken);

string lastTimestamp = activity.Id ?? string.Empty;
foreach (var message in messages)
{
    var response = await _slackApi.CallAsync(
        "chat.postMessage",
        message,
        channelData?.ApiToken ?? _options.BotToken,
        cancellationToken);

    lastTimestamp = response.ts ?? string.Empty;
    SlackLogSanitizer.ExecuteSafely(() =>
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            SlackAdapterLog.LogMessageSent(
                Logger,
                conversationId ?? string.Empty,
                lastTimestamp,
                SlackLogSanitizer.SanitizeObject(activity));
        }
    });
}

responses[index] = new ResourceResponse(lastTimestamp);
```

Keep non-message Activities as no-ops. Keep missing-channel logging. Do not catch Slack API failures.

- [ ] **Step 4: Run outbound adapter tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.SendActivitiesAsync|FullyQualifiedName~SlackAdapterTests.ProcessAsync_SendActivity_PostsToSlack" --no-restore
```

Expected: all selected tests pass.

- [ ] **Step 5: Commit multi-message adapter wiring**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Send converted Slack messages" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 8: Compose Internal Components Through AddSlack

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterExtensions.cs`
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`

- [ ] **Step 1: Write failing DI composition tests**

Extend `DependencyInjection_UsesSlackApiLogger` to resolve every internal service through reflection or an `InternalsVisibleTo` test reference:

```csharp
Assert.NotNull(host.Services.GetRequiredService<SlackRequestValidator>());
Assert.NotNull(host.Services.GetRequiredService<SlackRequestParser>());
Assert.NotNull(host.Services.GetRequiredService<SlackEventDeduplicator>());
Assert.NotNull(host.Services.GetRequiredService<SlackActivityConverter>());
Assert.NotNull(host.Services.GetRequiredService<SlackMessageConverter>());
Assert.NotNull(host.Services.GetRequiredService<SlackAttachmentConverter>());
Assert.NotNull(host.Services.GetRequiredService<ISlackFileUploader>());
```

Retain the constructor reflection assertions for the original three-parameter and logger-aware four-parameter public constructors.

- [ ] **Step 2: Run DI tests and verify missing registrations**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.DependencyInjection|FullyQualifiedName~SlackAdapterTests.Constructors" --no-restore
```

Expected: the component resolution assertions fail.

- [ ] **Step 3: Register components and use an adapter factory**

In `AddSlack(SlackAdapterOptions options)`, register:

```csharp
services.AddSingleton(options);
services.AddSingleton<SlackRequestValidator>();
services.AddSingleton<SlackRequestParser>();
services.AddSingleton<SlackEventDeduplicator>();
services.AddSingleton<SlackActivityConverter>();
services.AddSingleton<SlackApi>();
services.AddSingleton<ISlackFileUploader, SlackFileUploader>();
services.AddSingleton<SlackAttachmentConverter>();
services.AddSingleton<SlackMessageConverter>();
services.AddSingleton<SlackAdapter>(provider => new SlackAdapter(
    options,
    provider.GetRequiredService<SlackApi>(),
    provider.GetRequiredService<ILogger<SlackAdapter>>(),
    provider.GetRequiredService<IActivityTaskQueue>(),
    provider.GetRequiredService<SlackRequestValidator>(),
    provider.GetRequiredService<SlackRequestParser>(),
    provider.GetRequiredService<SlackEventDeduplicator>(),
    provider.GetRequiredService<SlackActivityConverter>(),
    provider.GetRequiredService<SlackMessageConverter>()));
services.AddSingleton<IChannelAdapter>(provider => provider.GetRequiredService<SlackAdapter>());
```

Add one internal composition constructor with those exact dependencies. Keep the current public constructors and have them create the same default component graph from `IHttpClientFactory` and optional loggers. Avoid two independently-created `SlackApi` instances inside one adapter.

- [ ] **Step 4: Confirm `SlackAdapter` is orchestration-only**

Remove any remaining conversion, validation, form parsing, data URL parsing, action rendering, or deduplication helpers from `SlackAdapter`. The file should contain constructor composition, `ProcessAsync`, `SendActivitiesAsync`, `UpdateActivityAsync`, `DeleteActivityAsync`, and channel/thread resolution only.

- [ ] **Step 5: Run DI and full adapter tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests" --no-restore
```

Expected: all `SlackAdapterTests` pass.

- [ ] **Step 6: Commit DI composition and final refactor**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterExtensions.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Compose Slack adapter services" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

## Task 9: Complete Regression Coverage and Documentation

**Files:**
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackActivityConverterTests.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackMessageConverterTests.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAttachmentConverterTests.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackFileUploaderTests.cs`
- Modify: `src\samples\SlackAgent\appsettings.json`

- [ ] **Step 1: Add the partial-failure regression test**

Ensure the first attachment throws during upload and the second still renders:

```csharp
[Fact]
public async Task ConvertAsync_FailedAttachment_LogsAndContinues()
{
    var uploader = new Mock<ISlackFileUploader>();
    uploader.Setup(item => item.UploadAsync(
            It.IsAny<byte[]>(),
            "bad.txt",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ThrowsAsync(new SlackResponseException("upload failed"));

    var logger = new RecordingLogger<SlackAttachmentConverter>();
    var converter = CreateConverter(uploader.Object, logger);
    var attachments = new List<Attachment>
    {
        new("text/plain", content: Encoding.UTF8.GetBytes("bad"), name: "bad.txt"),
        new("image/png", contentUrl: "https://example.test/good.png", name: "good.png"),
    };

    var result = await converter.ConvertAsync(
        attachments,
        "B1:T1",
        "C1",
        "xoxb-token",
        renderButtonsAsMenu: false,
        CancellationToken.None);

    Assert.Equal("good.png", Assert.Single(result).Title);
    Assert.Contains(logger.Entries, entry =>
        entry.Level == LogLevel.Warning
        && entry.Message.Contains("text/plain", StringComparison.Ordinal));
}
```

- [ ] **Step 2: Add the cancellation regression test**

Configure the uploader to throw `OperationCanceledException` and assert `SlackAttachmentConverter.ConvertAsync` propagates it rather than treating it as an attachment failure.

- [ ] **Step 3: Confirm sample configuration contains no BotName**

The Slack section must be:

```jsonc
"Slack": {
  "BotToken": "xoxb-your-bot-token",
  "SigningSecret": "your-signing-secret",
  "BotId": "B0000000000",
  "BotUserId": "U0000000000"
}
```

- [ ] **Step 4: Run the complete Slack test project**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --no-restore
```

Expected: all Slack tests pass with zero failures.

- [ ] **Step 5: Build the Slack extension and sample**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Microsoft.Agents.Extensions.Slack.csproj --no-restore
dotnet build src\samples\SlackAgent\SlackAgent.csproj --no-restore
```

Expected: both builds succeed with zero warnings and zero errors.

- [ ] **Step 6: Check the final diff and configuration references**

Run:

```powershell
git --no-pager diff --check
rg "BotName|files\.upload" src\libraries\Extensions\Microsoft.Agents.Extensions.Slack src\tests\Microsoft.Agents.Extensions.Slack.Tests src\samples\SlackAgent
```

Expected: `git diff --check` produces no output. `rg` produces no `BotName` references and no call to the retired `files.upload` method; references explaining that `files.upload` is retired are acceptable only in documentation.

- [ ] **Step 7: Commit regression coverage and documentation**

```powershell
git add src\tests\Microsoft.Agents.Extensions.Slack.Tests src\samples\SlackAgent\appsettings.json
git commit -m "Complete Slack outbound parity coverage" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```
