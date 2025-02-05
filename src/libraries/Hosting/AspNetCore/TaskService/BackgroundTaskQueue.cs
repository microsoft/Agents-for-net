// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.TaskService
{
    /// <summary>
    /// Singleton queue, used to transfer a work item to the <see cref="HostedTaskService"/>.
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _workItems = new ConcurrentQueue<Func<CancellationToken, Task>>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        /// <summary>
        /// Enqueue a work item to be processed on a background thread.
        /// </summary>
        /// <param name="workItem">The work item to be enqueued for execution. Is defined as
        /// a function taking a cancellation token.</param>
        public void QueueBackgroundWorkItem(Func<CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _workItems.Enqueue(workItem);
            _signal.Release();
        }

        /// <summary>
        /// Wait for a signal of an enqueued work item to be processed.
        /// </summary>
        /// <param name="cancellationToken">CancellationToken used to cancel the wait.</param>
        /// <returns>A function taking a cancellation token that needs to be processed.
        /// </returns>
        public async Task<Func<CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            Func<CancellationToken, Task> dequeued;
            _workItems.TryDequeue(out dequeued);
            return dequeued;
        }
    }
}
