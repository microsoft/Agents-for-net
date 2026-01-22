// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// A lock-free text chunk batcher that uses Rx to batch incoming text chunks
    /// and emit accumulated text at configurable intervals.
    /// </summary>
    /// <remarks>
    /// This class encapsulates Rx-based text batching to eliminate the need for locks
    /// when accumulating text chunks in streaming scenarios. It:
    /// - Accumulates incoming text chunks using Scan operator
    /// - Emits the accumulated text at configurable intervals
    /// - Supports dynamic interval changes via SetInterval
    /// - Uses DistinctUntilChanged to prevent redundant emissions
    /// - Uses ReplaySubject to allow late subscribers to get the latest value
    /// </remarks>
    internal class DynamicIntervalTextChunkBatcher : IObservable<string>, IDisposable
    {
        private readonly Subject<string> _inputSubject = new();
        private readonly BehaviorSubject<int> _intervalSubject;
        private readonly ReplaySubject<string> _outputSubject = new(1);
        private readonly IDisposable _subscription;
        private volatile bool _isCompleted;
        private volatile bool _disposed;

        /// <summary>
        /// Creates a new instance of the <see cref="DynamicIntervalTextChunkBatcher"/> class.
        /// </summary>
        /// <param name="intervalMs">The initial interval in milliseconds at which accumulated text is emitted.</param>
        public DynamicIntervalTextChunkBatcher(int intervalMs)
        {
            _intervalSubject = new BehaviorSubject<int>(intervalMs);

            // Build the Rx pipeline:
            // 1. Scan: Accumulate chunks into immutable strings (thread-safe)
            // 2. CombineLatest + SwitchMap: Pair with interval timer
            // 3. DistinctUntilChanged: Only emit when text changes
            // 4. Publish().RefCount(): Share subscription
            var outputObservable = _inputSubject
                .Scan(string.Empty, (accumulated, chunk) => accumulated + chunk)
                .CombineLatest(
                    _intervalSubject.Select(interval =>
                        Observable.Interval(TimeSpan.FromMilliseconds(interval))
                            .StartWith(0L))
                        .Switch(),
                    (text, _) => text)
                .DistinctUntilChanged()
                .Where(text => !string.IsNullOrEmpty(text))
                .Publish()
                .RefCount();

            _subscription = outputObservable.Subscribe(
                text => _outputSubject.OnNext(text),
                ex => _outputSubject.OnError(ex),
                () => { }
            );
        }

        /// <summary>
        /// Dynamically changes the emission interval.
        /// </summary>
        /// <param name="intervalMs">The new interval in milliseconds.</param>
        public void SetInterval(int intervalMs)
        {
            if (!_disposed && !_isCompleted)
            {
                _intervalSubject.OnNext(intervalMs);
            }
        }

        /// <summary>
        /// Submits a text chunk to be accumulated and batched.
        /// </summary>
        /// <param name="text">The text chunk to add.</param>
        public void OnNext(string text)
        {
            if (!_isCompleted && !_disposed)
            {
                _inputSubject.OnNext(text);
            }
        }

        /// <summary>
        /// Signals completion of the input stream.
        /// </summary>
        public void OnCompleted()
        {
            if (!_isCompleted && !_disposed)
            {
                _isCompleted = true;
                _inputSubject.OnCompleted();
                _intervalSubject.OnCompleted();
                _outputSubject.OnCompleted();
            }
        }

        /// <summary>
        /// Subscribes an observer to receive accumulated text emissions.
        /// </summary>
        /// <param name="observer">The observer to subscribe.</param>
        /// <returns>A disposable to unsubscribe.</returns>
        public IDisposable Subscribe(IObserver<string> observer)
        {
            return _outputSubject.Subscribe(observer);
        }

        /// <summary>
        /// Disposes of all resources used by the batcher.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _subscription?.Dispose();
                _inputSubject?.Dispose();
                _intervalSubject?.Dispose();
                _outputSubject?.Dispose();
            }
        }
    }
}
