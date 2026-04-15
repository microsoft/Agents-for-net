# Enable Agentic Streaming Design

## Problem

In `StreamingResponse.SetDefaults()`, Teams agentic requests have streaming hardcoded off:

```csharp
if (turnContext.Activity.IsAgenticRequest())
{
    // Agentic requests do not support streaming responses at this time.
    IsStreamingChannel = false;
}
```

This prevents streaming responses for all Teams agentic (skill) scenarios. We need an opt-in flag so developers can re-enable streaming for agentic requests when their scenario supports it.

## Approach

Post-construction override (Approach A). Follow the `StartTypingTimer` precedent:

- Add a config-driven `bool` property to `AgentApplicationOptions`
- In `AgentApplication.OnTurnAsync()`, check the flag and re-enable streaming on the already-constructed `StreamingResponse` via an internal method
- No public API changes to `IStreamingResponse`

This works because `StreamingResponse` doesn't actually start streaming until `QueueTextChunk`/`QueueInformativeUpdateAsync` is called — well after `OnTurnAsync()` has a chance to flip the flag.

## Files to Change

### 1. `AgentApplicationOptions.cs`

Add property:

```csharp
/// <summary>
/// Optional. If true, streaming responses will be enabled for agentic (skill) requests
/// on channels that support streaming (e.g., Teams). Defaults to false.
/// </summary>
public bool EnableAgenticStreaming { get; set; } = false;
```

Add config binding in the `IConfiguration` constructor:

```csharp
EnableAgenticStreaming = section.GetValue<bool>(nameof(EnableAgenticStreaming), false);
```

### 2. `StreamingResponse.cs`

Add internal method with guards:

```csharp
internal void EnableAgenticStreaming()
{
    if (IsStreamStarted() || _ended)
    {
        return; // no-op if streaming already in progress or ended
    }

    if (_isTeamsChannel)
    {
        Interval = 1000;
        IsStreamingChannel = true;
    }
}
```

Key design decisions:
- **Teams-only**: The current suppression is Teams-specific in `SetDefaults()`. Other channels aren't affected.
- **Guarded**: No-op if streaming has started or ended, preventing mid-stream mutation.
- **Internal**: Only callable from within the assembly, not exposed on `IStreamingResponse`.

### 3. `AgentApplication.cs`

In `OnTurnAsync()`, **before** `StartTypingTimer`, add:

```csharp
// Enable streaming for agentic requests if configured
if (Options.EnableAgenticStreaming
    && AgenticAuthorization.IsAgenticRequest(turnContext)
    && turnContext.StreamingResponse is StreamingResponse streamingResponse)
{
    streamingResponse.EnableAgenticStreaming();
}
```

Placement before the typing timer establishes final turn behavior as early as possible.

### 4. `AgentApplicationBuilder.cs`

Add fluent setter:

```csharp
/// <summary>
/// Configures streaming responses for agentic (skill) requests.
/// Default state is false (streaming disabled for agentic requests).
/// </summary>
/// <param name="enableAgenticStreaming">Whether to enable streaming for agentic requests.</param>
/// <returns>The ApplicationBuilder instance.</returns>
public AgentApplicationBuilder SetEnableAgenticStreaming(bool enableAgenticStreaming)
{
    Options.EnableAgenticStreaming = enableAgenticStreaming;
    return this;
}
```

### 5. Tests

Cover:
- Default value is `false`
- Config binding reads `EnableAgenticStreaming`
- Teams + agentic + flag `false` → `IsStreamingChannel == false` (existing behavior preserved)
- Teams + agentic + flag `true` → `IsStreamingChannel == true`, `Interval == 1000`
- Non-agentic Teams requests unchanged regardless of flag
- Guard: calling `EnableAgenticStreaming()` after stream started is a no-op

## Configuration Example

```json
{
  "AgentApplication": {
    "EnableAgenticStreaming": true,
    "StartTypingTimer": true
  }
}
```

## Backward Compatibility

- Default `false` — no behavior change for existing agents
- No public API additions to `IStreamingResponse`
- Existing runtime fallback (Teams rejecting streaming with "streaming api is not enabled") still works and disables streaming for the turn
