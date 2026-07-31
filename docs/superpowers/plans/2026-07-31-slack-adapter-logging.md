# Slack Adapter Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Debug-level, CloudAdapter-style logging for verified Slack payloads, converted Activities, and successful SlackAdapter send/update/delete operations without exposing secrets.

**Architecture:** Introduce a recursive JSON sanitizer and a source-generated `SlackAdapterLog` event registry. `SlackAdapter` will sanitize before logging and will emit outbound logs only after the corresponding Slack Web API call succeeds.

**Tech Stack:** .NET 8, C#, `Microsoft.Extensions.Logging`, source-generated `LoggerMessage`, `System.Text.Json`, xUnit, Moq

---

## File Structure

- Create `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackLogSanitizer.cs` for recursive, non-mutating JSON redaction.
- Create `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapterLog.cs` for stable source-generated logging events.
- Modify `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs` to emit inbound, Activity, send, update, and delete logs.
- Create `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackLogSanitizerTests.cs` for redaction behavior.
- Modify `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs` for adapter logging coverage and a recording logger.

### Task 1: Add recursive Slack JSON redaction

**Files:**
- Create: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackLogSanitizer.cs`
- Create: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackLogSanitizerTests.cs`

- [ ] **Step 1: Write failing sanitizer tests**

Create `SlackLogSanitizerTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Xunit;

namespace Microsoft.Agents.Extensions.Slack.Tests;

public class SlackLogSanitizerTests
{
    [Fact]
    public void SanitizeJson_RedactsSensitivePropertiesRecursively()
    {
        const string json = """
            {
              "token":"legacy-token",
              "nested":{
                "ApiToken":"xoxb-secret",
                "response_url":"https://hooks.slack.com/actions/secret"
              },
              "items":[
                {"access_token":"access-secret"},
                {"authorization":"Bearer secret"},
                {"bot_access_token":"bot-secret"},
                {"signing_secret":"signing-secret"}
              ],
              "text":"keep me"
            }
            """;

        var sanitized = SlackLogSanitizer.SanitizeJson(json);

        Assert.Contains("\"text\":\"keep me\"", sanitized);
        Assert.Contains("[REDACTED]", sanitized);
        Assert.DoesNotContain("legacy-token", sanitized);
        Assert.DoesNotContain("xoxb-secret", sanitized);
        Assert.DoesNotContain("hooks.slack.com", sanitized);
        Assert.DoesNotContain("access-secret", sanitized);
        Assert.DoesNotContain("Bearer secret", sanitized);
        Assert.DoesNotContain("bot-secret", sanitized);
        Assert.DoesNotContain("signing-secret", sanitized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    public void SanitizeJson_InvalidJson_ReturnsUnavailable(string json)
    {
        Assert.Equal("[UNAVAILABLE]", SlackLogSanitizer.SanitizeJson(json));
    }
}
```

- [ ] **Step 2: Run the sanitizer tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackLogSanitizerTests"
```

Expected: compilation fails because `SlackLogSanitizer` does not exist.

- [ ] **Step 3: Implement the sanitizer**

Create `SlackLogSanitizer.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.Agents.Extensions.Slack;

internal static class SlackLogSanitizer
{
    private const string Redacted = "[REDACTED]";
    private const string Unavailable = "[UNAVAILABLE]";

    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "apitoken",
        "accesstoken",
        "botaccesstoken",
        "authorization",
        "signingsecret",
        "responseurl",
    };

    internal static string SanitizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unavailable;
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return Unavailable;
            }

            Redact(node);
            return node.ToJsonString();
        }
        catch (JsonException)
        {
            return Unavailable;
        }
    }

    private static void Redact(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var propertyName in obj.Select(property => property.Key).ToArray())
            {
                var normalizedPropertyName = propertyName
                    .Replace("_", string.Empty, StringComparison.Ordinal)
                    .Replace("-", string.Empty, StringComparison.Ordinal);

                if (SensitiveProperties.Contains(normalizedPropertyName))
                {
                    obj[propertyName] = Redacted;
                }
                else if (obj[propertyName] is JsonNode child)
                {
                    Redact(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child != null)
                {
                    Redact(child);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run the sanitizer tests and verify they pass**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackLogSanitizerTests"
```

Expected: all `SlackLogSanitizerTests` pass.

- [ ] **Step 5: Commit the sanitizer**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackLogSanitizer.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackLogSanitizerTests.cs
git commit -m "Add Slack log redaction" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 2: Log verified inbound payloads and converted Activities

**Files:**
- Create: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapterLog.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`

- [ ] **Step 1: Add a recording logger to adapter tests**

Add these usings:

```csharp
using Microsoft.Extensions.Logging;
using System.Linq;
```

Add this nested type to `SlackAdapterTests`:

```csharp
private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);

private sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }
}
```

Extend `CreateAdapter` with an optional logger:

```csharp
private static SlackAdapter CreateAdapter(
    out Mock<IHttpClientFactory> factory,
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? sendFunc = null,
    ILogger<SlackAdapter>? logger = null)
```

Pass `logger` as the third `SlackAdapter` constructor argument.

- [ ] **Step 2: Write failing inbound logging tests**

Add:

```csharp
[Fact]
public async Task ProcessAsync_VerifiedEvent_LogsSanitizedPayloadAndActivity()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    var body = """
        {
          "token":"legacy-secret",
          "type":"event_callback",
          "team_id":"T1",
          "event_id":"EvLOG",
          "event":{"type":"message","channel":"C100","text":"hello","ts":"1700000000.000100","user":"U999","channel_type":"channel"}
        }
        """;
    var context = CreateContext(body, signed: true);

    await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

    var payloadLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 1));
    Assert.Equal(LogLevel.Debug, payloadLog.Level);
    Assert.Contains("hello", payloadLog.Message);
    Assert.Contains("[REDACTED]", payloadLog.Message);
    Assert.DoesNotContain("legacy-secret", payloadLog.Message);

    var activityLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 2));
    Assert.Contains("\"type\":\"message\"", activityLog.Message);
    Assert.Contains("[REDACTED]", activityLog.Message);
    Assert.DoesNotContain(BotToken, activityLog.Message);
}

[Fact]
public async Task ProcessAsync_VerifiedInteractivity_LogsSanitizedPayload()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    var payload = """
        {"type":"block_actions","response_url":"https://hooks.slack.com/actions/secret","user":{"id":"U777"},"team":{"id":"T1"},"channel":{"id":"C200"},"message":{"ts":"1700000000.000300"}}
        """;
    var context = CreateContext(
        "payload=" + WebUtility.UrlEncode(payload),
        signed: true,
        contentType: "application/x-www-form-urlencoded");

    await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

    var payloadLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 1));
    Assert.Contains("[REDACTED]", payloadLog.Message);
    Assert.DoesNotContain("hooks.slack.com", payloadLog.Message);
}

[Fact]
public async Task ProcessAsync_InvalidSignature_DoesNotLogPayload()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    const string body = """{"type":"url_verification","challenge":"do-not-log"}""";
    var context = CreateContext(body, signed: true, tamperSignature: true);

    await adapter.ProcessAsync(context.Request, context.Response, NoopAgent(), CancellationToken.None);

    Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 1);
    Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("do-not-log", StringComparison.Ordinal));
}
```

- [ ] **Step 3: Run the inbound logging tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_VerifiedEvent_LogsSanitizedPayloadAndActivity|FullyQualifiedName~SlackAdapterTests.ProcessAsync_VerifiedInteractivity_LogsSanitizedPayload|FullyQualifiedName~SlackAdapterTests.ProcessAsync_InvalidSignature_DoesNotLogPayload"
```

Expected: tests fail because event IDs 1 and 2 are not logged.

- [ ] **Step 4: Add the generated logging registry**

Create `SlackAdapterLog.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Slack;

internal static partial class SlackAdapterLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Slack payload received: RequestId={RequestId}, Payload='{Payload}'")]
    internal static partial void LogPayloadReceived(ILogger logger, string requestId, string payload);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Slack Activity created: RequestId={RequestId}, ConversationId={ConversationId}, Activity='{Activity}'")]
    internal static partial void LogActivityCreated(
        ILogger logger,
        string requestId,
        string conversationId,
        string activity);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Slack message sent: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Activity='{Activity}'")]
    internal static partial void LogMessageSent(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string activity);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Slack message updated: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Activity='{Activity}'")]
    internal static partial void LogMessageUpdated(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string activity);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Slack message deleted: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Reference='{Reference}'")]
    internal static partial void LogMessageDeleted(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string reference);
}
```

- [ ] **Step 5: Emit inbound and Activity logs**

In `SlackAdapter.ProcessAsync`, after signature verification:

```csharp
var requestId = httpRequest.HttpContext.TraceIdentifier;
var isFormUrlEncoded = IsFormUrlEncoded(httpRequest.ContentType);
var receivedPayload = isFormUrlEncoded ? ExtractFormValue(body, "payload") : body;

if (Logger.IsEnabled(LogLevel.Debug))
{
    SlackAdapterLog.LogPayloadReceived(
        Logger,
        requestId,
        SlackLogSanitizer.SanitizeJson(receivedPayload));
}
```

Reuse `isFormUrlEncoded` and `receivedPayload` in the conversion branch:

```csharp
if (isFormUrlEncoded)
{
    var payloadJson = receivedPayload;
    if (string.IsNullOrEmpty(payloadJson))
    {
        httpResponse.StatusCode = (int)HttpStatusCode.OK;
        return;
    }

    activity = CreateActivityFromInteractivePayload(payloadJson);
}
```

Immediately before `ProcessActivityAsync`:

```csharp
if (Logger.IsEnabled(LogLevel.Debug))
{
    SlackAdapterLog.LogActivityCreated(
        Logger,
        requestId,
        activity.Conversation?.Id,
        SlackLogSanitizer.SanitizeJson(ProtocolJsonSerializer.ToJson(activity)));
}
```

- [ ] **Step 6: Run the inbound logging tests and verify they pass**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_VerifiedEvent_LogsSanitizedPayloadAndActivity|FullyQualifiedName~SlackAdapterTests.ProcessAsync_VerifiedInteractivity_LogsSanitizedPayload|FullyQualifiedName~SlackAdapterTests.ProcessAsync_InvalidSignature_DoesNotLogPayload"
```

Expected: all three tests pass.

- [ ] **Step 7: Commit inbound logging**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterLog.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Log inbound Slack activities" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 3: Log successful SlackAdapter responses

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`

- [ ] **Step 1: Write failing send logging assertions**

Update `ProcessAsync_SendActivity_PostsToSlack` to create a logger:

```csharp
var logger = new RecordingLogger<SlackAdapter>();
var adapter = CreateAdapter(out _, (request, cancellationToken) =>
{
    // Existing response setup.
}, logger);
```

After the HTTP request assertions, add:

```csharp
var sentLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 3));
Assert.Contains("pong", sentLog.Message);
Assert.Contains("1700000000.000200", sentLog.Message);
Assert.DoesNotContain(BotToken, sentLog.Message);
```

Add a no-op test:

```csharp
[Fact]
public async Task SendActivitiesAsync_NonMessage_DoesNotLogSentResponse()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    var turnContext = new Mock<ITurnContext>();
    turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
    {
        Conversation = new ConversationAccount(id: "B123:T1:C100"),
    });

    await adapter.SendActivitiesAsync(
        turnContext.Object,
        [new Activity { Type = ActivityTypes.Typing }],
        CancellationToken.None);

    Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 3);
}
```

- [ ] **Step 2: Write failing update and delete logging tests**

Add:

```csharp
[Fact]
public async Task UpdateActivityAsync_LogsSuccessfulResponse()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    var turnContext = new Mock<ITurnContext>();
    turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
    {
        Conversation = new ConversationAccount(id: "B123:T1:C100"),
        ChannelData = new SlackChannelData { ApiToken = BotToken },
    });
    var activity = MessageFactory.Text("updated");
    activity.Id = "1700000000.000100";

    await adapter.UpdateActivityAsync(turnContext.Object, activity, CancellationToken.None);

    var updateLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 4));
    Assert.Contains("updated", updateLog.Message);
    Assert.Contains("\"1\"", updateLog.Message);
    Assert.DoesNotContain(BotToken, updateLog.Message);
}

[Fact]
public async Task DeleteActivityAsync_LogsSuccessfulResponse()
{
    var logger = new RecordingLogger<SlackAdapter>();
    var adapter = CreateAdapter(out _, logger: logger);
    var turnContext = new Mock<ITurnContext>();
    turnContext.SetupGet(context => context.Activity).Returns(new SlackActivity
    {
        Conversation = new ConversationAccount(id: "B123:T1:C100"),
        ChannelData = new SlackChannelData { ApiToken = BotToken },
    });
    var reference = new ConversationReference
    {
        ActivityId = "1700000000.000100",
        Conversation = new ConversationAccount(id: "B123:T1:C100"),
    };

    await adapter.DeleteActivityAsync(turnContext.Object, reference, CancellationToken.None);

    var deleteLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 5));
    Assert.Contains("1700000000.000100", deleteLog.Message);
    Assert.DoesNotContain(BotToken, deleteLog.Message);
}
```

- [ ] **Step 3: Run the outbound logging tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_SendActivity_PostsToSlack|FullyQualifiedName~SlackAdapterTests.SendActivitiesAsync_NonMessage_DoesNotLogSentResponse|FullyQualifiedName~SlackAdapterTests.UpdateActivityAsync_LogsSuccessfulResponse|FullyQualifiedName~SlackAdapterTests.DeleteActivityAsync_LogsSuccessfulResponse"
```

Expected: send, update, and delete tests fail because events 3, 4, and 5 are not emitted; the no-op assertion passes.

- [ ] **Step 4: Log successful sends**

After `chat.postMessage` succeeds:

```csharp
if (Logger.IsEnabled(LogLevel.Debug))
{
    SlackAdapterLog.LogMessageSent(
        Logger,
        conversationId,
        response.ts,
        SlackLogSanitizer.SanitizeJson(ProtocolJsonSerializer.ToJson(activity)));
}
```

Keep the log after the API call so failures never appear as successful responses.

- [ ] **Step 5: Log successful updates**

After `chat.update` succeeds:

```csharp
if (Logger.IsEnabled(LogLevel.Debug))
{
    SlackAdapterLog.LogMessageUpdated(
        Logger,
        turnContext.Activity?.Conversation?.Id,
        response.ts,
        SlackLogSanitizer.SanitizeJson(ProtocolJsonSerializer.ToJson(activity)));
}
```

- [ ] **Step 6: Make delete asynchronous and log successful deletes**

Change `DeleteActivityAsync` to `async Task`, await the API result, and log:

```csharp
var response = await _slackApi.CallAsync("chat.delete", new
{
    channel,
    ts = reference.ActivityId,
}, channelData?.ApiToken ?? _options.BotToken, cancellationToken).ConfigureAwait(false);

if (Logger.IsEnabled(LogLevel.Debug))
{
    SlackAdapterLog.LogMessageDeleted(
        Logger,
        reference.Conversation?.Id,
        response.ts ?? reference.ActivityId,
        SlackLogSanitizer.SanitizeJson(ProtocolJsonSerializer.ToJson(reference)));
}
```

- [ ] **Step 7: Run all Slack extension tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\
```

Expected: all tests pass.

- [ ] **Step 8: Build the Slack extension and sample**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Microsoft.Agents.Extensions.Slack.csproj --no-restore
dotnet build src\samples\SlackAgent\SlackAgent.csproj --no-restore
```

Expected: both builds succeed with zero warnings and zero errors.

- [ ] **Step 9: Commit outbound logging**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Log Slack adapter responses" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```
