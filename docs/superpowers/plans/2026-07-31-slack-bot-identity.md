# Slack Bot Identity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make direct `SlackAdapter` activities use the same encoded bot, user, and conversation identities as the Intercom/Azure Bot Service Slack channel.

**Architecture:** Add a distinct `BotId` option for Slack's `B...` identity while preserving `BotUserId` for the OAuth `U...` identity. Centralize `<slack-id>:<team-id>` account encoding in `SlackHelpers`, then use it consistently when mapping Events API and interactivity payloads into activities.

**Tech Stack:** .NET 8, C#, Microsoft Agents SDK Activity model, xUnit, Moq

---

## File Structure

- `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapterOptions.cs`
  defines the two Slack bot identities and documents their separate purposes.
- `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackHelpers.cs`
  owns account and conversation ID encoding and decoding.
- `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
  maps Slack Events API and interactivity payloads into ABS-compatible activities.
- `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackHelpersTests.cs`
  verifies encoded account ID behavior.
- `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`
  verifies activity identity mapping and bot self-message filtering.

### Task 1: Add Slack account identity helpers

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackHelpers.cs`
- Test: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackHelpersTests.cs`

- [ ] **Step 1: Write failing account ID helper tests**

Add these tests to `SlackHelpersTests`:

```csharp
[Fact]
public void CreateAccountId_ReturnsSlackAndTeamIds()
{
    Assert.Equal("U123:T123", SlackHelpers.CreateAccountId("U123", "T123"));
}

[Fact]
public void AccountIdHelpers_ExtractParts()
{
    const string accountId = "U123:T123";

    Assert.Equal("U123", SlackHelpers.SlackIdFromAccountId(accountId));
    Assert.Equal("T123", SlackHelpers.SlackTeamIdFromAccountId(accountId));
}

[Theory]
[InlineData("U123")]
[InlineData("U123:T123:extra")]
public void SlackIdFromAccountId_InvalidFormat_ThrowsArgumentException(string accountId)
{
    var exception = Assert.Throws<ArgumentException>(() => SlackHelpers.SlackIdFromAccountId(accountId));

    Assert.Equal("accountId", exception.ParamName);
}
```

- [ ] **Step 2: Run the helper tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackHelpersTests"
```

Expected: compilation fails because `CreateAccountId`, `SlackIdFromAccountId`, and `SlackTeamIdFromAccountId` do not exist.

- [ ] **Step 3: Implement the account ID helpers**

Add these methods to `SlackHelpers`:

```csharp
public static string CreateAccountId(string slackId, string slackTeamId)
{
    return $"{slackId}:{slackTeamId}";
}

public static string SlackIdFromAccountId(string accountId)
{
    return FromAccountId(accountId, 0);
}

public static string SlackTeamIdFromAccountId(string accountId)
{
    return FromAccountId(accountId, 1);
}

private static string FromAccountId(string accountId, int pos)
{
    AssertionHelpers.ThrowIfNullOrWhiteSpace(accountId, nameof(accountId));

    var split = accountId.Split(':');
    if (split.Length != 2)
    {
        throw new ArgumentException($"Invalid accountId: {accountId}", nameof(accountId));
    }

    return split[pos];
}
```

- [ ] **Step 4: Run the helper tests and verify they pass**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackHelpersTests"
```

Expected: all `SlackHelpersTests` pass.

- [ ] **Step 5: Commit the helper change**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackHelpers.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackHelpersTests.cs
git commit -m "Add Slack account identity helpers" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 2: Separate Slack bot and bot-user configuration

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapterOptions.cs`
- Test: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`

- [ ] **Step 1: Give adapter tests distinct bot identities**

Replace the existing bot identity constant with:

```csharp
private const string BotId = "B123";
private const string BotUserId = "U123";
```

Update both `SlackAdapterOptions` constructions to include the new property:

```csharp
new SlackAdapterOptions
{
    BotToken = BotToken,
    SigningSecret = SigningSecret,
    BotId = BotId,
    BotUserId = BotUserId,
}
```

For the no-signing-secret test, use:

```csharp
new SlackAdapterOptions
{
    BotToken = BotToken,
    BotId = BotId,
    BotUserId = BotUserId,
}
```

Change the existing recipient assertion to the Intercom-compatible encoded identity:

```csharp
Assert.Equal("B123:T1", slack.Recipient.Id);
```

- [ ] **Step 2: Run the message-event test and verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_MessageEvent_InvokesAgentWithSlackActivity"
```

Expected: compilation fails because `SlackAdapterOptions.BotId` does not exist.

- [ ] **Step 3: Add the distinct `BotId` option**

Add this property before `BotUserId` in `SlackAdapterOptions`:

```csharp
/// <summary>
/// The Slack bot id (starts with <c>B</c>). Used as the Activity recipient and
/// as the bot component of the conversation id.
/// </summary>
public string BotId { get; set; }
```

Replace the existing `BotUserId` documentation with:

```csharp
/// <summary>
/// The Slack user id of the bot (starts with <c>U</c>). Used to ignore the
/// bot user's own messages so the agent does not reply to itself.
/// </summary>
public string BotUserId { get; set; }
```

- [ ] **Step 4: Run the message-event test and verify the assertion still fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_MessageEvent_InvokesAgentWithSlackActivity"
```

Expected: the test runs but fails because `Recipient.Id` is still `U123`.

- [ ] **Step 5: Commit the option contract**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapterOptions.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Separate Slack bot identities" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 3: Map Events API activities to Intercom identities

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
- Test: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`

- [ ] **Step 1: Extend the message-event assertions**

Add these assertions to `ProcessAsync_MessageEvent_InvokesAgentWithSlackActivity`:

```csharp
Assert.Equal("U999:T1", slack.From.Id);
Assert.Equal("B123:T1", slack.Recipient.Id);
Assert.Equal("B123:T1:C100", slack.Conversation.Id);
```

Remove the old assertions expecting unencoded `BotUserId` and `U999`.

- [ ] **Step 2: Add a failing bot-user self-message test**

Add:

```csharp
[Fact]
public async Task ProcessAsync_BotUserOwnMessage_Ignored()
{
    var adapter = CreateAdapter(out _);
    var body = """
        {
          "type":"event_callback",
          "team_id":"T1",
          "event_id":"EvBOTUSER",
          "event":{"type":"message","channel":"C100","text":"loop","ts":"1700000000.000100","user":"U123"}
        }
        """;
    var context = CreateContext(body, signed: true);

    var agentCalled = false;
    await adapter.ProcessAsync(context.Request, context.Response, DelegateAgent((_, _) =>
    {
        agentCalled = true;
        return Task.CompletedTask;
    }), CancellationToken.None);

    Assert.False(agentCalled);
    Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
}
```

- [ ] **Step 3: Run the event mapping tests and verify they fail**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_MessageEvent_InvokesAgentWithSlackActivity|FullyQualifiedName~SlackAdapterTests.ProcessAsync_BotUserOwnMessage_Ignored"
```

Expected: the mapping test fails on unencoded sender/recipient or the conversation ID; the self-message test already passes because filtering remains keyed to `BotUserId`.

- [ ] **Step 4: Implement Events API identity mapping**

In `CreateActivityFromEvent`, keep self-message filtering against `_options.BotUserId`, then change the mapping to:

```csharp
var channel = content.channel;
var threadTs = content.Get<string>("thread_ts");
var teamId = envelope.team_id;

var activity = new SlackActivity
{
    ChannelId = Channels.Slack,
    ServiceUrl = SlackServiceUrl,
    Id = envelope.event_id ?? content.ts,
    Timestamp = DateTimeOffset.UtcNow,
    From = new ChannelAccount(id: SlackHelpers.CreateAccountId(content.user, teamId)),
    Recipient = new ChannelAccount(id: SlackHelpers.CreateAccountId(_options.BotId, teamId)),
    Conversation = new ConversationAccount(
        id: SlackHelpers.CreateConversationId(_options.BotId, teamId, channel, threadTs))
    {
        IsGroup = !string.Equals(content.channel_type, "im", StringComparison.Ordinal),
    },
};
```

Do not change `SlackChannelData`, activity type selection, text decoding, or event naming.

- [ ] **Step 5: Update the top-level reply expectation**

In `ProcessAsync_SendActivity_PostsToSlack`, replace:

```csharp
Assert.Contains("\"thread_ts\":\"1700000000.000100\"", post.Body, StringComparison.Ordinal);
```

with:

```csharp
Assert.DoesNotContain("\"thread_ts\"", post.Body, StringComparison.Ordinal);
```

This preserves the three-part top-level conversation ID used by Intercom/ABS. Threaded events continue to carry their actual `thread_ts`.

- [ ] **Step 6: Run all adapter tests and verify they pass**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests"
```

Expected: all `SlackAdapterTests` pass.

- [ ] **Step 7: Commit the Events API mapping**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Match Slack event activity identities" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```

### Task 4: Map interactivity activities to Intercom identities

**Files:**
- Modify: `src/libraries/Extensions/Microsoft.Agents.Extensions.Slack/SlackAdapter.cs`
- Test: `src/tests/Microsoft.Agents.Extensions.Slack.Tests/SlackAdapterTests.cs`

- [ ] **Step 1: Add interactivity identity assertions**

Add these assertions to `ProcessAsync_InteractivePayload_InvokesAgentAsEvent`:

```csharp
Assert.Equal("U777:T1", slack.From.Id);
Assert.Equal("B123:T1", slack.Recipient.Id);
Assert.Equal("B123:T1:C200:1700000000.000300", slack.Conversation.Id);
```

Replace the existing unencoded sender assertion.

- [ ] **Step 2: Run the interactivity test and verify it fails**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\ --filter "FullyQualifiedName~SlackAdapterTests.ProcessAsync_InteractivePayload_InvokesAgentAsEvent"
```

Expected: the test fails because sender, recipient, and conversation identities remain unencoded or use `BotUserId`.

- [ ] **Step 3: Implement interactivity identity mapping**

In `CreateActivityFromInteractivePayload`, introduce `teamId` and change the account/conversation mapping:

```csharp
var channel = payload.channel;
var user = payload.Get<string>("user.id");
var teamId = payload.Get<string>("team.id");
var threadTs = payload.Get<string>("message.thread_ts") ?? payload.Get<string>("message.ts");

var activity = new SlackActivity
{
    Type = ActivityTypes.Event,
    Name = payload.type,
    ChannelId = Channels.Slack,
    ServiceUrl = SlackServiceUrl,
    Id = Guid.NewGuid().ToString(),
    Timestamp = DateTimeOffset.UtcNow,
    From = new ChannelAccount(id: SlackHelpers.CreateAccountId(user, teamId)),
    Recipient = new ChannelAccount(id: SlackHelpers.CreateAccountId(_options.BotId, teamId)),
    Conversation = new ConversationAccount(
        id: SlackHelpers.CreateConversationId(_options.BotId, teamId, channel, threadTs)),
};
```

Do not change the raw payload stored in `SlackChannelData`.

- [ ] **Step 4: Run all Slack extension tests**

Run:

```powershell
dotnet test src\tests\Microsoft.Agents.Extensions.Slack.Tests\
```

Expected: all tests pass.

- [ ] **Step 5: Build the Slack extension**

Run:

```powershell
dotnet build src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\Microsoft.Agents.Extensions.Slack.csproj
```

Expected: build succeeds with zero warnings and zero errors.

- [ ] **Step 6: Commit the interactivity mapping**

```powershell
git add src\libraries\Extensions\Microsoft.Agents.Extensions.Slack\SlackAdapter.cs src\tests\Microsoft.Agents.Extensions.Slack.Tests\SlackAdapterTests.cs
git commit -m "Match Slack interactivity identities" -m "Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>" -m "Copilot-Session: 2ff0599a-2c2c-405e-828f-f975b919bf50"
```
