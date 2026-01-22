// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// <see cref="BackgroundService"/> implementation used to process work items on background threads.
    /// See <see href="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice">BackgroundService</see> for more information.
    /// </summary>
    internal class HostedTaskService : BackgroundService
    {
        private readonly ILogger<HostedTaskService> _logger;
        private readonly BehaviorSubject<bool> _isAcceptingWork = new(true);
        private readonly ConcurrentDictionary<Func<CancellationToken, Task>, Task> _tasks = new();
        private readonly IBackgroundTaskQueue _taskQueue;
        private readonly int _shutdownTimeoutSeconds;

        /// <summary>
        /// Create a <see cref="HostedTaskService"/> instance for processing work on a background thread.
        /// </summary>
        /// <remarks>
        /// It is important to note that exceptions on the background thread are only logged in the <see cref="ILogger"/>.
        /// </remarks>
        /// <param name="taskQueue"><see cref="ActivityTaskQueue"/> implementation where tasks are queued to be processed.</param>
        /// <param name="logger"><see cref="ILogger"/> implementation, for logging including background thread exception information.</param>
        /// <param name="options"></param>
        public HostedTaskService(IBackgroundTaskQueue taskQueue, ILogger<HostedTaskService> logger, AdapterOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(taskQueue);

            _shutdownTimeoutSeconds = options != null ? options.ShutdownTimeoutSeconds : 60;
            _taskQueue = taskQueue;
            _logger = logger ?? NullLogger<HostedTaskService>.Instance;
        }

        /// <summary>
        /// Called by BackgroundService when the hosting service is shutting down.
        /// </summary>
        /// <param name="stoppingToken"><see cref="CancellationToken"/> sent from BackgroundService for shutdown.</param>
        /// <returns>The Task to be executed asynchronously.</returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Signal that we're no longer accepting work
            _isAcceptingWork.OnNext(false);
            _isAcceptingWork.OnCompleted();

            // Wait for currently running tasks, but only up to the timeout
            var timeout = TimeSpan.FromSeconds(_shutdownTimeoutSeconds);
            var activeTasks = _tasks.Values.ToArray();

            if (activeTasks.Length > 0)
            {
                await Task.WhenAny(
                    Task.WhenAll(activeTasks),
                    Task.Delay(timeout, stoppingToken)
                ).ConfigureAwait(false);
            }

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.{Environment.NewLine}", Environment.NewLine);

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);
                if (workItem != null)
                {
                    try
                    {
                        // Check if we're still accepting work
                        if (_isAcceptingWork.Value)
                        {
                            var task = GetTaskFromWorkItem(workItem, stoppingToken)
                                .ContinueWith(t =>
                                {
                                    // After the work item completes, clear the running tasks of all completed tasks.
                                    foreach (var kv in _tasks.Where(tsk => tsk.Value.IsCompleted))
                                    {
                                        _tasks.TryRemove(kv.Key, out Task removed);
                                    }
                                }, stoppingToken);

                            _tasks.TryAdd(workItem, task);
                        }
                        else
                        {
                            _logger.LogError("Work item not processed.  Server is shutting down.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing work item.");
                    }
                }
            }
        }

        private Task GetTaskFromWorkItem(Func<CancellationToken, Task> workItem, CancellationToken stoppingToken)
        {
            // Start the work item, and return the task
            return Task.Run(
                async () =>
                {
                    try
                    {
                        await workItem(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Agent Errors should be processed in the Adapter.OnTurnError.
                        _logger.LogError(ex, "Error occurred executing WorkItem.");
                    }
                }, stoppingToken);
        }

        public override void Dispose()
        {
            _isAcceptingWork.Dispose();
            base.Dispose();
        }
    }
}
