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
- Support both .NET 8 (keyed services) and .NET Framework 4.8 / netstandard2.0 (dictionary registry)

## Non-Goals

- App developers configuring streaming behavior (extension authors only)
- Changes to `IStreamingResponse` interface contract
- New streaming features for existing Teams/WebChat/DirectLine channels

---

## Architecture Overview

```
StreamingResponseBase (abstract, new)
  └── StreamingResponse       ← existing class, refactored to extend base
  └── SlackStreamingResponse  ← in Slack extension (example of extension author usage)

IStreamingResponseFactory (new interface)
  └── registered as keyed service by channelId (net8) or via registry (netstandard2.0)

TurnContext (modified)
  └── _streamingResponse field: no longer readonly, no longer eagerly assigned
  └── SetStreamingResponse() — internal, called by adapter after construction

ChannelAdapter / ChannelServiceAdapterBase (modified)
  └── resolves IStreamingResponseFactory, injects into TurnContext before RunPipelineAsync

A2AAdapter — not modified (see A2AAdapter section below)

Registration helpers (new, in Microsoft.Agents.Builder)
  └── requires Microsoft.Extensions.DependencyInjection (MEDI) — see constraint below
  └── net8.0:         AddKeyedSingleton<IStreamingResponseFactory>(channelId)
  └── netstandard2.0: StreamingResponseFactoryRegistry (dictionary, built lazily from registrations)
```

`IStreamingResponse` is **unchanged**. All existing app code compiles and runs without modification.

---

## Optional Channel Features on `IStreamingResponse`

`IStreamingResponse` contains members beyond the core streaming API. Some are general optional features that any channel may implement; others are currently Teams-specific. Since `IStreamingResponse` is unchanged, all implementations must satisfy these members. `StreamingResponseBase` provides virtual no-op defaults.

**General optional features** (any channel can implement — not Teams-specific):

```csharp
bool FeedbackLoopEnabled { get; set; }
string FeedbackLoopType { get; set; }
List<ClientCitation>? Citations { get; }
void AddCitation(...);
void AddCitations(...);
```

For example, a Slack extension could implement `FeedbackLoopEnabled` to trigger Slack reactions/thumbs-up UI. Non-implementing channels get the virtual no-op default — `Citations` returns an empty list, feedback loop properties are ignored.

**Teams-specific members** (no meaningful equivalent on other channels):

```csharp
IActivity? FinalMessage { get; set; }
bool? EnableGeneratedByAILabel { get; set; }
SensitivityUsageInfo? SensitivityLabel { get; set; }
```

`StreamingResponseBase` provides virtual no-op defaults for all of these. **This is an accepted trade-off.** A future version may split the interface, but that is a separate breaking-change discussion.

---

## Component Designs

### 1. `StreamingResponseBase`

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs`
**Visibility:** `public abstract`

Owns shared infrastructure: text buffer, interval timer, stream state, sequence tracking.

#### Internal Queue Model

The current closure-based queue (`List<Func<IActivity>>`) is replaced by a plain text `StringBuilder`. The timer drains accumulated text and passes it to `SendChunksAsync`. Informative updates are sent immediately on the caller's thread via `SendInformativeAsync` — they do not go through the text buffer.

`StreamingResponse`'s `SendChunksAsync` override constructs the Typing activity from the received text string — no closure needed.

#### `IsStreamingChannel`

Changed from abstract get-only to a concrete property with `protected set`. The `protected set` is not visible through `IStreamingResponse { get; }` — this is valid C# and satisfies the interface contract.

```csharp
public bool IsStreamingChannel { get; protected set; }
```

Subclasses assign in their constructors. `StreamingResponse` no longer redeclares this property — it is inherited from the base and set in `SetDefaults()`. The base class sets it to `false` when applying `FallbackToNonStreaming`.

#### `Citations`

Declared with a `protected set` in the base class so that `StreamingResponse`'s internal `Citations ??= []` assignment patterns compile:

```csharp
public virtual List<ClientCitation>? Citations { get; protected set; } = [];
```

`StreamingResponse` overrides this and re-declares with the same `protected set`, preserving the existing behavior.

#### `_streamStarted` and `IsStreamStarted()`

The current `StreamingResponse.IsStreamStarted()` returns `_timer != null`. The base class replaces this with an explicit `_streamStarted` boolean, set to `true` when the timer is first created (on first `QueueTextChunk` call). This is semantically equivalent — `IsStreamStarted()` returns `_streamStarted`.

#### `EndStreamAsync` Threading

`EndStreamAsync` blocks the calling thread via `WaitHandle.WaitOne` to drain the buffer. This is **preserved**. `FinalizeStreamAsync` is called from this blocked thread context.

#### API

```csharp
public abstract class StreamingResponseBase : IStreamingResponse
{
    private readonly StringBuilder _buffer = new();
    private Timer? _timer;
    private int _sequenceNumber;
    private bool _streamStarted;   // set true on first QueueTextChunk; IsStreamStarted() returns this
    private bool _streamEnded;
    private readonly object _lock = new();

    public int Interval { get; set; }
    public int EndStreamTimeout { get; set; }   // default 120000ms

    public bool IsStreamingChannel { get; protected set; }

    public string StreamId { get; protected set; }
    public string Message { get; protected set; }

    public void QueueTextChunk(string text);
    public Task QueueInformativeUpdateAsync(string text, CancellationToken ct = default);
    public Task<StreamingResponseResult> EndStreamAsync(CancellationToken ct = default);
    public bool IsStreamStarted() => _streamStarted;
    public int UpdatesSent() => _sequenceNumber;

    public virtual Task ResetAsync(CancellationToken ct = default);
    // Resets: _buffer, _timer, _sequenceNumber, _streamStarted, _streamEnded, StreamId, Message
    // Does NOT reset IsStreamingChannel — subclass responsibility
    // Subclasses must call base.ResetAsync() then reset their own state

    // Abstract hooks
    protected abstract Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct);
    protected abstract Task SendInformativeAsync(string text, CancellationToken ct);
    protected abstract Task FinalizeStreamAsync(CancellationToken ct);
    protected abstract Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct);

    // General optional features — virtual no-op defaults (any channel may override)
    public virtual bool FeedbackLoopEnabled { get; set; }
    public virtual string FeedbackLoopType { get; set; } = "default";
    public virtual List<ClientCitation>? Citations { get; protected set; } = [];
    public virtual void AddCitation(ClientCitation citation) { }
    public virtual void AddCitation(Citation citation, int citationPosition) { }
    public virtual void AddCitations(IList<Citation> citations) { }
    public virtual void AddCitations(IList<ClientCitation> citations) { }

    // Teams-specific — virtual no-op defaults
    public virtual IActivity? FinalMessage { get; set; }
    public virtual bool? EnableGeneratedByAILabel { get; set; }
    public virtual SensitivityUsageInfo? SensitivityLabel { get; set; }
}
```

#### `StreamErrorAction`

```csharp
public enum StreamErrorAction
{
    Continue,                // ignore error, keep streaming
    FallbackToNonStreaming,  // base sets IsStreamingChannel = false, stops timer; FinalizeStreamAsync still called
    Cancel,                  // base stops stream, returns StreamingResponseResult.UserCancelled
}
```

For `FallbackToNonStreaming`: base sets `IsStreamingChannel = false` (valid via `protected set`), stops the timer. `FinalizeStreamAsync` is still called — `StreamingResponse` checks `IsStreamingChannel` there to decide whether to send a plain vs. streamed final message.

#### Abstract Hook Responsibilities

| Hook | Called from | Responsibility |
|------|-------------|----------------|
| `SendChunksAsync(text, seq, ct)` | Timer thread | Send buffered text as channel-specific intermediate message |
| `SendInformativeAsync(text, ct)` | Caller thread | Send "thinking" update; implement as no-op if unsupported |
| `FinalizeStreamAsync(ct)` | Blocked EndStreamAsync thread | Send final message / stop API / no-op |
| `HandleSendErrorAsync(ex, ct)` | Timer thread | Interpret error, return action for base to apply |

---

### 2. `StreamingResponse` Refactored

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs`
**Change:** Extend `StreamingResponseBase` instead of implementing `IStreamingResponse` directly.

Key implementation points:

- `IsStreamingChannel` is **no longer redeclared** — it is inherited and set in `SetDefaults()`
- `Citations` is **overridden** with `protected set` to preserve internal assignment patterns
- `FinalizeStreamAsync` checks `IsStreamingChannel` to handle fallback mode (if `false`, sends plain Message without streaming entities)
- `HandleSendErrorAsync` returns `StreamErrorAction.Cancel` for `ContentStreamNotAllowed`, `StreamErrorAction.FallbackToNonStreaming` for "streaming api is not enabled", `StreamErrorAction.Continue` otherwise (logged)
- `ResetAsync` calls `base.ResetAsync()` then re-runs `SetDefaults()` (resetting `IsStreamingChannel` and `Interval`) and resets Teams-specific fields

All existing tests pass without modification.

---

### 3. `IStreamingResponseFactory`

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs`

```csharp
public interface IStreamingResponseFactory
{
    IStreamingResponse Create(ITurnContext turnContext);
}
```

---

### 4. `StreamingResponseFactoryRegistry`

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs`

```csharp
public class StreamingResponseFactoryRegistry
{
    private readonly Dictionary<string, IStreamingResponseFactory> _factories = new();

    public void Register(string channelId, IStreamingResponseFactory factory)
        => _factories[channelId] = factory;

    public bool TryGet(string channelId, out IStreamingResponseFactory? factory)
        => _factories.TryGetValue(channelId, out factory);
}
```

---

### 5. Registration Helpers

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs`

**MEDI requirement:** The netstandard2.0 registration path uses `GetServices<StreamingResponseFactoryRegistration>()` returning multiple instances of the same concrete type. This works correctly with `Microsoft.Extensions.DependencyInjection`. Third-party containers (Autofac, Unity, etc.) may not support this pattern. **This design requires MEDI** for the netstandard2.0 path. Document this constraint in the XML doc on the extension method.

#### Pattern

An internal record accumulates per-channel registrations. The registry singleton is built lazily from all accumulated entries at first resolve:

```csharp
internal record StreamingResponseFactoryRegistration(
    string ChannelId,
    Func<IServiceProvider, IStreamingResponseFactory> Factory);
```

```csharp
public static IServiceCollection AddStreamingResponseFactory<TFactory>(
    this IServiceCollection services, string channelId)
    where TFactory : class, IStreamingResponseFactory
{
    services.TryAddTransient<TFactory>();

    // Each call adds a new registration entry — GetServices<T>() returns all of them
    services.AddSingleton(new StreamingResponseFactoryRegistration(
        channelId,
        sp => sp.GetRequiredService<TFactory>()));

    // TryAddSingleton: registered once; factory delegate closes over IEnumerable from DI
    services.TryAddSingleton<StreamingResponseFactoryRegistry>(sp =>
    {
        var registry = new StreamingResponseFactoryRegistry();
        foreach (var reg in sp.GetServices<StreamingResponseFactoryRegistration>())
            registry.Register(reg.ChannelId, reg.Factory(sp));
        return registry;
    });

#if NET8_0_OR_GREATER
    services.AddKeyedSingleton<IStreamingResponseFactory, TFactory>(channelId);
#endif

    return services;
}

public static IServiceCollection AddStreamingResponseFactory(
    this IServiceCollection services, string channelId, IStreamingResponseFactory factory)
{
    services.AddSingleton(new StreamingResponseFactoryRegistration(channelId, _ => factory));
    services.TryAddSingleton<StreamingResponseFactoryRegistry>(sp =>
    {
        var registry = new StreamingResponseFactoryRegistry();
        foreach (var reg in sp.GetServices<StreamingResponseFactoryRegistration>())
            registry.Register(reg.ChannelId, reg.Factory(sp));
        return registry;
    });
#if NET8_0_OR_GREATER
    services.AddKeyedSingleton<IStreamingResponseFactory>(channelId, factory);
#endif
    return services;
}
```

---

### 6. `TurnContext` Changes

**Location:** `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs`

#### Field Change

```csharp
// Before (both constructors eagerly assigned this):
private readonly IStreamingResponse _streamingResponse;

// After (nullable, non-readonly, lazily assigned):
private IStreamingResponse? _streamingResponse;
```

Both existing constructors are updated to **remove** the eager `_streamingResponse = new StreamingResponse(this)` assignment. The constructor signatures are unchanged.

#### `SetStreamingResponse` (internal)

```csharp
internal void SetStreamingResponse(IStreamingResponse streamingResponse)
{
    if (_streamingResponse?.IsStreamStarted() == true)
        throw new InvalidOperationException(
            "Cannot set streaming response after the stream has started.");
    _streamingResponse = streamingResponse;
}
```

Guard: if the default `StreamingResponse` was already created and started (i.e., `QueueTextChunk` was called before `SetStreamingResponse`), throw. In normal adapter flow this never happens — `SetStreamingResponse` is called before `RunPipelineAsync`.

#### `StreamingResponse` Property

```csharp
public IStreamingResponse StreamingResponse =>
    _streamingResponse ??= new StreamingResponse(this);
```

#### Copy Constructor

```csharp
public TurnContext(ITurnContext context, IActivity activity)
{
    // ... existing init ...
    // Carry forward the streaming response from the source context if available
    _streamingResponse = (context as TurnContext)?._streamingResponse;
    // If source is null/other type: _streamingResponse = null, default created on first access
    // TypedTurnContext<T> delegates StreamingResponse to its inner ITurnContext — no change needed
}
```

#### Backward Compatibility Note

The constructor **bodies** change (eager assignment removed), but the constructor **signatures** are unchanged. Existing call sites compile and run identically. The only visible behavior change: if code accesses `StreamingResponse` after construction but before a turn handler runs (unusual), it now creates a default `StreamingResponse(this)` lazily rather than using one created at construction time. The result is identical.

---

### 7. Adapter Changes

**Location:** `ChannelAdapter.cs`, `ChannelServiceAdapterBase.cs`

```csharp
private IStreamingResponseFactory? ResolveFactory(string? channelId, IServiceProvider services)
{
    if (channelId is null) return null;
#if NET8_0_OR_GREATER
    if (services.GetKeyedService<IStreamingResponseFactory>(channelId) is { } kf)
        return kf;
#endif
    var registry = services.GetService<StreamingResponseFactoryRegistry>();
    if (registry?.TryGet(channelId, out var rf) == true)
        return rf;
    return null;
}
```

```csharp
// In ProcessActivityAsync, ProcessProactiveAsync, etc.:
var turnContext = new TurnContext(this, activity, claimsIdentity);

var factory = ResolveFactory(activity.ChannelId, _services);
if (factory is not null)
    turnContext.SetStreamingResponse(factory.Create(turnContext));

await RunPipelineAsync(turnContext, handler, cancellationToken);
```

**Adapters without `IServiceProvider`**: No change — `SetStreamingResponse` is never called, default `StreamingResponse` created on first access.

---

### 8. `A2AAdapter` — Not Modified

`A2AAdapter` does not construct `TurnContext` directly in its turn-handling path. It queues activities to a background service that processes them. The `ResolveFactory + SetStreamingResponse` pattern does not apply.

**Important distinction:** `DeliveryModes.Stream` in A2A is a **transport-level** concern — it controls how activity responses are delivered back to the parent agent (streamed HTTP vs. async). It is entirely separate from `IStreamingResponse`, which is a **turn-handler-level** tool that lets an agent incrementally produce content. These two mechanisms are orthogonal; `A2AAdapter` already handles the `DeliveryModes.Stream` transport correctly by converting outbound activities to A2A streaming payloads.

**Decision:** `A2AAdapter` is explicitly out of scope for this change. If `IStreamingResponse`-based extensibility is needed for A2A, it is a separate future work item.

---

## Extension Author Example: Slack

The Slack extension uses `SlackStream` from `Microsoft.Agents.Extensions.Slack.Api`, which wraps the Slack `chat.startStream`, `chat.appendStream`, and `chat.stopStream` API methods. `SlackStream` is created from `SlackApi` (a DI service) and the channel/thread information from `SlackChannelData` (deserialized from `Activity.ChannelData`).

```csharp
// Inside Microsoft.Agents.Extensions.Slack
internal class SlackStreamingResponse : StreamingResponseBase
{
    private readonly ITurnContext _turnContext;
    private readonly SlackApi _slackApi;
    private SlackStream? _stream;

    public SlackStreamingResponse(ITurnContext turnContext, SlackApi slackApi)
    {
        _turnContext = turnContext;
        _slackApi = slackApi;
        IsStreamingChannel = true;
        Interval = 200;
    }

    // Lazily starts the Slack stream on first send
    private async Task<SlackStream> EnsureStreamAsync(CancellationToken ct)
    {
        if (_stream == null)
        {
            var channelData = ProtocolJsonSerializer.To<SlackChannelData>(
                _turnContext.Activity.ChannelData);
            _stream = await new SlackStream(
                _slackApi,
                channelData.Channel,
                channelData.ThreadTs,
                channelData.ApiToken).StartAsync();
        }
        return _stream;
    }

    protected override async Task SendChunksAsync(string text, int seq, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(text)) return;
        var stream = await EnsureStreamAsync(ct);
        await stream.AppendAsync(new MarkdownTextChunk(text));
    }

    protected override async Task SendInformativeAsync(string text, CancellationToken ct)
    {
        var stream = await EnsureStreamAsync(ct);
        await stream.AppendAsync(new TaskUpdateChunk("thinking", text, SlackTaskStatus.InProgress));
    }

    protected override async Task FinalizeStreamAsync(CancellationToken ct)
    {
        // No final Activity required — Slack stream is closed with StopAsync
        if (_stream != null)
            await _stream.StopAsync();
    }

    protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct)
        => Task.FromResult(StreamErrorAction.Cancel);
}

// Factory — receives SlackApi via DI
internal class SlackStreamingResponseFactory : IStreamingResponseFactory
{
    private readonly SlackApi _slackApi;

    public SlackStreamingResponseFactory(SlackApi slackApi) => _slackApi = slackApi;

    public IStreamingResponse Create(ITurnContext ctx) =>
        new SlackStreamingResponse(ctx, _slackApi);
}

// Registration in Slack extension setup
services.AddStreamingResponseFactory<SlackStreamingResponseFactory>("slack");
```

**Extension methods for typed chunks** (for app developers who want Slack-specific content):

```csharp
public static class SlackStreamingExtensions
{
    public static void QueueBlocks(this IStreamingResponse response, BlocksChunk blocks)
    {
        if (response is SlackStreamingResponse slack)
            slack.QueueTypedChunk(blocks);
        // no-op on non-Slack channels
    }
}
```

**App developer code — unchanged for standard usage:**

```csharp
await turnContext.StreamingResponse.QueueInformativeUpdateAsync("Working...", ct);
turnContext.StreamingResponse.QueueTextChunk("Hello");
await turnContext.StreamingResponse.EndStreamAsync(ct);
```

---

## Backward Compatibility

| Concern | Impact |
|---------|--------|
| `IStreamingResponse` interface | No changes |
| Existing `StreamingResponse` behavior | Preserved — all current tests pass |
| `TurnContext` constructor signatures | Unchanged |
| `TurnContext` constructor bodies | Eager `_streamingResponse` assignment removed; lazy default created on first access — behavior identical |
| `TypedTurnContext<T>` | No changes — delegates to inner `ITurnContext` |
| App developer code | No changes required |
| `A2AAdapter` | Not modified — out of scope |
| Adapters without `IServiceProvider` | No changes — default `StreamingResponse` on first access |

---

## File Changes Summary

| File | Change |
|------|--------|
| `Microsoft.Agents.Builder/StreamingResponseBase.cs` | **New** — abstract base class |
| `Microsoft.Agents.Builder/StreamingResponse.cs` | **Refactor** — extend `StreamingResponseBase` |
| `Microsoft.Agents.Builder/IStreamingResponseFactory.cs` | **New** — factory interface |
| `Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs` | **New** — dictionary registry |
| `Microsoft.Agents.Builder/StreamingResponseFactoryRegistration.cs` | **New** — internal registration record |
| `Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs` | **New** — registration helpers |
| `Microsoft.Agents.Builder/TurnContext.cs` | **Modify** — field nullable/non-readonly, eager assignment removed from constructors, `SetStreamingResponse`, copy-constructor fix |
| `Microsoft.Agents.Builder/ChannelAdapter.cs` | **Modify** — `ResolveFactory`, call `SetStreamingResponse` |
| `Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs` | **Modify** — same |
| `Microsoft.Agents.Builder.Tests/StreamingResponseTests.cs` | **Update** — add factory resolution tests |
| `Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs` | **New** — base class contract tests |
