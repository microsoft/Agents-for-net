// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Singleton queue, used to transfer a work item to the <see cref="HostedTaskService"/>.
    /// </summary>
    internal class BackgroundTaskQueue : IBackgroundTaskQueue, IDisposable
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new();
        private readonly Subject<Unit> _signal = new();
        private bool _disposed;

        /// <summary>
        /// Enqueue a work item to be processed on a background thread.
        /// </summary>
        /// <param name="workItem">The work item to be enqueued for execution. Is defined as
        /// a function taking a cancellation token.</param>
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _workItems.Enqueue(workItem);
            _signal.OnNext(Unit.Default);
        }

        /// <summary>
        /// Wait for a signal of an enqueued work item to be processed.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken used to cancel the wait.</param>
        /// <returns>A function taking a cancellation token that needs to be processed.
        /// </returns>
        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            // Try to dequeue immediately if items are available
            if (_workItems.TryDequeue(out var workItem))
            {
                return workItem;
            }

            // Wait for a signal that an item was enqueued
            await _signal.FirstAsync().ToTask(cancellationToken).ConfigureAwait(false);

            _workItems.TryDequeue(out workItem);
            return workItem;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _signal.OnCompleted();
                    _signal.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
