// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Builder.Tests.App.TestUtils;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests.App
{
    public class TypingWorkerTests
    {
        private static TypingOptions MakeOptions(int initialDelayMs, int intervalMs) =>
            new TypingOptions
            {
                InitialDelayMs = initialDelayMs,
                IntervalMs = intervalMs,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
                {
                    [Channels.M365Copilot] = new TypingChannelStrategy(initialDelayMs: 0, intervalMs: intervalMs)
                }
            };

        private static (TestAdapter adapter, TurnContext context) CreateMessageTurn(
            string channelId = Microsoft.Agents.Core.Models.Channels.Test)
        {
            var adapter = new TestAdapter(channelId);
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = channelId,
                Conversation = new ConversationAccount { Id = "conv1" },
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot1" },
            };
            var context = new TurnContext(adapter, activity);
            return (adapter, context);
        }

        private static Activity MakeMessageActivity(
            string channelId = Microsoft.Agents.Core.Models.Channels.Test) =>
            new Activity
            {
                Type = ActivityTypes.Message,
                Text = "hello",
                ChannelId = channelId,
                Conversation = new ConversationAccount { Id = "conv1" },
                From = new ChannelAccount { Id = "user1" },
                Recipient = new ChannelAccount { Id = "bot1" },
            };

        private static Activity PingEvent() =>
            new Activity
            {
                Type = ActivityTypes.Event,
                Name = "ping",
                ChannelId = Microsoft.Agents.Core.Models.Channels.Test,
                Conversation = new ConversationAccount { Id = "conv1" }
            };

        // Awaits a synchronization signal with a generous ceiling so a genuine hang fails fast
        // instead of blocking the test run indefinitely. The ceiling is never reached in the
        // normal (deterministic, virtual-time) case because the signal is released as soon as
        // the worker reaches the awaited state.
        private static async Task AwaitSignal(SemaphoreSlim signal, string what)
        {
            Assert.True(
                await signal.WaitAsync(TimeSpan.FromSeconds(10)),
                $"Timed out waiting for: {what}.");
        }

        [Fact]
        public void Create_ReturnsNull_ForNonMessageActivity()
        {
            var adapter = new TestAdapter();
            var activity = new Activity
            {
                Type = ActivityTypes.ConversationUpdate,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Test,
                Conversation = new ConversationAccount { Id = "c" },
                From = new ChannelAccount { Id = "u" },
                Recipient = new ChannelAccount { Id = "b" }
            };
            var context = new TurnContext(adapter, activity);

            var worker = TypingWorker.Create(context, MakeOptions(500, 2000));

            Assert.Null(worker);
        }

        [Fact]
        public void Create_ReturnsWorker_ForMessageActivity()
        {
            var (_, context) = CreateMessageTurn();

            var worker = TypingWorker.Create(context, MakeOptions(500, 2000));

            Assert.NotNull(worker);
        }

        [Fact]
        public void Create_UsesDefaultStrategy_ForUnknownChannel()
        {
            var (_, context) = CreateMessageTurn("unknownchannel");

            var worker = TypingWorker.Create(context, MakeOptions(123, 456));

            Assert.NotNull(worker);
        }

        [Fact]
        public void Create_UsesM365CopilotStrategy_ForM365CopilotChannel()
        {
            // M365Copilot must use its channel-specific strategy (InitialDelayMs = 0).
            var (_, context) = CreateMessageTurn(Channels.M365Copilot);

            var worker = TypingWorker.Create(context, MakeOptions(9999, 2000));

            Assert.NotNull(worker);
        }

        [Fact]
        public async Task Start_SendsTypingActivity_AfterInitialDelay()
        {
            var time = new SignalingTimeProvider();
            var adapter = new SignalingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            // Huge interval: only the single initial-delay typing should ever fire.
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 500, intervalMs: 30_000), time)!;

            worker.Start();

            await AwaitSignal(time.TimerArmed, "initial-delay timer armed");
            time.Advance(TimeSpan.FromMilliseconds(500));
            await AwaitSignal(adapter.TypingSent, "first typing activity");

            await worker.DisposeAsync();

            var typingCount = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(1, typingCount);
        }

        [Fact]
        public async Task Start_SendsMultipleTypingActivities_AtInterval()
        {
            var time = new SignalingTimeProvider();
            var adapter = new SignalingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 100, intervalMs: 100), time)!;

            worker.Start();

            // Deterministically drive four typing activities by advancing virtual time exactly
            // one interval at a time, synchronizing on the worker's own timer/send signals.
            for (var i = 0; i < 4; i++)
            {
                await AwaitSignal(time.TimerArmed, $"timer #{i + 1} armed");
                time.Advance(TimeSpan.FromMilliseconds(100));
                await AwaitSignal(adapter.TypingSent, $"typing #{i + 1}");
            }

            await worker.DisposeAsync();

            var typingCount = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(4, typingCount);
        }

        [Fact]
        public async Task Start_StopsOnDispose()
        {
            var time = new SignalingTimeProvider();
            var adapter = new SignalingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 100, intervalMs: 100), time)!;

            worker.Start();

            // Fire two typing activities deterministically.
            for (var i = 0; i < 2; i++)
            {
                await AwaitSignal(time.TimerArmed, $"timer #{i + 1} armed");
                time.Advance(TimeSpan.FromMilliseconds(100));
                await AwaitSignal(adapter.TypingSent, $"typing #{i + 1}");
            }

            await worker.DisposeAsync();
            var countAfterDispose = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);

            // Advancing far past several intervals after dispose must not produce any more typing.
            time.Advance(TimeSpan.FromMilliseconds(100 * 10));
            var countAfterAdvance = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);

            Assert.Equal(countAfterDispose, countAfterAdvance);
        }

        [Fact]
        public async Task Start_StopsOnStreamingFinalTypingActivitySent()
        {
            var time = new SignalingTimeProvider();
            var adapter = new SignalingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            // Huge initial delay so the worker is still parked when we send the final activity.
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 30_000, intervalMs: 30_000), time)!;
            worker.Start();

            // Ensure the worker is parked on its initial-delay timer before we stop it.
            await AwaitSignal(time.TimerArmed, "initial-delay timer armed");

            // Send a streaming-final typing activity through the turn context middleware pipeline,
            // which the worker observes and treats as a stop signal.
            var finalTyping = new Activity
            {
                Type = ActivityTypes.Typing,
                ChannelId = Microsoft.Agents.Core.Models.Channels.Test,
                Entities = [new StreamInfo { StreamType = StreamTypes.Final }]
            };
            await context.SendActivityAsync(finalTyping, CancellationToken.None);

            // DisposeAsync completes only if the worker actually stopped (it awaits the task).
            await worker.DisposeAsync();
            var countAfterStop = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);

            // Advancing past the (huge) initial delay must not resurrect the worker.
            time.Advance(TimeSpan.FromMilliseconds(60_000));
            var countAfterAdvance = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);

            Assert.Equal(countAfterStop, countAfterAdvance);
        }

        // ── Fix 1: transport errors must not fault the background task ───────────────

        [Fact]
        public async Task RunAsync_DoesNotFaultTask_WhenAdapterThrows()
        {
            // Arrange: adapter that throws on every send (simulates a transient transport error).
            var time = new SignalingTimeProvider();
            var adapter = new ThrowingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 100, intervalMs: 30_000), time)!;

            worker.Start();

            // Act: advance to the first send and wait until the adapter has thrown.
            await AwaitSignal(time.TimerArmed, "initial-delay timer armed");
            time.Advance(TimeSpan.FromMilliseconds(100));
            await AwaitSignal(adapter.SendAttempted, "send attempt (that throws)");

            // Assert: DisposeAsync must not re-throw; a faulted _workerTask would propagate here.
            await worker.DisposeAsync();
        }

        // ── Fix 2: negative delay values must be rejected early ──────────────────────
        //
        // Task.Delay(ms) throws ArgumentOutOfRangeException for ms < 0 (other than -1).
        // Since TypingOptions/strategies are publicly settable, TypingWorker.Create validates
        // them and throws immediately so the problem surfaces at configuration time rather
        // than being silently swallowed inside the background task.

        [Fact]
        public void Create_WithNegativeInitialDelay_ThrowsArgumentOutOfRange()
        {
            var (_, context) = CreateMessageTurn();
            var options = new TypingOptions
            {
                InitialDelayMs = -1,
                IntervalMs = 2000,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => TypingWorker.Create(context, options));
        }

        [Fact]
        public void Create_WithNegativeIntervalMs_ThrowsArgumentOutOfRange()
        {
            var (_, context) = CreateMessageTurn();
            var options = new TypingOptions
            {
                InitialDelayMs = 0,
                IntervalMs = -1,
                ChannelStrategies = new Dictionary<string, ITypingChannelStrategy>(
                    System.StringComparer.OrdinalIgnoreCase)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => TypingWorker.Create(context, options));
        }

        // ── Fix 3: StopTypingTimer must clear the service entry so Start can restart ─

        [Fact]
        public async Task StopTypingTimer_ClearsServiceEntry_AllowingRestart()
        {
            // Arrange
            var (_, context) = CreateMessageTurn();
            var app = new TestApplication(new TestApplicationOptions(new MemoryStorage()));

            // First start registers the worker.
            app.StartTypingTimer(context);
            Assert.NotNull(context.Services.Get<TypingWorker>());

            // Stop disposes and clears the service entry.
            await app.StopTypingTimer(context);
            Assert.Null(context.Services.Get<TypingWorker>());  // cleared by StopTypingTimer

            // Second start must succeed (without Fix 3 it early-returns because
            // the disposed worker is still registered).
            app.StartTypingTimer(context);
            Assert.NotNull(context.Services.Get<TypingWorker>());

            // Clean up.
            await app.StopTypingTimer(context);
        }

        // ─────────────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A <see cref="TestAdapter"/> whose <see cref="SendActivitiesAsync"/> always throws,
        /// simulating a transient transport failure. Signals <see cref="SendAttempted"/> before
        /// throwing so tests can synchronize on the failure without sleeping.
        /// </summary>
        private sealed class ThrowingTestAdapter : TestAdapter
        {
            private readonly SemaphoreSlim _sendAttempted = new(0);

            public SemaphoreSlim SendAttempted => _sendAttempted;

            public ThrowingTestAdapter() : base(Microsoft.Agents.Core.Models.Channels.Test)
            {
            }

            public override Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
            {
                _sendAttempted.Release();
                throw new InvalidOperationException("Simulated transport error");
            }
        }

        [Fact]
        public async Task Start_ResetsInterval_AfterNonTypingActivitySent()
        {
            // A reset issued mid-countdown must restart the full interval. With virtual time we
            // advance partway through the interval, inject a reset, and prove the next typing does
            // NOT fire at the original deadline but only after a fresh full interval from the reset.
            var time = new SignalingTimeProvider();
            var adapter = new SignalingTestAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 400, intervalMs: 400), time)!;
            worker.Start();

            // First typing fires after the initial delay.
            await AwaitSignal(time.TimerArmed, "initial-delay timer armed");
            time.Advance(TimeSpan.FromMilliseconds(400));
            await AwaitSignal(adapter.TypingSent, "first typing");
            var countBeforeReset = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(1, countBeforeReset);

            // Worker now arms the interval timer (would fire after another 400ms).
            await AwaitSignal(time.TimerArmed, "interval timer armed");

            // Advance partway (200ms of the 400ms interval), then reset.
            time.Advance(TimeSpan.FromMilliseconds(200));
            await context.SendActivityAsync(PingEvent(), CancellationToken.None);

            // The reset makes the worker restart a fresh 400ms countdown.
            await AwaitSignal(time.TimerArmed, "restarted interval timer armed");

            // Advance the remaining 200ms of the ORIGINAL interval. Because it was reset, the
            // second typing must NOT have fired yet.
            time.Advance(TimeSpan.FromMilliseconds(200));
            var countMid = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(countBeforeReset, countMid);

            // Advance the remaining 200ms of the RESTARTED interval; now the second typing fires.
            time.Advance(TimeSpan.FromMilliseconds(200));
            await AwaitSignal(adapter.TypingSent, "second typing after reset");

            await worker.DisposeAsync();

            var countAfter = adapter.GetActivitySnapshot().Count(a => a.Type == ActivityTypes.Typing);
            Assert.Equal(2, countAfter);
        }

        [Fact]
        public async Task Start_ResetsInterval_WhenResetFiresDuringSend()
        {
            // Exercises the race where ResetInterval fires while SendTypingActivityAsync is
            // in-flight (i.e., outside WaitAsync). A gated adapter holds the send open so we can
            // deterministically inject a reset during the send window — no wall-clock delays.
            var time = new SignalingTimeProvider();
            var adapter = new GatedSendAdapter();
            var context = new TurnContext(adapter, MakeMessageActivity());
            var worker = TypingWorker.Create(
                context, MakeOptions(initialDelayMs: 100, intervalMs: 500), time)!;
            worker.Start();

            // Drive the first typing send and hold it open.
            await AwaitSignal(time.TimerArmed, "initial-delay timer armed");
            time.Advance(TimeSpan.FromMilliseconds(100));
            await AwaitSignal(adapter.SendStarted, "first typing send in-flight");

            // Fire a reset while the worker is inside SendTypingActivityAsync (outside WaitAsync).
            await context.SendActivityAsync(PingEvent(), CancellationToken.None);

            // Let the first send complete. The worker then enters WaitAsync, observes the pending
            // reset, and arms a fresh full 500ms interval.
            adapter.ReleaseSend();
            await AwaitSignal(adapter.SendCompleted, "first typing send completed");
            Assert.Equal(1, adapter.TypingSendCount);

            await AwaitSignal(time.TimerArmed, "post-reset interval timer armed");

            // The restarted interval has not elapsed yet — no second send.
            Assert.Equal(1, adapter.TypingSendCount);

            // Advance the full restarted interval to produce the second typing.
            time.Advance(TimeSpan.FromMilliseconds(500));
            await AwaitSignal(adapter.SendStarted, "second typing send in-flight");
            adapter.ReleaseSend();
            await AwaitSignal(adapter.SendCompleted, "second typing send completed");

            await worker.DisposeAsync();

            Assert.Equal(2, adapter.TypingSendCount);
        }

        /// <summary>
        /// A <see cref="TestAdapter"/> that holds each typing send open on a gate until the test
        /// releases it, letting tests deterministically control the "send in-flight" window
        /// without any wall-clock delay.
        /// </summary>
        private sealed class GatedSendAdapter : TestAdapter
        {
            private int _typingSendCount;
            private readonly SemaphoreSlim _sendStarted = new(0);
            private readonly SemaphoreSlim _sendCompleted = new(0);
            private readonly SemaphoreSlim _gate = new(0);

            public int TypingSendCount => Volatile.Read(ref _typingSendCount);

            /// <summary>Signaled when a typing send begins (before the gate is awaited).</summary>
            public SemaphoreSlim SendStarted => _sendStarted;

            /// <summary>Signaled after a typing send has passed the gate and been recorded.</summary>
            public SemaphoreSlim SendCompleted => _sendCompleted;

            /// <summary>Releases one held typing send.</summary>
            public void ReleaseSend() => _gate.Release();

            public GatedSendAdapter() : base(Microsoft.Agents.Core.Models.Channels.Test)
            {
            }

            public override async Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
            {
                foreach (var a in activities)
                {
                    if (a.Type == ActivityTypes.Typing)
                    {
                        _sendStarted.Release();
                        await _gate.WaitAsync(cancellationToken);
                        Interlocked.Increment(ref _typingSendCount);
                        _sendCompleted.Release();
                    }
                }

                return await base.SendActivitiesAsync(turnContext, activities, cancellationToken);
            }
        }

        /// <summary>
        /// A <see cref="TestAdapter"/> that signals <see cref="TypingSent"/> after each typing
        /// activity is recorded, allowing tests to await sends deterministically.
        /// </summary>
        private sealed class SignalingTestAdapter : TestAdapter
        {
            private readonly SemaphoreSlim _typingSent = new(0);

            public SemaphoreSlim TypingSent => _typingSent;

            public SignalingTestAdapter() : base(Microsoft.Agents.Core.Models.Channels.Test)
            {
            }

            public override async Task<ResourceResponse[]> SendActivitiesAsync(
                ITurnContext turnContext, IActivity[] activities, CancellationToken cancellationToken)
            {
                var result = await base.SendActivitiesAsync(turnContext, activities, cancellationToken);
                foreach (var a in activities)
                {
                    if (a.Type == ActivityTypes.Typing)
                    {
                        _typingSent.Release();
                    }
                }

                return result;
            }
        }

        /// <summary>
        /// A <see cref="TimeProvider"/> that wraps a <see cref="FakeTimeProvider"/> and signals
        /// <see cref="TimerArmed"/> each time a timer is created (i.e., each time the worker begins
        /// a delay). Tests await that signal before advancing virtual time, eliminating the
        /// classic race where a test advances the clock before the code under test has armed its
        /// next timer.
        /// </summary>
        private sealed class SignalingTimeProvider : TimeProvider
        {
            private readonly FakeTimeProvider _inner = new();
            private readonly SemaphoreSlim _timerArmed = new(0);

            public SemaphoreSlim TimerArmed => _timerArmed;

            public void Advance(TimeSpan delta) => _inner.Advance(delta);

            public override DateTimeOffset GetUtcNow() => _inner.GetUtcNow();

            public override long GetTimestamp() => _inner.GetTimestamp();

            public override long TimestampFrequency => _inner.TimestampFrequency;

            public override TimeZoneInfo LocalTimeZone => _inner.LocalTimeZone;

            public override ITimer CreateTimer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
            {
                var timer = _inner.CreateTimer(callback, state, dueTime, period);
                _timerArmed.Release();
                return timer;
            }
        }
    }
}
