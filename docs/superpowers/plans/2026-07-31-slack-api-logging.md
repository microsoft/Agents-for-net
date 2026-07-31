# Slack API Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Log every outbound Slack Web API request and response used by SlackAdapter, SlackAgentExtension, and Slack streams.

**Architecture:** Add a source-generated `SlackApiLog` registry and inject `ILogger<SlackApi>` into the two SlackApi construction paths. `SlackApi.CallAsync` logs sanitized request options before HTTP send and sanitized HTTP response content before validation, while existing higher-level SlackAdapter Activity logs remain unchanged.

**Tech Stack:** .NET 8, C#, `Microsoft.Extensions.Logging`, `System.Text.Json`, xUnit, Moq

---

## File Structure

- Create `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/Api/SlackApiLog.cs` for Slack API request and response events.
- Modify `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/Api/SlackApi.cs` to emit centralized request/response logs.
- Modify `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs` to supply `ILogger<SlackApi>`.
- Modify `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAgentExtension.cs` to supply `ILogger<SlackApi>`.
- Create `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackApiLoggingTests.cs` for direct API logging.
- Modify `src/samples/SlackAgent/appsettings.json` to enable Slack Debug logs.

### Task 1: Add centralized Slack API request and response logging

**Files:**
- Create: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/Api/SlackApiLog.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/Api/SlackApi.cs`
- Create: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackApiLoggingTests.cs`

- [ ] **Step 1: Write failing SlackApi logging tests**

Create tests using a recording `ILogger<SlackApi>` and test HTTP handler:

```csharp
[Fact]
public async Task CallAsync_LogsSanitizedRequestAndResponse()
{
    var logger = new RecordingLogger<SlackApi>();
    var api = CreateApi(
        logger,
        """{"ok":true,"ts":"1700000000.000200","access_token":"response-secret"}""");

    var response = await api.CallAsync(
        "chat.postMessage",
        new
        {
            channel = "C100",
            text = "hello",
            token = "request-secret",
        },
        "xoxb-bearer-secret");

    Assert.True(response.ok);

    var requestLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 1));
    Assert.Contains("chat.postMessage", requestLog.Message);
    Assert.Contains("\"text\":\"hello\"", requestLog.Message);
    Assert.Contains("[REDACTED]", requestLog.Message);
    Assert.DoesNotContain("request-secret", requestLog.Message);
    Assert.DoesNotContain("xoxb-bearer-secret", requestLog.Message);

    var responseLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 2));
    Assert.Contains("200", responseLog.Message);
    Assert.Contains("1700000000.000200", responseLog.Message);
    Assert.Contains("[REDACTED]", responseLog.Message);
    Assert.DoesNotContain("response-secret", responseLog.Message);
    Assert.DoesNotContain("xoxb-bearer-secret", responseLog.Message);
}

[Fact]
public async Task CallAsync_LogsSlackErrorResponseBeforeThrowing()
{
    var logger = new RecordingLogger<SlackApi>();
    var api = CreateApi(logger, """{"ok":false,"error":"channel_not_found"}""");

    await Assert.ThrowsAsync<SlackResponseException>(() =>
        api.CallAsync("chat.postMessage", new { channel = "missing" }, "xoxb-secret"));

    var responseLog = Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 2));
    Assert.Contains("channel_not_found", responseLog.Message);
}

[Fact]
public async Task CallAsync_TransportFailure_LogsRequestOnly()
{
    var logger = new RecordingLogger<SlackApi>();
    var api = CreateApi(logger, (_, _) => throw new HttpRequestException("offline"));

    await Assert.ThrowsAsync<HttpRequestException>(() =>
        api.CallAsync("chat.postMessage", new { channel = "C100" }, "xoxb-secret"));

    Assert.Single(logger.Entries.Where(entry => entry.EventId.Id == 1));
    Assert.DoesNotContain(logger.Entries, entry => entry.EventId.Id == 2);
}
```

The test helper must assert that the outgoing HTTP Authorization header is
present for the request while ensuring the logger never records its value.

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackApiLoggingTests"
```

Expected: compilation fails because the `SlackApi` logger constructor and
`SlackApiLog` events do not exist.

- [ ] **Step 3: Add the generated SlackApi log registry**

Create `SlackApiLog.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Slack.Api;

internal static partial class SlackApiLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Slack API request: Method={Method}, Options='{Options}'")]
    internal static partial void LogRequest(ILogger logger, string method, string options);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Slack API response: Method={Method}, StatusCode={StatusCode}, Response='{Response}'")]
    internal static partial void LogResponse(
        ILogger logger,
        string method,
        int statusCode,
        string response);
}
```

- [ ] **Step 4: Inject the logger into SlackApi**

Change the constructor to:

```csharp
private readonly IHttpClientFactory _httpClientFactory;
private readonly ILogger<SlackApi> _logger;

public SlackApi(IHttpClientFactory httpClientFactory, ILogger<SlackApi> logger = null)
{
    AssertionHelpers.ThrowIfNull(httpClientFactory, nameof(httpClientFactory));
    _httpClientFactory = httpClientFactory;
    _logger = logger ?? NullLogger<SlackApi>.Instance;
}
```

Add the required `Microsoft.Extensions.Logging` and
`Microsoft.Extensions.Logging.Abstractions` usings.

- [ ] **Step 5: Log the sanitized request before HTTP send**

After the options JSON is created and before `SendAsync`:

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
{
    SlackApiLog.LogRequest(
        _logger,
        method,
        SlackLogSanitizer.SanitizeJson(json));
}
```

Do not serialize or log the bearer token or Authorization header.

- [ ] **Step 6: Log the sanitized response before validation**

Immediately after reading the response body and before deserialization:

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
{
    SlackApiLog.LogResponse(
        _logger,
        method,
        (int)response.StatusCode,
        SlackLogSanitizer.SanitizeJson(text));
}
```

This placement must preserve error response logging before
`SlackResponseException`.

- [ ] **Step 7: Run the focused and full Slack tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackApiLoggingTests"
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\
```

Expected: all tests pass.

- [ ] **Step 8: Commit centralized Slack API logging**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackApi.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Api\SlackApiLog.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackApiLoggingTests.cs
git commit -m "Log Slack API requests and responses" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 2: Wire SlackApi logging through adapter, extension, and sample

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAgentExtension.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`
- Modify: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAgentExtensionTests.cs`
- Modify: `src/samples/SlackAgent/appsettings.json`

- [ ] **Step 1: Add failing adapter logger wiring test**

Extend the existing successful send test to supply separate recording loggers:

```csharp
var adapterLogger = new RecordingLogger<SlackAdapter>();
var apiLogger = new RecordingLogger<SlackApi>();
var adapter = CreateAdapter(
    out _,
    sendFunc,
    adapterLogger,
    apiLogger);
```

Assert that the call produces:

```csharp
Assert.Single(apiLogger.Entries.Where(entry => entry.EventId.Id == 1));
Assert.Single(apiLogger.Entries.Where(entry => entry.EventId.Id == 2));
```

Update the `SlackAdapter` test helper and production constructor to accept an
optional `ILogger<SlackApi>`.

- [ ] **Step 2: Run the adapter wiring test and verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_SendActivity_PostsToSlack"
```

Expected: compilation fails because `SlackAdapter` cannot receive the SlackApi
logger.

- [ ] **Step 3: Wire SlackAdapter to SlackApi**

Change the constructor:

```csharp
public SlackAdapter(
    SlackAdapterOptions options,
    IHttpClientFactory httpClientFactory,
    ILogger<SlackAdapter> logger = null,
    ILogger<SlackApi> slackApiLogger = null)
    : base(logger)
{
    _options = options ?? throw new ArgumentNullException(nameof(options));
    _slackApi = new SlackApi(
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory)),
        slackApiLogger);
}
```

- [ ] **Step 4: Add failing SlackAgentExtension logger wiring test**

In `SlackAgentExtensionTests`, construct `AgentApplicationOptions` with a
recording `ILoggerFactory`, invoke `SlackExtension.CallAsync`, and assert
`SlackApi` request/response events are recorded. The test must use the real
`SlackAgentExtension` before-turn service registration rather than directly
constructing `SlackApi`.

- [ ] **Step 5: Run the extension wiring test and verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAgentExtensionTests"
```

Expected: the new test fails because the extension-created SlackApi does not
receive a logger.

- [ ] **Step 6: Wire SlackAgentExtension to SlackApi**

Change:

```csharp
var slackApi = new SlackApi(
    application.Options.HttpClientFactory,
    application.Options.LoggerFactory.CreateLogger<SlackApi>());
```

Add `using Microsoft.Extensions.Logging;`.

- [ ] **Step 7: Enable Slack Debug logging in the sample**

Add this category to `src/samples/SlackAgent/appsettings.json`:

```json
"Microsoft.Agents.Extensions.Slack": "Debug"
```

Keep `Microsoft.AspNetCore` at `Warning`. Do not place secrets in
`appsettings.json`.

- [ ] **Step 8: Run all Slack tests and builds**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Microsoft.Agents.Extensions.Slack.csproj --no-restore
dotnet build src\samples\SlackAgent\SlackAgent.csproj --no-restore
```

Expected: all tests pass and both builds succeed without warnings or errors.

- [ ] **Step 9: Commit logger wiring**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAgentExtension.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAgentExtensionTests.cs src\samples\SlackAgent\appsettings.json
git commit -m "Wire Slack API logging" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```
