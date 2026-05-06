# StreamingResponse Extensibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract shared streaming infrastructure into `StreamingResponseBase` and add a keyed factory mechanism so channel extension authors can plug in custom `IStreamingResponse` implementations per channel.

**Architecture:** A new abstract `StreamingResponseBase` owns the text buffer, interval timer, and stream state, exposing abstract hooks (`SendChunksAsync`, `SendInformativeAsync`, `FinalizeStreamAsync`, `HandleSendErrorAsync`) for channel-specific behavior. The existing `StreamingResponse` is refactored to extend this base. `IStreamingResponseFactory` is registered as a keyed DI service by `channelId`; `TurnContext` lazily resolves the factory (falling back to `new StreamingResponse`) before the turn handler runs. A protected virtual hook on `ChannelAdapter` lets concrete adapters supply the factory.

**Tech Stack:** C# 12, .NET 8 / netstandard2.0, xUnit 2.9, Moq 4.20, `Microsoft.Extensions.DependencyInjection` (MEDI required for registration path)

**Spec:** `docs/superpowers/specs/2026-05-05-streaming-response-extensibility-design.md`

---

## File Map

| File | Status | Responsibility |
|------|--------|----------------|
| `src/libraries/Builder/Microsoft.Agents.Builder/StreamErrorAction.cs` | Create | `StreamErrorAction` enum |
| `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs` | Create | Factory interface |
| `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistration.cs` | Create | Internal registration record |
| `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs` | Create | Dictionary registry (netstandard2.0 compat) |
| `src/libraries/Builder/Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs` | Create | `AddStreamingResponseFactory` DI helpers |
| `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs` | Create | Abstract base: buffer, timer, hooks |
| `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs` | Modify | Extend `StreamingResponseBase` instead of `IStreamingResponse` |
| `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs` | Modify | Lazy init, `SetStreamingResponse`, copy-ctor fix |
| `src/libraries/Builder/Microsoft.Agents.Builder/ChannelAdapter.cs` | Modify | `Services` property + factory injection hook |
| `src/libraries/Builder/Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs` | Modify | Factory injection in overridden `ProcessActivityAsync` and `ProcessProactiveAsync` (production path used by `CloudAdapter`) |
| `src/libraries/Hosting/AspNetCore/CloudAdapter.cs` | Modify | Accept `IServiceProvider` in constructor to populate inherited `Services` property |
| `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs` | Create | Base class contract tests |
| `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs` | Create | Registry + DI registration tests |
| `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseTests.cs` | Modify | Verify no regression, add factory resolution test |

---

## Task 1: Supporting Types

**Files:**
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/StreamErrorAction.cs`
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs`
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistration.cs`
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs`
- Test: `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs`

- [ ] **Step 1: Write failing tests for `StreamingResponseFactoryRegistry`**

```csharp
// src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Moq;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseFactoryTests
    {
        [Fact]
        public void Registry_Register_And_TryGet_ReturnsFactory()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory);

            Assert.True(registry.TryGet("slack", out var result));
            Assert.Same(factory, result);
        }

        [Fact]
        public void Registry_TryGet_UnknownChannel_ReturnsFalse()
        {
            var registry = new StreamingResponseFactoryRegistry();

            Assert.False(registry.TryGet("unknown", out var result));
            Assert.Null(result);
        }

        [Fact]
        public void Registry_Register_OverwritesPrevious()
        {
            var registry = new StreamingResponseFactoryRegistry();
            var factory1 = new Mock<IStreamingResponseFactory>().Object;
            var factory2 = new Mock<IStreamingResponseFactory>().Object;

            registry.Register("slack", factory1);
            registry.Register("slack", factory2);

            registry.TryGet("slack", out var result);
            Assert.Same(factory2, result);
        }
    }
}
```

- [ ] **Step 2: Run test to confirm FAIL**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseFactoryTests" --no-build
```

Expected: compilation error (types don't exist yet).

- [ ] **Step 3: Create `StreamErrorAction.cs`**

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/StreamErrorAction.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Indicates what action StreamingResponseBase should take after HandleSendErrorAsync returns.
    /// </summary>
    public enum StreamErrorAction
    {
        /// <summary>Ignore the error and keep streaming.</summary>
        Continue,

        /// <summary>
        /// Set IsStreamingChannel = false, stop the timer. FinalizeStreamAsync is still called.
        /// Useful for channels that return "streaming not supported" errors at runtime.
        /// </summary>
        FallbackToNonStreaming,

        /// <summary>Stop the stream and return StreamingResponseResult.UserCancelled.</summary>
        Cancel,
    }
}
```

- [ ] **Step 4: Create `IStreamingResponseFactory.cs`**

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Creates an <see cref="IStreamingResponse"/> implementation for a specific channel.
    /// Register implementations via <c>services.AddStreamingResponseFactory&lt;TFactory&gt;("channelId")</c>.
    /// </summary>
    public interface IStreamingResponseFactory
    {
        /// <summary>Creates a streaming response for the given turn context.</summary>
        IStreamingResponse Create(ITurnContext turnContext);
    }
}
```

- [ ] **Step 5: Create `StreamingResponseFactoryRegistration.cs`**

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistration.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Internal record used to accumulate per-channel factory registrations for
    /// building the <see cref="StreamingResponseFactoryRegistry"/> at first resolve.
    /// </summary>
    internal record StreamingResponseFactoryRegistration(
        string ChannelId,
        Func<IServiceProvider, IStreamingResponseFactory> Factory);
}
```

- [ ] **Step 6: Create `StreamingResponseFactoryRegistry.cs`**

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Dictionary-based registry of <see cref="IStreamingResponseFactory"/> instances keyed by channelId.
    /// Used as the netstandard2.0-compatible resolution path alongside keyed DI services on .NET 8+.
    /// Requires Microsoft.Extensions.DependencyInjection — third-party containers are not supported.
    /// </summary>
    public class StreamingResponseFactoryRegistry
    {
        private readonly Dictionary<string, IStreamingResponseFactory> _factories = new();

        /// <summary>Registers a factory for a channel, overwriting any previous registration.</summary>
        public void Register(string channelId, IStreamingResponseFactory factory)
            => _factories[channelId] = factory;

        /// <summary>Tries to find a factory for the given channelId.</summary>
        public bool TryGet(string channelId, out IStreamingResponseFactory? factory)
            => _factories.TryGetValue(channelId, out factory);
    }
}
```

- [ ] **Step 7: Build to confirm compilation**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

Expected: 0 errors.

- [ ] **Step 8: Run tests to confirm PASS**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseFactoryTests" --no-build
```

Expected: 3 tests pass.

- [ ] **Step 9: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/StreamErrorAction.cs \
        src/libraries/Builder/Microsoft.Agents.Builder/IStreamingResponseFactory.cs \
        src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistration.cs \
        src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseFactoryRegistry.cs \
        src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs
git commit -m "feat: add StreamErrorAction, IStreamingResponseFactory, StreamingResponseFactoryRegistry"
```

---

## Task 2: `StreamingResponseBase`

**Files:**
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs`
- Create: `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs`

Since `StreamingResponseBase` is abstract, tests use a minimal concrete test subclass defined inside the test file.

- [ ] **Step 1: Write failing tests**

```csharp
// src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs
using Microsoft.Agents.Builder;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class StreamingResponseBaseTests
    {
        // ── Minimal test subclass ──────────────────────────────────────────────
        private class TestStreamingResponse : StreamingResponseBase
        {
            public List<(string Text, int Seq)> SentChunks = new();
            public List<string> SentInformatives = new();
            public bool Finalized;
            public StreamErrorAction ErrorAction = StreamErrorAction.Continue;
            public Exception? ThrownOnNextSend;

            protected override Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct)
            {
                if (ThrownOnNextSend is not null)
                {
                    var ex = ThrownOnNextSend;
                    ThrownOnNextSend = null;
                    throw ex;
                }
                SentChunks.Add((bufferedText, sequenceNumber));
                return Task.CompletedTask;
            }

            protected override Task SendInformativeAsync(string text, CancellationToken ct)
            {
                SentInformatives.Add(text);
                return Task.CompletedTask;
            }

            protected override Task FinalizeStreamAsync(CancellationToken ct)
            {
                Finalized = true;
                return Task.CompletedTask;
            }

            protected override Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct)
                => Task.FromResult(ErrorAction);
        }
        // ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void QueueTextChunk_AccumulatesInMessage()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 5000 };
            sr.QueueTextChunk("Hello");
            sr.QueueTextChunk(" World");
            Assert.Equal("Hello World", sr.Message);
        }

        [Fact]
        public void QueueTextChunk_AfterStreamEnded_Throws()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false, Interval = 100 };
            sr.QueueTextChunk("text");
            sr.EndStreamAsync().Wait();
            Assert.Throws<AggregateException>(() => sr.QueueTextChunk("more"));
        }

        [Fact]
        public void QueueTextChunk_Empty_IsIgnored()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            sr.QueueTextChunk("");
            sr.QueueTextChunk(null);
            Assert.Equal("", sr.Message);
        }

        [Fact]
        public async Task QueueInformativeUpdateAsync_OnStreamingChannel_CallsSendInformativeAsync()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 5000 };
            await sr.QueueInformativeUpdateAsync("Thinking...");
            Assert.Single(sr.SentInformatives);
            Assert.Equal("Thinking...", sr.SentInformatives[0]);
        }

        [Fact]
        public async Task QueueInformativeUpdateAsync_OnNonStreamingChannel_IsNoOp()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            await sr.QueueInformativeUpdateAsync("Thinking...");
            Assert.Empty(sr.SentInformatives);
        }

        [Fact]
        public async Task EndStreamAsync_NonStreaming_CallsFinalize_AndReturnsSuccess()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            sr.QueueTextChunk("hello");
            var result = await sr.EndStreamAsync();
            Assert.True(sr.Finalized);
            Assert.Equal(StreamingResponseResult.Success, result);
        }

        [Fact]
        public async Task EndStreamAsync_WhenNothingQueued_ReturnsNotStarted()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 100 };
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.NotStarted, result);
        }

        [Fact]
        public async Task EndStreamAsync_CalledTwice_ReturnsAlreadyEnded()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            sr.QueueTextChunk("text");
            await sr.EndStreamAsync();
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.AlreadyEnded, result);
        }

        [Fact]
        public async Task EndStreamAsync_StreamingChannel_SendsChunksAndFinalizes()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 100 };
            sr.QueueTextChunk("hello");
            await Task.Delay(200); // let timer fire
            sr.QueueTextChunk(" world");
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.Success, result);
            Assert.True(sr.SentChunks.Count >= 1);
            Assert.True(sr.Finalized);
        }

        [Fact]
        public async Task ResetAsync_ClearsStateForReuse()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            sr.QueueTextChunk("text");
            await sr.EndStreamAsync();
            await sr.ResetAsync();

            Assert.Equal("", sr.Message);
            Assert.False(sr.IsStreamStarted());
            // Stream can be used again after reset
            sr.QueueTextChunk("new");
            Assert.Equal("new", sr.Message);
        }

        [Fact]
        public async Task HandleSendErrorAsync_FallbackToNonStreaming_DisablesStreaming()
        {
            var sr = new TestStreamingResponse
            {
                IsStreamingChannel = true,
                Interval = 100,
                ErrorAction = StreamErrorAction.FallbackToNonStreaming,
                ThrownOnNextSend = new Exception("streaming not supported")
            };
            sr.QueueTextChunk("text");
            var result = await sr.EndStreamAsync();
            Assert.False(sr.IsStreamingChannel);
            // Finalize is still called after fallback
            Assert.True(sr.Finalized);
        }

        [Fact]
        public async Task HandleSendErrorAsync_Cancel_ReturnsUserCancelled()
        {
            var sr = new TestStreamingResponse
            {
                IsStreamingChannel = true,
                Interval = 100,
                ErrorAction = StreamErrorAction.Cancel,
                ThrownOnNextSend = new Exception("user cancelled")
            };
            sr.QueueTextChunk("text");
            var result = await sr.EndStreamAsync();
            Assert.Equal(StreamingResponseResult.UserCancelled, result);
        }

        [Fact]
        public void IsStreamStarted_BeforeAnyChunk_ReturnsFalse()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 5000 };
            Assert.False(sr.IsStreamStarted());
        }

        [Fact]
        public void UpdatesSent_ReflectsChunksSent()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = true, Interval = 5000 };
            Assert.Equal(0, sr.UpdatesSent());
        }

        [Fact]
        public void TeamsSpecificMembers_AreNoOpsOnBase()
        {
            var sr = new TestStreamingResponse { IsStreamingChannel = false };
            // Should not throw; should silently no-op
            sr.AddCitation(new Microsoft.Agents.Core.Models.ClientCitation());
            Assert.Empty(sr.Citations!);
            sr.FeedbackLoopEnabled = true;  // no-op setter
            sr.EnableGeneratedByAILabel = true;  // no-op setter
        }
    }
}
```

- [ ] **Step 2: Run tests to confirm FAIL**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseBaseTests"
```

Expected: compilation failure (StreamingResponseBase doesn't exist yet).

- [ ] **Step 3: Implement `StreamingResponseBase.cs`**

Key notes before writing:
- Replace the closure-based queue (`List<Func<IActivity>>`) with a plain `StringBuilder _buffer` and `bool _messageUpdated` flag
- Keep `AutoResetEvent _queueEmpty` for `EndStreamAsync` wait
- Timer callback `OnTimerTick` (async void): snapshot text, call `SendChunksAsync`, restart timer or signal done
- `QueueInformativeUpdateAsync`: send directly via `SendInformativeAsync` if `IsStreamingChannel`, also call `StartStream()` so `IsStreamStarted()` returns true
- `EndStreamAsync` for non-streaming: skip wait, just call `FinalizeStreamAsync`
- `EndStreamAsync` for streaming: `_queueEmpty.WaitOne(EndStreamTimeout)`, then call `FinalizeStreamAsync`
- `HandleSendErrorAsync` returning `FallbackToNonStreaming`: set `IsStreamingChannel = false` (possible via `protected set`), call `FinalizeStreamAsync` before returning from `EndStreamAsync`
- `HandleSendErrorAsync` returning `Cancel`: set `_streamCancelled = true`, signal `_queueEmpty`

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Abstract base for streaming response implementations.
    /// Owns the text buffer, interval timer, and stream state.
    /// Extension authors subclass this and implement the abstract channel-specific hooks.
    /// </summary>
    public abstract class StreamingResponseBase : IStreamingResponse
    {
        public static readonly int DefaultEndStreamTimeout = (int)TimeSpan.FromMinutes(2).TotalMilliseconds;

        private readonly StringBuilder _buffer = new();
        private Timer? _timer;
        private bool _messageUpdated;
        private bool _streamStarted;
        private bool _streamEnded;
        private bool _streamCancelled;
        private bool _userCancelled;
        private readonly object _lock = new();
        private readonly AutoResetEvent _queueEmpty = new(false);

        /// <summary>Gets or sets the interval in ms between intermediate sends.</summary>
        public int Interval { get; set; } = 500;

        /// <summary>Gets or sets the timeout in ms for EndStreamAsync to wait for the buffer to drain.</summary>
        public int EndStreamTimeout { get; set; } = DefaultEndStreamTimeout;

        /// <summary>
        /// Whether the current channel supports intermediate streaming messages.
        /// Subclasses set this in their constructor. The base may set it to false on FallbackToNonStreaming.
        /// </summary>
        public bool IsStreamingChannel { get; protected set; }

        /// <summary>Gets the stream ID assigned after the first intermediate message is sent.</summary>
        public string StreamId { get; protected set; } = string.Empty;

        /// <summary>Gets the accumulated message text.</summary>
        public string Message { get; protected set; } = string.Empty;

        // ── IStreamingResponse: Teams-specific — virtual no-op defaults ───────

        /// <inheritdoc/>
        public virtual IActivity? FinalMessage { get; set; }

        /// <inheritdoc/>
        public virtual bool FeedbackLoopEnabled { get; set; }

        /// <inheritdoc/>
        public virtual string FeedbackLoopType { get; set; } = "default";

        /// <inheritdoc/>
        public virtual bool? EnableGeneratedByAILabel { get; set; }

        /// <inheritdoc/>
        public virtual SensitivityUsageInfo? SensitivityLabel { get; set; }

        /// <inheritdoc/>
        public virtual List<ClientCitation>? Citations { get; protected set; } = [];

        /// <inheritdoc/>
        public virtual void AddCitation(ClientCitation citation) { }

        /// <inheritdoc/>
        public virtual void AddCitation(Citation citation, int citationPosition) { }

        /// <inheritdoc/>
        public virtual void AddCitations(IList<Citation> citations) { }

        /// <inheritdoc/>
        public virtual void AddCitations(IList<ClientCitation> citations) { }

        // ── IStreamingResponse: shared behavior ───────────────────────────────

        /// <inheritdoc/>
        public virtual void QueueTextChunk(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            lock (_lock)
            {
                if (_streamEnded)
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);

                Message += text;
                _messageUpdated = true;
                StartStream(250);
            }
        }

        /// <inheritdoc/>
        public async Task QueueInformativeUpdateAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
                return;

            lock (_lock)
            {
                if (_streamEnded)
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.StreamingResponseEnded, null);
            }

            await SendInformativeAsync(text, cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                // Mark stream as started so IsStreamStarted() returns true and EndStreamAsync waits
                StartStream();
            }
        }

        /// <inheritdoc/>
        public async Task<StreamingResponseResult> EndStreamAsync(CancellationToken cancellationToken = default)
        {
            if (!IsStreamingChannel)
            {
                lock (_lock)
                {
                    if (_streamEnded)
                        return StreamingResponseResult.AlreadyEnded;
                    _streamEnded = true;
                }

                await FinalizeStreamAsync(cancellationToken).ConfigureAwait(false);
                return StreamingResponseResult.Success;
            }
            else
            {
                lock (_lock)
                {
                    if (_streamEnded)
                        return StreamingResponseResult.AlreadyEnded;
                    _streamEnded = true;

                    if (_streamCancelled)
                        return _userCancelled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;

                    if (!_streamStarted)
                        return StreamingResponseResult.NotStarted;
                }

                // Wait for timer to drain the buffer
                if (!_queueEmpty.WaitOne(EndStreamTimeout))
                    return StreamingResponseResult.Timeout;

                if (_streamCancelled)
                    return _userCancelled ? StreamingResponseResult.UserCancelled : StreamingResponseResult.Error;

                await FinalizeStreamAsync(cancellationToken).ConfigureAwait(false);
                return StreamingResponseResult.Success;
            }
        }

        /// <inheritdoc/>
        public bool IsStreamStarted() => _streamStarted;

        /// <inheritdoc/>
        public int UpdatesSent() => _sequenceNumber;

        private int _sequenceNumber;

        /// <summary>
        /// Resets shared base state. Subclasses must call base.ResetAsync() then reset their own state.
        /// Does NOT reset IsStreamingChannel — that is the subclass's responsibility.
        /// </summary>
        public virtual async Task ResetAsync(CancellationToken cancellationToken = default)
        {
            if (IsStreamStarted())
                await EndStreamAsync(cancellationToken).ConfigureAwait(false);

            lock (_lock)
            {
                StopStream();
                _streamEnded = false;
                _streamStarted = false;
                _streamCancelled = false;
                _userCancelled = false;
                _messageUpdated = false;
                _sequenceNumber = 0;
                Message = string.Empty;
                StreamId = string.Empty;
            }
        }

        // ── Abstract hooks ────────────────────────────────────────────────────

        /// <summary>
        /// Called by the timer thread with accumulated text. Send as a channel-specific intermediate message.
        /// </summary>
        protected abstract Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// Called on the caller's thread when QueueInformativeUpdateAsync is called on a streaming channel.
        /// Send a "thinking/working" update to the channel. Implement as no-op if channel doesn't support it.
        /// </summary>
        protected abstract Task SendInformativeAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// Called after the buffer drains (streaming) or immediately (non-streaming) by EndStreamAsync.
        /// Send the final message, call channel stop API, or no-op.
        /// For FallbackToNonStreaming: IsStreamingChannel will be false when called — check it to decide format.
        /// </summary>
        protected abstract Task FinalizeStreamAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Called on the timer thread when SendChunksAsync throws. Interpret the error and return
        /// a StreamErrorAction. The base class applies the action (stop timer, set flags, etc.).
        /// </summary>
        protected abstract Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken cancellationToken);

        // ── Timer management ─────────────────────────────────────────────────

        private void StartStream(int initialInterval = 0)
        {
            if (_timer == null && IsStreamingChannel)
            {
                _streamStarted = true;
                _timer = new Timer(OnTimerTick, null,
                    initialInterval == 0 ? Interval : initialInterval,
                    Timeout.Infinite);
            }
        }

        private void StopStream()
        {
            _timer?.Dispose();
            _timer = null;
        }

        private async void OnTimerTick(object? state)
        {
            string? textToSend = null;
            int seqToSend = 0;

            lock (_lock)
            {
                if (_messageUpdated)
                {
                    textToSend = Message;
                    seqToSend = ++_sequenceNumber;
                    _messageUpdated = false;
                    _timer?.Change(Timeout.Infinite, Timeout.Infinite); // pause during send
                }
                else if (_streamEnded)
                {
                    _queueEmpty.Set();
                    StopStream();
                    return;
                }
                else
                {
                    // No new text yet — poll faster while waiting for chunks
                    _timer?.Change(200, Timeout.Infinite);
                    return;
                }
            }

            try
            {
                await SendChunksAsync(textToSend!, seqToSend, CancellationToken.None).ConfigureAwait(false);

                lock (_lock)
                {
                    if (_streamEnded && !_messageUpdated)
                    {
                        _queueEmpty.Set();
                        StopStream();
                    }
                    else
                    {
                        _timer?.Change(Interval, Timeout.Infinite); // restart
                    }
                }
            }
            catch (Exception ex)
            {
                // Cannot rethrow — we're on the timer thread. Call the error hook.
                var action = await HandleSendErrorAsync(ex, CancellationToken.None).ConfigureAwait(false);

                lock (_lock)
                {
                    switch (action)
                    {
                        case StreamErrorAction.FallbackToNonStreaming:
                            IsStreamingChannel = false;
                            break;
                        case StreamErrorAction.Cancel:
                            _streamCancelled = true;
                            _userCancelled = true;
                            break;
                        // Continue: do nothing, keep streaming
                    }
                    StopStream();
                    _queueEmpty.Set();
                }

                // If fallback: EndStreamAsync will call FinalizeStreamAsync after the wait unblocks
            }
        }
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

Expected: 0 errors.

- [ ] **Step 5: Run base tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseBaseTests"
```

Expected: all pass. If `EndStreamAsync_StreamingChannel_SendsChunksAndFinalizes` is flaky due to timing, increase the delay or the Interval value.

- [ ] **Step 6: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponseBase.cs \
        src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseBaseTests.cs
git commit -m "feat: add StreamingResponseBase abstract class"
```

---

## Task 3: Refactor `StreamingResponse` to Extend `StreamingResponseBase`

**Files:**
- Modify: `src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs`
- Test: `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseTests.cs` (existing — must still pass)

- [ ] **Step 1: Run existing streaming tests to get baseline**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseTests"
```

Record the count — all must pass before and after this task.

- [ ] **Step 2: Refactor `StreamingResponse.cs`**

Change `internal class StreamingResponse : IStreamingResponse` to `internal class StreamingResponse : StreamingResponseBase`.

Key changes:
1. **Class declaration**: `internal class StreamingResponse : StreamingResponseBase`
2. **Remove fields that move to base**: `_nextSequence`, `_ended`, `_timer`, `_messageUpdated`, `_canceled`, `_userCanceled`, `_queue`, `_queueEmpty`
3. **Keep fields unique to `StreamingResponse`**: `_context` (TurnContext), `_isTeamsChannel` (bool)
4. **`IsStreamingChannel`**: remove `{ get; private set; }` — inherited from base with `protected set`
5. **`Citations`**: override with `protected set`:
   ```csharp
   public override List<ClientCitation>? Citations { get; protected set; } = [];
   ```
6. **Other Teams properties**: add `override` keyword (`FinalMessage`, `FeedbackLoopEnabled`, etc.) — they were already on `IStreamingResponse`, now they override the virtual no-ops in base
7. **`Interval`, `EndStreamTimeout`, `StreamId`, `Message`**: remove — inherited from base
8. **`UpdatesSent()`**: remove — inherited from base
9. **`IsStreamStarted()`**: remove — inherited from base
10. **`QueueTextChunk`**: remove — inherited from base. Do **not** add a `QueueTextChunk` override.

   Citation formatting is applied exclusively in `SendChunksAsync` (item 14 below) where `CitationUtils.FormatCitationsResponse(bufferedText)` formats the snapshot before sending. Applying it again here in `QueueTextChunk` would double-format the text. The `Message` property on the base accumulates raw unformatted text; the formatted text appears only in what is sent over the wire.

11. **`QueueInformativeUpdateAsync`**: remove — inherited from base
12. **`EndStreamAsync`**: remove — inherited from base
13. **`ResetAsync`**: call `base.ResetAsync()`, then re-run `SetDefaults()` and reset Teams-specific fields:
    ```csharp
    public override async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await base.ResetAsync(cancellationToken).ConfigureAwait(false);
        FinalMessage = null;
        Citations = [];
        SensitivityLabel = null;
        EnableGeneratedByAILabel = false;
        SetDefaults(_context);
    }
    ```

14. **Implement abstract hooks**:

    ```csharp
    protected override async Task SendChunksAsync(string bufferedText, int sequenceNumber, CancellationToken ct)
    {
        var formattedText = CitationUtils.FormatCitationsResponse(bufferedText);
        var activity = new Activity
        {
            Type = ActivityTypes.Typing,
            Text = formattedText,
            Entities = []
        };

        activity.Entities.Add(new StreamInfo
        {
            StreamType = StreamTypes.Streaming,
            StreamSequence = sequenceNumber,
        });

        if (Citations is { Count: > 0 })
        {
            var currCitations = CitationUtils.GetUsedCitations(formattedText, Citations);
            var entity = new AIEntity();
            if (currCitations is { Count: > 0 })
                entity.Citation = currCitations;
            activity.Entities.Add(entity);
        }

        await SendStreamActivityAsync(activity, ct).ConfigureAwait(false);
    }

    protected override async Task SendInformativeAsync(string text, CancellationToken ct)
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Typing,
            Text = text,
            Entities = [new StreamInfo { StreamType = StreamTypes.Informative }]
        };
        await SendStreamActivityAsync(activity, ct).ConfigureAwait(false);
    }

    protected override Task FinalizeStreamAsync(CancellationToken ct)
    {
        if (!IsStreamingChannel)
        {
            // Non-streaming or fallback: send plain message only if there's content
            if (UpdatesSent() > 0 || FinalMessage != null || !string.IsNullOrWhiteSpace(Message))
                return _context.SendActivityAsync(CreateFinalMessage(), ct);
            return Task.CompletedTask;
        }

        if (UpdatesSent() > 0 || FinalMessage != null)
            return SendStreamActivityAsync(CreateFinalMessage(), ct);
        return Task.CompletedTask;
    }

    protected override async Task<StreamErrorAction> HandleSendErrorAsync(Exception ex, CancellationToken ct)
    {
        if (ex is ErrorResponseException errorResponse)
        {
            if (TeamsStreamCancelled.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase))
            {
                _context?.Adapter?.Logger?.LogWarning("User canceled stream on the client side.");
                return StreamErrorAction.Cancel;
            }

#pragma warning disable CA1862
            if (BadArgument.Equals(errorResponse.Body?.Error?.Code, StringComparison.OrdinalIgnoreCase) &&
                errorResponse.Body?.Error?.Message.ToLower().Contains(TeamsStreamNotAllowed) == true)
            {
                _context?.Adapter?.Logger?.LogWarning("Streaming disabled for this turn.");
                return StreamErrorAction.FallbackToNonStreaming;
            }
#pragma warning restore CA1862

            _context?.Adapter?.Logger?.LogWarning("Exception during StreamingResponse: {Message}", ex.Message);
        }
        return StreamErrorAction.Continue;
    }
    ```

15. **`SendStreamActivityAsync`** (private helper — replaces old `SendActivityAsync`):
    ```csharp
    private async Task SendStreamActivityAsync(IActivity activity, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(StreamId))
        {
            activity.Id = StreamId;
            activity.GetStreamingEntity().StreamId = StreamId;
        }

        var response = await _context.SendActivityAsync(activity, ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(StreamId))
            StreamId = response.Id;
    }
    ```

16. **`SetDefaults`**: update to set `IsStreamingChannel` (base property) instead of `IsStreamingChannel = ...` (which now works via `protected set`). Also set `_isTeamsChannel`.

17. **`CreateFinalMessage()`**: keep as-is (private helper building the final activity). It references `IsStreamingChannel` to decide whether to add StreamInfo.Final — this works correctly since `IsStreamingChannel` is inherited.

- [ ] **Step 3: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

Expected: 0 errors. Fix any compilation issues before proceeding.

- [ ] **Step 4: Run ALL streaming tests to verify no regression**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponse"
```

Expected: same count as baseline (all pass). If any fail, fix before committing.

- [ ] **Step 5: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/StreamingResponse.cs
git commit -m "refactor: StreamingResponse extends StreamingResponseBase"
```

---

## Task 4: Modify `TurnContext`

**Files:**
- Modify: `src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs`
- Modify: `src/tests/Microsoft.Agents.Builder.Tests/TurnContextTests.cs` (add new tests)

- [ ] **Step 1: Add tests to `TurnContextTests.cs`**

Open `src/tests/Microsoft.Agents.Builder.Tests/TurnContextTests.cs` and add the following test class (or add to the existing class if it exists):

```csharp
// Add to TurnContextTests.cs:

[Fact]
public void StreamingResponse_DefaultsToStreamingResponse_WhenNoFactoryRegistered()
{
    var adapter = new Mock<IChannelAdapter>().Object;
    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "test" };
    var ctx = new TurnContext(adapter, activity);

    // Default: should create a StreamingResponse (the existing Activity-based impl)
    var sr = ctx.StreamingResponse;
    Assert.NotNull(sr);
    // It should be lazy — accessing twice returns same instance
    Assert.Same(sr, ctx.StreamingResponse);
}

[Fact]
public void SetStreamingResponse_ReplacesDefault()
{
    var adapter = new Mock<IChannelAdapter>().Object;
    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "slack" };
    var ctx = new TurnContext(adapter, activity);

    var customSr = new Mock<IStreamingResponse>().Object;
    ctx.SetStreamingResponse(customSr);

    Assert.Same(customSr, ctx.StreamingResponse);
}

[Fact]
public void SetStreamingResponse_AfterStreamStarted_Throws()
{
    var adapter = new Mock<IChannelAdapter>();
    adapter.Setup(a => a.SendActivitiesAsync(It.IsAny<ITurnContext>(), It.IsAny<IActivity[]>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new[] { new ResourceResponse() });

    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "directline" };
    var ctx = new TurnContext(adapter.Object, activity);

    // Start the stream
    ctx.StreamingResponse.QueueTextChunk("hello");

    var customSr = new Mock<IStreamingResponse>();
    customSr.Setup(s => s.IsStreamStarted()).Returns(false);

    // After stream started, SetStreamingResponse should throw
    // (The existing default sr has IsStreamStarted() = true)
    Assert.Throws<InvalidOperationException>(() => ctx.SetStreamingResponse(new Mock<IStreamingResponse>().Object));
}

[Fact]
public void CopyConstructor_CarriesForwardStreamingResponse()
{
    var adapter = new Mock<IChannelAdapter>().Object;
    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "slack" };
    var ctx = new TurnContext(adapter, activity);

    var customSr = new Mock<IStreamingResponse>().Object;
    ctx.SetStreamingResponse(customSr);

    // Copy constructor should carry forward the streaming response
    var ctx2 = new TurnContext(ctx, new Activity { Type = ActivityTypes.Message });
    Assert.Same(customSr, ctx2.StreamingResponse);
}
```

- [ ] **Step 2: Run to confirm FAIL**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~TurnContextTests"
```

The new tests should fail (SetStreamingResponse doesn't exist yet).

- [ ] **Step 3: Modify `TurnContext.cs`**

Exact changes:

**Line 33** — change field declaration:
```csharp
// Before:
private readonly IStreamingResponse _streamingResponse;

// After:
private IStreamingResponse? _streamingResponse;
```

**Line 54** (in first constructor) — remove the eager assignment:
```csharp
// Remove this line:
_streamingResponse = new StreamingResponse(this);
```

**Line 71** (in copy constructor) — replace eager assignment with carry-forward:
```csharp
// Before:
_streamingResponse = new StreamingResponse(this);

// After:
_streamingResponse = (turnContext as TurnContext)?._streamingResponse;
```

**Line 110** — change property to lazy:
```csharp
// Before:
public IStreamingResponse StreamingResponse { get { return _streamingResponse; } }

// After:
public IStreamingResponse StreamingResponse => _streamingResponse ??= new StreamingResponse(this);
```

**Add new method** (after the `StreamingResponse` property):
```csharp
/// <summary>
/// Sets a custom streaming response implementation, typically called by adapters
/// that have resolved an <see cref="IStreamingResponseFactory"/> from DI.
/// Must be called before the stream has started.
/// </summary>
internal void SetStreamingResponse(IStreamingResponse streamingResponse)
{
    if (_streamingResponse?.IsStreamStarted() == true)
        throw new InvalidOperationException(
            "Cannot set streaming response after the stream has started.");
    _streamingResponse = streamingResponse;
}
```

- [ ] **Step 4: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

Expected: 0 errors.

- [ ] **Step 5: Run TurnContext tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~TurnContextTests"
```

Expected: all pass.

- [ ] **Step 6: Run full Builder test suite to catch regressions**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/
```

Expected: all pass.

- [ ] **Step 7: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/TurnContext.cs \
        src/tests/Microsoft.Agents.Builder.Tests/TurnContextTests.cs
git commit -m "feat: TurnContext lazy StreamingResponse with SetStreamingResponse hook"
```

---

## Task 5: Registration Helpers

**Files:**
- Create: `src/libraries/Builder/Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs`
- Modify: `src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs` (add DI tests)

- [ ] **Step 1: Add DI registration tests**

Add to `StreamingResponseFactoryTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Add these tests to StreamingResponseFactoryTests class:

[Fact]
public void AddStreamingResponseFactory_PopulatesRegistry()
{
    var services = new ServiceCollection();
    services.AddStreamingResponseFactory<TestFactory>("slack");

    var provider = services.BuildServiceProvider();
    var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

    Assert.True(registry.TryGet("slack", out var factory));
    Assert.NotNull(factory);
}

[Fact]
public void AddStreamingResponseFactory_TwoChannels_BothInRegistry()
{
    var services = new ServiceCollection();
    services.AddStreamingResponseFactory<TestFactory>("slack");
    services.AddStreamingResponseFactory<TestFactory2>("teams");

    var provider = services.BuildServiceProvider();
    var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

    Assert.True(registry.TryGet("slack", out _));
    Assert.True(registry.TryGet("teams", out _));
}

[Fact]
public void AddStreamingResponseFactory_InstanceOverload_Works()
{
    var services = new ServiceCollection();
    var factory = new TestFactory();
    services.AddStreamingResponseFactory("slack", factory);

    var provider = services.BuildServiceProvider();
    var registry = provider.GetRequiredService<StreamingResponseFactoryRegistry>();

    Assert.True(registry.TryGet("slack", out var resolved));
    Assert.Same(factory, resolved);
}

// Test helpers (private inner classes in test file)
private class TestFactory : IStreamingResponseFactory
{
    public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
}
private class TestFactory2 : IStreamingResponseFactory
{
    public IStreamingResponse Create(ITurnContext ctx) => new Mock<IStreamingResponse>().Object;
}
```

- [ ] **Step 2: Run to confirm FAIL**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseFactoryTests"
```

Expected: new tests fail (AddStreamingResponseFactory doesn't exist).

- [ ] **Step 3: Implement `ServiceCollectionStreamingExtensions.cs`**

```csharp
// src/libraries/Builder/Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Extension methods for registering <see cref="IStreamingResponseFactory"/> implementations.
    /// </summary>
    /// <remarks>
    /// Requires <c>Microsoft.Extensions.DependencyInjection</c> (MEDI).
    /// The netstandard2.0 registry path uses <c>GetServices&lt;T&gt;()</c> returning multiple instances
    /// of the same concrete type, which is supported by MEDI but not all third-party containers.
    /// </remarks>
    public static class ServiceCollectionStreamingExtensions
    {
        /// <summary>
        /// Registers a streaming response factory for the specified channel.
        /// On .NET 8+, also registers as a keyed service for fast lookup.
        /// </summary>
        /// <typeparam name="TFactory">The factory type to register.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="channelId">The channel ID (e.g., "slack", "msteams").</param>
        public static IServiceCollection AddStreamingResponseFactory<TFactory>(
            this IServiceCollection services,
            string channelId)
            where TFactory : class, IStreamingResponseFactory
        {
            services.TryAddTransient<TFactory>();

            // Each call adds a registration entry. GetServices<T>() returns all of them.
            services.AddSingleton(new StreamingResponseFactoryRegistration(
                channelId,
                sp => sp.GetRequiredService<TFactory>()));

            // Registry singleton built lazily from all accumulated entries at first resolve.
            // TryAddSingleton: only the first registration wins — subsequent calls are no-ops.
            // This is correct: the factory delegate reads all entries from GetServices<T>().
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

        /// <summary>
        /// Registers a streaming response factory instance for the specified channel.
        /// </summary>
        public static IServiceCollection AddStreamingResponseFactory(
            this IServiceCollection services,
            string channelId,
            IStreamingResponseFactory factory)
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
    }
}
```

- [ ] **Step 4: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

- [ ] **Step 5: Run factory tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~StreamingResponseFactoryTests"
```

Expected: all pass.

- [ ] **Step 6: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/ServiceCollectionStreamingExtensions.cs \
        src/tests/Microsoft.Agents.Builder.Tests/StreamingResponseFactoryTests.cs
git commit -m "feat: AddStreamingResponseFactory DI registration helpers"
```

---

## Task 6: Modify `ChannelAdapter` — Factory Injection Hook

**Files:**
- Modify: `src/libraries/Builder/Microsoft.Agents.Builder/ChannelAdapter.cs`
- Modify: `src/tests/Microsoft.Agents.Builder.Tests/ChannelAdapterTests.cs` (add factory test)

The goal: when `ProcessActivityAsync` / `ProcessProactiveAsync` create `TurnContext`, they call `SetStreamingResponse` if a factory is registered for the channel.

`ChannelAdapter` is abstract and has no `IServiceProvider`. Add:
1. `public IServiceProvider? Services { get; protected set; }` property — concrete adapters set this in their constructor if they have it
2. `ResolveStreamingResponseFactory(string? channelId)` protected helper using `Services`
3. Call in `ProcessActivityAsync` and `ProcessProactiveAsync` after `TurnContext` is created

`CloudAdapter` (the default production adapter) is registered as a DI singleton, so adding an optional `IServiceProvider? services = null` constructor parameter lets DI inject it automatically. `CloudAdapter` then sets `Services = services`.

- [ ] **Step 1: Add factory resolution test**

Add to `ChannelAdapterTests.cs`:

```csharp
[Fact]
public async Task ProcessActivityAsync_InjectsFactoryStreamingResponse_WhenRegistered()
{
    // Arrange
    var mockSr = new Mock<IStreamingResponse>();
    mockSr.Setup(s => s.IsStreamStarted()).Returns(false);

    var factory = new Mock<IStreamingResponseFactory>();
    factory.Setup(f => f.Create(It.IsAny<ITurnContext>())).Returns(mockSr.Object);

    var services = new ServiceCollection();
    services.AddStreamingResponseFactory("test-channel", factory.Object);
    var provider = services.BuildServiceProvider();

    ITurnContext? capturedContext = null;
    var adapter = new TestChannelAdapter(provider);
    adapter.Setup_SendActivities(); // no-op

    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "test-channel" };
    var identity = new ClaimsIdentity();

    await adapter.ProcessActivityAsync(identity, activity,
        (ctx, ct) => { capturedContext = ctx; return Task.CompletedTask; },
        CancellationToken.None);

    // The streaming response on the turn context should be our mock
    Assert.Same(mockSr.Object, capturedContext!.StreamingResponse);
}

[Fact]
public async Task ProcessActivityAsync_NoFactory_UsesDefaultStreamingResponse()
{
    var adapter = new TestChannelAdapter(services: null);
    var activity = new Activity { Type = ActivityTypes.Message, ChannelId = "unknown-channel" };
    var identity = new ClaimsIdentity();

    ITurnContext? capturedContext = null;
    await adapter.ProcessActivityAsync(identity, activity,
        (ctx, ct) => { capturedContext = ctx; return Task.CompletedTask; },
        CancellationToken.None);

    // Default: should be the built-in StreamingResponse (not null)
    Assert.NotNull(capturedContext!.StreamingResponse);
}
```

You'll need to create or extend a `TestChannelAdapter` in the test file that accepts `IServiceProvider?` and sets `Services`:

```csharp
private class TestChannelAdapter : ChannelAdapter
{
    private readonly List<IActivity> _sent = new();

    public TestChannelAdapter(IServiceProvider? services = null)
    {
        Services = services;
    }

    public void Setup_SendActivities() { } // no-op, SendActivitiesAsync already returns empty

    public override Task<ResourceResponse[]> SendActivitiesAsync(
        ITurnContext ctx, IActivity[] activities, CancellationToken ct)
        => Task.FromResult(activities.Select(a => new ResourceResponse(a.Id ?? "id")).ToArray());
}
```

- [ ] **Step 2: Run to confirm FAIL**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~ChannelAdapterTests"
```

Expected: new tests fail.

- [ ] **Step 3: Modify `ChannelAdapter.cs`**

**Add `Services` property** (after the `Logger` property, around line 32):

```csharp
/// <summary>
/// Optional service provider used to resolve streaming response factories.
/// Concrete adapters that have DI access should set this in their constructor.
/// </summary>
public IServiceProvider? Services { get; protected set; }
```

**Add `ResolveStreamingResponseFactory` helper** (`protected`, near the end of the class — must be `protected` not `private` so `ChannelServiceAdapterBase` can call it via inheritance):

```csharp
protected IStreamingResponseFactory? ResolveStreamingResponseFactory(string? channelId)
{
    if (channelId is null || Services is null)
        return null;

#if NET8_0_OR_GREATER
    if (Services.GetKeyedService<IStreamingResponseFactory>(channelId) is { } keyedFactory)
        return keyedFactory;
#endif

    var registry = Services.GetService<StreamingResponseFactoryRegistry>();
    if (registry?.TryGet(channelId, out var registryFactory) == true)
        return registryFactory;

    return null;
}
```

**Add necessary usings**:
```csharp
using Microsoft.Extensions.DependencyInjection;
```

**Modify `ProcessActivityAsync`** (around line 124) — inject factory after context creation:

```csharp
public virtual async Task<InvokeResponse> ProcessActivityAsync(
    ClaimsIdentity claimsIdentity, IActivity activity, AgentCallbackHandler callback,
    CancellationToken cancellationToken)
{
    AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
    AssertionHelpers.ThrowIfNull(activity, nameof(activity));
    AssertionHelpers.ThrowIfNull(callback, nameof(callback));

    using var context = new TurnContext(this, activity, claimsIdentity);

    // Inject channel-specific streaming response if a factory is registered
    var factory = ResolveStreamingResponseFactory(activity.ChannelId);
    if (factory is not null)
        context.SetStreamingResponse(factory.Create(context));

    await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
    return ProcessTurnResults(context);
}
```

**Modify `ProcessProactiveAsync`** (around line 140) — same injection pattern:

```csharp
public virtual async Task ProcessProactiveAsync(
    ClaimsIdentity claimsIdentity, IActivity continuationActivity, string audience,
    AgentCallbackHandler callback, CancellationToken cancellationToken)
{
    AssertionHelpers.ThrowIfNull(claimsIdentity, nameof(claimsIdentity));
    AssertionHelpers.ThrowIfNull(continuationActivity, nameof(continuationActivity));
    AssertionHelpers.ThrowIfNull(callback, nameof(callback));

    using var context = new TurnContext(this, continuationActivity, claimsIdentity);

    var factory = ResolveStreamingResponseFactory(continuationActivity.ChannelId);
    if (factory is not null)
        context.SetStreamingResponse(factory.Create(context));

    await RunPipelineAsync(context, callback, cancellationToken).ConfigureAwait(false);
}
```

- [ ] **Step 4: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

- [ ] **Step 5: Run adapter tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~ChannelAdapterTests"
```

Expected: all pass.

- [ ] **Step 6: Run full test suite**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/
```

Expected: all pass.

- [ ] **Step 7: Modify `ChannelServiceAdapterBase.cs`**

`CloudAdapter` extends `ChannelServiceAdapterBase`, which overrides both `ProcessActivityAsync` and `ProcessProactiveAsync` without calling `base`. Therefore the injections in `ChannelAdapter` are never reached for real deployments — `ChannelServiceAdapterBase` must also apply factory injection.

Both methods inherit `ResolveStreamingResponseFactory` and `Services` from `ChannelAdapter`, so only two lines need to be added to each.

**Note:** `ChannelServiceAdapterBase.CreateConversationAsync` also creates a `TurnContext` (line ~159) and calls `RunPipelineAsync`. Factory injection is **intentionally excluded** there — streaming during a `CreateConversation` flow is not a supported use case, and the activity type is `createConversation`, not a normal message turn.

**In `ProcessActivityAsync`** — after `using var context = new TurnContext(this, activity, claimsIdentity);` (around line 250), before `ResolveIfConnectorClientIsNeeded`:

```csharp
// Inject channel-specific streaming response if a factory is registered
var srFactory = ResolveStreamingResponseFactory(activity.ChannelId);
if (srFactory is not null)
    context.SetStreamingResponse(srFactory.Create(context));
```

**In `ProcessProactiveAsync`** — after `using var context = new TurnContext(this, continuationActivity, claimsIdentity);` (around line 207), before `ChannelServiceFactory.CreateConnectorClientAsync`:

```csharp
var srFactory = ResolveStreamingResponseFactory(continuationActivity.ChannelId);
if (srFactory is not null)
    context.SetStreamingResponse(srFactory.Create(context));
```

- [ ] **Step 8: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

- [ ] **Step 9: Run adapter tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~ChannelAdapterTests"
```

Expected: all pass.

- [ ] **Step 10: Run full test suite**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/
```

Expected: all pass.

- [ ] **Step 11: Modify `CloudAdapter.cs` to populate `Services`**

`CloudAdapter` is registered as a DI singleton. Adding an optional `IServiceProvider? services = null` parameter to its constructor lets DI inject it automatically, with no breaking change to existing code using DI registration or explicit construction with positional parameters.

In the `CloudAdapter` constructor signature (in `src/libraries/Hosting/AspNetCore/CloudAdapter.cs`), add `IServiceProvider? services = null` as the last parameter. In the constructor body add:

```csharp
Services = services;
```

The `Services` property is declared with `protected set` on `ChannelAdapter`, so `CloudAdapter` can assign it in its constructor body.

Since `CloudAdapter` is in a different project (`Microsoft.Agents.Hosting.AspNetCore`) from `ChannelAdapter` (`Microsoft.Agents.Builder`), ensure `using Microsoft.Extensions.DependencyInjection;` is present in `CloudAdapter.cs` if needed (it may already be there for other DI calls).

- [ ] **Step 12: Build**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Debug --no-incremental
```

- [ ] **Step 13: Run adapter tests**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/ --filter "FullyQualifiedName~ChannelAdapterTests"
```

- [ ] **Step 14: Run full test suite**

```
dotnet test src/tests/Microsoft.Agents.Builder.Tests/
```

Expected: all pass.

- [ ] **Step 15: Commit**

```
git add src/libraries/Builder/Microsoft.Agents.Builder/ChannelAdapter.cs \
        src/libraries/Builder/Microsoft.Agents.Builder/ChannelServiceAdapterBase.cs \
        src/libraries/Hosting/AspNetCore/CloudAdapter.cs \
        src/tests/Microsoft.Agents.Builder.Tests/ChannelAdapterTests.cs
git commit -m "feat: ChannelAdapter + ChannelServiceAdapterBase + CloudAdapter factory injection for IStreamingResponseFactory"
```

---

## Task 7: Final Verification

- [ ] **Step 1: Build full solution in Release**

```
dotnet build src/Microsoft.Agents.SDK.sln -c Release
```

Expected: 0 errors, 0 warnings introduced by this change.

- [ ] **Step 2: Run all tests**

```
dotnet test src/tests/ --no-build -c Debug
```

Expected: all existing tests pass plus new tests.

- [ ] **Step 3: Final commit (if clean-up needed)**

If any files were modified during clean-up that are not yet staged, add them explicitly:

```
git status   # identify any remaining unstaged files
git add <list any remaining files explicitly>
git commit -m "feat: StreamingResponse extensibility - complete"
```
