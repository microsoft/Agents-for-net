# Slack Interactivity Activity Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make direct Slack interactivity produce the same Activity mappings as Intercom's `SlackMapper.ToV3Activity`.

**Architecture:** Keep common Slack identity, conversation, and channel-data construction in `SlackAdapter.CreateActivityFromInteractivePayload`, then select the Activity contract from the first Slack action. Preserve the raw JSON payload for unknown actions, and add optional bot display-name configuration for Intercom-compatible mention entities.

**Tech Stack:** C# 12, .NET 8, Microsoft Agents SDK Activity models, System.Text.Json, xUnit, Moq.

---

## File Structure

- Modify `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs` to expose the optional Slack bot display name.
- Modify `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs` to map every `ToV3Activity` conditional.
- Modify `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs` to cover select, button, legacy message action, and fallback mappings.
- Modify `src\samples\SlackAgent\appsettings.json` to document `BotName`.

### Task 1: Add Bot Display Name Configuration

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs`
- Modify: `src\samples\SlackAgent\appsettings.json`
- Test: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`

- [ ] **Step 1: Write the failing options test**

Add this test:

```csharp
[Fact]
public void SlackAdapterOptions_BotName_RetainsConfiguredValue()
{
    var options = new SlackAdapterOptions { BotName = "SlackAgent" };

    Assert.Equal("SlackAgent", options.BotName);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.SlackAdapterOptions_BotName_RetainsConfiguredValue" --no-restore
```

Expected: compilation fails because `SlackAdapterOptions.BotName` does not exist.

- [ ] **Step 3: Add `BotName` to adapter options**

Add to `SlackAdapterOptions`:

```csharp
/// <summary>
/// The Slack bot display name used in mention entities created for interactive message actions.
/// When empty, <see cref="BotId"/> is used as the mention text fallback.
/// </summary>
public string BotName { get; set; }
```

Add to the sample's `Slack` configuration:

```json
"BotName": "your-slack-bot-display-name"
```

- [ ] **Step 4: Run the options test**

Run the command from Step 2.

Expected: the test passes.

- [ ] **Step 5: Commit the configuration surface**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs src\samples\SlackAgent\appsettings.json src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Configure Slack bot mention name" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 2: Map Select and Button Actions to Message Activities

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`

- [ ] **Step 1: Write failing select and button mapping tests**

Extend the test helper signature and option construction:

```csharp
private static SlackAdapter CreateAdapter(
    out Mock<IHttpClientFactory> factory,
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? sendFunc = null,
    ILogger<SlackAdapter>? logger = null,
    ILogger<SlackApi>? slackApiLogger = null,
    string? botName = null)
{
    sendFunc ??= (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("""{"ok":true,"ts":"1"}""", Encoding.UTF8, "application/json")
    });

    factory = CreateFactory(sendFunc);
    return new SlackAdapter(
        new SlackAdapterOptions
        {
            BotToken = BotToken,
            SigningSecret = SigningSecret,
            BotId = BotId,
            BotUserId = BotUserId,
            BotName = botName,
        },
        factory.Object,
        logger!,
        slackApiLogger!);
}
```

Add one test for each Intercom branch:

```csharp
[Fact]
public async Task ProcessAsync_SelectAction_CreatesMessageActivity()
{
    var payload = """
        {
          "type":"block_actions",
          "user":{"id":"U777","team_id":"T1"},
          "team":{"id":"T1"},
          "channel":{"id":"C200"},
          "message":{"ts":"1700000000.000300"},
          "actions":[{
            "type":"select",
            "selected_options":[{"value":"option&amp;one"}]
          }]
        }
        """;

    var activity = await ProcessInteractivePayloadAsync(payload, botName: "SlackAgent");

    Assert.Equal(ActivityTypes.Message, activity.Type);
    Assert.Equal("option&one", activity.Text);
    var mention = Assert.IsType<Mention>(Assert.Single(activity.Entities));
    Assert.Equal("@SlackAgent", mention.Text);
    Assert.Equal("B123:T1", mention.Mentioned.Id);
}

[Fact]
public async Task ProcessAsync_ButtonAction_CreatesMessageActivity()
{
    var payload = """
        {
          "type":"interactive_message",
          "user":{"id":"U777","team_id":"T1"},
          "team":{"id":"T1"},
          "channel":"C200",
          "message":{"ts":"1700000000.000300"},
          "actions":[{"type":"button","value":"yes&amp;please"}]
        }
        """;

    var activity = await ProcessInteractivePayloadAsync(payload, botName: null);

    Assert.Equal(ActivityTypes.Message, activity.Type);
    Assert.Equal("yes&please", activity.Text);
    var mention = Assert.IsType<Mention>(Assert.Single(activity.Entities));
    Assert.Equal("@B123", mention.Text);
    Assert.Equal("B123:T1", mention.Mentioned.Id);
}
```

Add a private test helper that uses the real adapter conversion:

```csharp
private static async Task<SlackActivity> ProcessInteractivePayloadAsync(string payload, string? botName = null)
{
    var adapter = CreateAdapter(out _, botName: botName);
    var context = CreateContext(
        "payload=" + WebUtility.UrlEncode(payload),
        signed: true,
        contentType: "application/x-www-form-urlencoded");

    IActivity? captured = null;
    await adapter.ProcessAsync(
        context.Request,
        context.Response,
        DelegateAgent((turnContext, _) =>
        {
            captured = turnContext.Activity;
            return Task.CompletedTask;
        }),
        CancellationToken.None);

    return Assert.IsType<SlackActivity>(captured);
}
```

- [ ] **Step 2: Run the two tests to verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_SelectAction_CreatesMessageActivity|FullyQualifiedName~SlackAdapterTests.ProcessAsync_ButtonAction_CreatesMessageActivity" --no-restore
```

Expected: both fail because current conversion returns `ActivityTypes.Event`.

- [ ] **Step 3: Implement select and button mapping**

In `CreateActivityFromInteractivePayload`, add this branch after feedback handling:

```csharp
else if ((string.Equals(payload.type, "interactive_message", StringComparison.Ordinal)
        || string.Equals(payload.type, "block_actions", StringComparison.Ordinal))
    && (string.Equals(actionType, "select", StringComparison.Ordinal)
        || string.Equals(actionType, "button", StringComparison.Ordinal)))
{
    activity.Type = ActivityTypes.Message;
    activity.Name = null;
    activity.Text = string.Equals(actionType, "select", StringComparison.Ordinal)
        ? payload.Get<string>("actions[0].selected_options[0].value").SlackDecode()
        : payload.Get<string>("actions[0].value").SlackDecode();

    activity.Entities =
    [
        new Mention
        {
            Mentioned = activity.Recipient,
            Text = "@" + (string.IsNullOrEmpty(_options.BotName) ? _options.BotId : _options.BotName),
        },
    ];
}
```

- [ ] **Step 4: Run the focused tests**

Run the command from Step 2.

Expected: both tests pass.

- [ ] **Step 5: Commit select and button mapping**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Map Slack interactive message actions" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 3: Map Legacy Message Actions and Unknown Payloads

**Files:**
- Modify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Modify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`

- [ ] **Step 1: Write failing legacy and fallback tests**

```csharp
[Fact]
public async Task ProcessAsync_MessageAction_CreatesLegacySlackEvent()
{
    var payload = """
        {
          "type":"message_action",
          "callback_id":"legacy-action",
          "user":{"id":"U777","team_id":"T1"},
          "team":{"id":"T1"},
          "channel":{"id":"C200"}
        }
        """;

    var activity = await ProcessInteractivePayloadAsync(payload);

    Assert.Equal(ActivityTypes.Event, activity.Type);
    Assert.Equal("SlackActivity", activity.Name);
    Assert.Equal("legacy-action", activity.Value);
}

[Fact]
public async Task ProcessAsync_UnknownInteractivePayload_CreatesVendorEventWithRawPayload()
{
    var payload = """
        {
          "type":"view_submission",
          "user":{"id":"U777","team_id":"T1"},
          "team":{"id":"T1"},
          "channel":{"id":"C200","name":"directmessage"},
          "view":{"id":"V123","app_installed_team_id":"T1"}
        }
        """;

    var activity = await ProcessInteractivePayloadAsync(payload);

    Assert.Equal(ActivityTypes.Event, activity.Type);
    Assert.Equal("vnd.slack.action.view_submission", activity.Name);

    var value = ProtocolJsonSerializer.ToObject<JsonObject>(activity.Value);
    Assert.Equal("C200", value!["channel"]!["id"]!.GetValue<string>());
    Assert.Equal("directmessage", value["channel"]!["name"]!.GetValue<string>());
    Assert.Equal("V123", value["view"]!["id"]!.GetValue<string>());
}
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_MessageAction_CreatesLegacySlackEvent|FullyQualifiedName~SlackAdapterTests.ProcessAsync_UnknownInteractivePayload_CreatesVendorEventWithRawPayload" --no-restore
```

Expected: the legacy name/value assertions fail, and the unknown payload is named only `view_submission` with no Activity value.

- [ ] **Step 3: Parse the raw payload once**

At the start of `CreateActivityFromInteractivePayload`, preserve a lossless JSON node alongside the typed model:

```csharp
var rawPayload = JsonNode.Parse(payloadJson);
var payload = ProtocolJsonSerializer.ToObject<ActionPayload>(payloadJson);
```

Add:

```csharp
using System.Text.Json.Nodes;
```

- [ ] **Step 4: Implement legacy and fallback mapping**

Complete the conditional chain after select/button:

```csharp
else if (string.Equals(payload.type, "message_action", StringComparison.Ordinal)
    && !string.IsNullOrEmpty(payload.Get<string>("callback_id")))
{
    activity.Type = ActivityTypes.Event;
    activity.Name = "SlackActivity";
    activity.Value = payload.Get<string>("callback_id");
}
else
{
    activity.Type = ActivityTypes.Event;
    activity.Name = $"vnd.slack.action.{payload.type}";
    activity.Value = rawPayload;
}
```

The existing feedback branch remains first, and select/button remains second, matching Intercom branch order.

- [ ] **Step 5: Run the focused tests**

Run the command from Step 2.

Expected: both tests pass.

- [ ] **Step 6: Run existing interactivity tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_Interactive|FullyQualifiedName~SlackAdapterTests.ProcessAsync_OrgInstalledBlockAction|FullyQualifiedName~SlackAdapterTests.ProcessAsync_OrgInstalledViewSubmission|FullyQualifiedName~SlackAdapterTests.ProcessAsync_FeedbackButtons" --no-restore
```

Expected: all selected tests pass. Update the old generic interactive test to expect `vnd.slack.action.block_actions` and a complete Activity value when its action type is not mapped.

- [ ] **Step 7: Commit legacy and fallback mapping**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Complete Slack interactivity Activity mapping" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 4: Verify the Complete Slack Transport

**Files:**
- Verify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs`
- Verify: `src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs`
- Verify: `src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs`
- Verify: `src\samples\SlackAgent\appsettings.json`

- [ ] **Step 1: Run the complete Slack test project**

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\Microsoft.Agents.Extensions.Slack.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: all Slack tests pass with zero failures.

- [ ] **Step 2: Build the Slack extension**

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Microsoft.Agents.Extensions.Slack.csproj --no-restore --verbosity minimal
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 3: Build the SlackAgent sample**

```powershell
dotnet build src\samples\SlackAgent\SlackAgent.csproj --no-restore --verbosity minimal
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 4: Inspect the final diff**

```powershell
git --no-pager diff --check
git --no-pager status --short
```

Expected: no whitespace errors; only the intended Slack adapter, options, sample configuration, and tests are modified beyond the previously approved direct-transport work.
