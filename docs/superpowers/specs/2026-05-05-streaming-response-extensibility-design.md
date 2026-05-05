# StreamingResponse Extensibility Design

**Date:** 2026-05-05
**Branch:** users/tracyboehrer/extend-streamingresponse
**Status:** Approved

## Problem

`StreamingResponse` is an internal class hardcoded for Teams, WebChat, and DirectLine (Activity-based channels). There is no extensibility mechanism for other channels (e.g., Slack) that stream differently — using non-Activity payloads, typed chunks, and no required final message. Channel extension authors have no way to plug in custom streaming behavior.

## Goals

- Allow channel extension authors to provide custom streaming implementations per channel
- Shared buffering + interval timer infrastructure available to all implementations
- No breaking changes to `IStreamingResponse` or existing app developer code
- Support both .NET 8 (keyed services) and .NET Framework 4.8 (dictionary registry)

## Non-Goals

- App developers configuring streaming behavior (extension authors only)
- Changes to `IStreamingResponse` interface contract
- New streaming features for existing Teams/WebChat/DirectLine channels

---

## Architecture Overview

Five components are introduced or modified:

```
StreamingResponseBase (abstract, new)
  └── StreamingResponse       ← existing class, refactored to extend base
  └── SlackStreamingResponse  ← in Slack extension (example of extension author usage)

IStreamingResponseFactory (new interface)
  └── registered as keyed service by channelId

TurnContext (modified)
  └── resolves factory by channelId, falls back to new StreamingResponse(this)

Registration helpers (new)
  └── .NET 8: keyed services
  └── Framework 4.8: StreamingResponseFactoryRegistry (dictionary)
```

`IStreamingResponse` is **unchanged**. All existing app code compiles and runs without modification.

---

## Component Designs

### 1. `StreamingResponseBase`

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs`
**Visibility:** `public abstract`

Owns the shared infrastructure: text buffer, interval timer, stream state, and sequence tracking. Extension authors subclass this and implement the abstract hooks.

```csharp
public abstract class StreamingResponseBase : IStreamingResponse
{
    // Shared state
    private readonly StringBuilder _buffer = new();
    private Timer? _timer;
    private int _sequenceNumber;
    private bool _streamStarted;
    private bool _streamEnded;
    private readonly object _lock = new();

    // Configurable
    public int Interval { get; set; }           // ms between sends
    public int EndStreamTimeout { get; set; }   // default 120000 (2 min)

    // Must be set by subclass
    public abstract bool IsStreamingChannel { get; }

    // IStreamingResponse — implemented here (shared behavior)
    public string StreamId { get; protected set; }
    public string Message { get; protected set; }
    public void QueueTextChunk(string text) { /* adds to buffer, starts timer */ }
    public Task QueueInformativeUpdateAsync(string text, CancellationToken ct = default)
        /* calls SendInformativeAsync if IsStreamingChannel, else no-op */
    public Task<StreamingResponseResult> EndStreamAsync(CancellationToken ct = default)
        /* drains buffer, stops timer, calls FinalizeStreamAsync */
    public bool IsStreamStarted() => _streamStarted;
    public int UpdatesSent() => _sequenceNumber;
    public Task ResetAsync(CancellationToken ct = default) { /* resets all state */ }

    // Abstract hooks — extension authors implement these
    protected abstract Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct);
    protected abstract Task SendInformativeAsync(string text, CancellationToken ct);
    protected abstract Task FinalizeStreamAsync(CancellationToken ct);
    protected abstract Task HandleSendErrorAsync(Exception ex, CancellationToken ct);

    // IStreamingResponse Teams-specific members — virtual no-op defaults
    // StreamingResponse overrides these with real Teams behavior
    // Other channel impls ignore them (no-op is correct behavior)
    public virtual IActivity? FinalMessage { get; set; }
    public virtual bool FeedbackLoopEnabled { get; set; }
    public virtual string FeedbackLoopType { get; set; } = "default";
    public virtual bool? EnableGeneratedByAILabel { get; set; }
    public virtual SensitivityUsageInfo? SensitivityLabel { get; set; }
    public virtual List<ClientCitation>? Citations => null;
    public virtual void AddCitation(ClientCitation citation) { }
    public virtual void AddCitation(Citation citation, int citationPosition) { }
    public virtual void AddCitations(IList<Citation> citations) { }
    public virtual void AddCitations(IList<ClientCitation> citations) { }
}
```

**Timer behavior (unchanged from current `StreamingResponse`):**
- Starts on first `QueueTextChunk` call
- Fires every `Interval` ms, calling `SendChunksAsync` with accumulated text
- Stops when `EndStreamAsync` drains the buffer

**Abstract hook responsibilities:**

| Hook | Responsibility |
|------|---------------|
| `SendChunksAsync` | Send buffered text as a streaming intermediate message. Channel-specific format. |
| `SendInformativeAsync` | Send a "thinking/working" update. No-op if channel doesn't support it. |
| `FinalizeStreamAsync` | Send the final message, call channel stop API, or no-op if no final message required. |
| `HandleSendErrorAsync` | Handle send failures — e.g., Teams fallback to non-streaming on `ContentStreamNotAllowed`. |

---

### 2. `StreamingResponse` Refactored

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` (existing)
**Change:** Extend `StreamingResponseBase` instead of implementing `IStreamingResponse` directly.

All existing behavior is preserved:
- Channel detection logic (`SetDefaults()`) moves to the constructor, sets `IsStreamingChannel` and `Interval`
- `SendChunksAsync` sends Typing activity with `StreamTypes.Streaming` entity
- `SendInformativeAsync` sends Typing activity with `StreamTypes.Informative` entity
- `FinalizeStreamAsync` builds and sends the final Message activity (with AI entity, citations, feedback loop, sensitivity label)
- `HandleSendErrorAsync` implements Teams fallback: catches `ContentStreamNotAllowed` (user cancelled) and `BadArgument`/`"streaming api is not enabled"` (channel fallback), sets `IsStreamingChannel = false`
- All Teams-specific property overrides (`FeedbackLoopEnabled`, `Citations`, `EnableGeneratedByAILabel`, `SensitivityLabel`, `FinalMessage`) remain

No change to external behavior. Existing tests pass without modification.

---

### 3. `IStreamingResponseFactory`

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs`
**Visibility:** `public interface`

```csharp
public interface IStreamingResponseFactory
{
    IStreamingResponse Create(ITurnContext turnContext);
}
```

Extension authors implement this to produce their channel-specific `IStreamingResponse`.

---

### 4. Registration Helpers

**Location:** `src/libraries/Hosting/AspNetCore/` (extension methods on `IServiceCollection`)

```csharp
// Register a factory for a specific channelId
public static IServiceCollection AddStreamingResponseFactory<TFactory>(
    this IServiceCollection services, string channelId)
    where TFactory : class, IStreamingResponseFactory;

public static IServiceCollection AddStreamingResponseFactory(
    this IServiceCollection services, string channelId, IStreamingResponseFactory factory);
```

**Implementation strategy:**

- On **net8.0**: registers as `AddKeyedSingleton<IStreamingResponseFactory>(channelId, ...)` AND into `StreamingResponseFactoryRegistry`
- On **net48**: registers into `StreamingResponseFactoryRegistry` only

`StreamingResponseFactoryRegistry` is a singleton `Dictionary<string, IStreamingResponseFactory>` wrapper registered in DI, used as the Framework 4.8 resolution path.

**Slack extension example:**
```csharp
// In Slack extension setup
services.AddStreamingResponseFactory<SlackStreamingResponseFactory>("slack");
```

---

### 5. `TurnContext` Changes

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs`
**Change:** Add optional `IServiceProvider` parameter; lazy factory resolution.

```csharp
// New overload — existing overloads unchanged
public TurnContext(IChannelAdapter adapter, IActivity activity, IServiceProvider? services)

// StreamingResponse property becomes lazy
public IStreamingResponse StreamingResponse =>
    _streamingResponse ??= ResolveStreamingResponse();

private IStreamingResponse ResolveStreamingResponse()
{
    var channelId = Activity.ChannelId;

#if NET8_0_OR_GREATER
    if (_services?.GetKeyedService<IStreamingResponseFactory>(channelId) is { } kf)
        return kf.Create(this);
#endif

    var registry = _services?.GetService<StreamingResponseFactoryRegistry>();
    if (registry?.TryGet(channelId, out var rf) == true)
        return rf!.Create(this);

    return new StreamingResponse(this); // existing default
}
```

Adapters that have `IServiceProvider` (e.g., `CloudAdapter`) pass it when constructing `TurnContext`. Adapters using the old constructor get the default `StreamingResponse` — zero behavior change.

---

## Extension Author Example: Slack

**`SlackStreamingResponse`** (in `Microsoft.Agents.Extensions.Slack`):

```csharp
internal class SlackStreamingResponse : StreamingResponseBase
{
    private readonly ITurnContext _turnContext;
    private readonly Queue<object> _typedChunks = new();

    public SlackStreamingResponse(ITurnContext turnContext)
    {
        _turnContext = turnContext;
        Interval = 200;
    }

    public override bool IsStreamingChannel => true;

    protected override async Task SendChunksAsync(string text, int seq, CancellationToken ct)
    {
        while (_typedChunks.TryDequeue(out var chunk))
            await SlackClient.SendAsync(_turnContext, chunk, ct);
        if (!string.IsNullOrEmpty(text))
            await SlackClient.SendAsync(_turnContext, new MarkdownTextChunk(text), ct);
    }

    protected override Task SendInformativeAsync(string text, CancellationToken ct)
        => SlackClient.SendAsync(_turnContext,
               new TaskUpdateChunk(title: text, status: SlackTaskStatus.InProgress), ct);

    protected override Task FinalizeStreamAsync(CancellationToken ct)
        => SlackClient.StopAsync(_turnContext, ct); // no final Activity required

    protected override Task HandleSendErrorAsync(Exception ex, CancellationToken ct)
        => SlackClient.SendAsync(_turnContext,
               new TaskUpdateChunk(title: "Error", status: SlackTaskStatus.Error), ct);

    // Internal: used by Slack-specific extension methods
    internal void QueueTypedChunk(object chunk) => _typedChunks.Enqueue(chunk);
}
```

**Extension methods for Slack-specific typed chunks** (optional, channel-specific):

```csharp
public static class SlackStreamingExtensions
{
    public static void QueueTaskUpdate(this IStreamingResponse response, TaskUpdateChunk chunk)
    {
        if (response is SlackStreamingResponse slack)
            slack.QueueTypedChunk(chunk);
        // silently ignored on non-Slack channels
    }
}
```

**App developer code — no changes required:**

```csharp
// Works on any channel, including Slack
await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working...", ct);
turnContext.StreamingResponse.QueueTextChunk("Hello world");
await turnContext.StreamingResponse.EndStreamAsync(ct);

// Optional Slack-specific typed chunk (only has effect on Slack)
turnContext.StreamingResponse.QueueTaskUpdate(new TaskUpdateChunk(...));
```

---

## Backward Compatibility

| Concern | Impact |
|---------|--------|
| `IStreamingResponse` interface | No changes |
| Existing `StreamingResponse` behavior | Preserved exactly — all current tests pass |
| `TurnContext` existing constructors | Unchanged — new overload is additive |
| App developer code | No changes required |
| Existing adapter code | No changes required (uses old TurnContext constructor, gets default StreamingResponse) |

---

## File Changes Summary

| File | Change |
|------|--------|
| `Microsoft.Agents.Builder/StreamingResponseBase.cs` | New — abstract base class |
| `Microsoft.Agents.Builder/StreamingResponse.cs` | Refactor to extend `StreamingResponseBase` |
| `Microsoft.Agents.Builder/IStreamingResponseFactory.cs` | New — factory interface |
| `Microsoft.Agents.Builder/TurnContext.cs` | Add IServiceProvider overload, lazy resolution |
| `Microsoft.Agents.Hosting.AspNetCore/` | New registration extension methods |
| `Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs` | New — Framework 4.8 compat dictionary |
| `Microsoft.Agents.Builder.Tests/StreamingResponseTests.cs` | Add tests for factory resolution |
| `Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs` | New — base class tests |
