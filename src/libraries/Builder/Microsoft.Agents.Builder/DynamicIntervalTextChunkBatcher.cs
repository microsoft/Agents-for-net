// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// An observable that batches incoming string chunks and emits the accumulated text
    /// at a configurable interval.
    /// </summary>
    internal class DynamicIntervalTextChunkBatcher : IObservable<string>, IDisposable
    {
        private readonly Subject<string> _inputSubject = new Subject<string>();
        private readonly BehaviorSubject<int> _intervalSubject;
        private readonly IObservable<string> _outputObservable;
        private readonly IDisposable _subscription;
        private readonly ReplaySubject<string> _outputSubject = new ReplaySubject<string>(1);
        private bool _isCompleted;
        private bool _disposed;

        public DynamicIntervalTextChunkBatcher(int intervalMs)
        {
            _intervalSubject = new BehaviorSubject<int>(intervalMs);

            // Create observable that accumulates chunks and emits on interval changes
            _outputObservable = _inputSubject
                .Scan(new StringBuilder(), (buffer, chunk) =>
                {
                    buffer.Append(chunk);
                    return buffer;
                })
                .CombineLatest(
                    _intervalSubject.Select(interval =>
                        Observable.Interval(TimeSpan.FromMilliseconds(interval), Scheduler.Default)
                            .StartWith(0L)),
                    (buffer, _) => buffer.ToString())
                .DistinctUntilChanged()
                .Where(text => !string.IsNullOrEmpty(text))
                .Publish()
                .RefCount();

            // Subscribe to drive the observable and store in output subject
            _subscription = _outputObservable.Subscribe(
                text => _outputSubject.OnNext(text),
                _outputSubject.OnError,
                () => { }
            );
        }

        /// <summary>
        /// Sets the interval for emitting batched text.
        /// </summary>
        /// <param name="intervalMs">The interval in milliseconds.</param>
        public void SetInterval(int intervalMs)
        {
            if (!_disposed)
            {
                _intervalSubject.OnNext(intervalMs);
            }
        }

        /// <summary>
        /// Adds a text chunk to the buffer.
        /// </summary>
        /// <param name="text">The text chunk to add.</param>
        public void OnNext(string text)
        {
            if (_isCompleted || _disposed)
            {
                return;
            }

            _inputSubject.OnNext(text);
        }

        /// <summary>
        /// Completes the batching and emits any remaining text.
        /// </summary>
        public void OnCompleted()
        {
            if (_isCompleted || _disposed)
            {
                return;
            }

            _isCompleted = true;
            _inputSubject.OnCompleted();
            _intervalSubject.OnCompleted();
            _outputSubject.OnCompleted();
        }

        /// <summary>
        /// Subscribes an observer to receive batched text emissions.
        /// </summary>
        public IDisposable Subscribe(IObserver<string> observer)
        {
            return _outputSubject.Subscribe(observer);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _subscription?.Dispose();
            _inputSubject?.Dispose();
            _intervalSubject?.Dispose();
            _outputSubject?.Dispose();
        }
    }
}
