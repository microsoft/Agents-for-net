// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.HeaderPropagation;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// <see cref="BackgroundService"/> implementation used to process activities with claims.
    ///  <see href="https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice">More information.</see>
    /// </summary>
    internal class HostedActivityService : BackgroundService
    {
        private readonly ILogger<HostedActivityService> _logger;
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly ConcurrentDictionary<ActivityWithClaims, Task> _activitiesProcessing = new();
        private readonly IActivityTaskQueue _activityQueue;
        private readonly int _shutdownTimeoutSeconds;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Create a <see cref="HostedActivityService"/> instance for processing Activities
        /// on background threads.
        /// </summary>
        /// <remarks>
        /// It is important to note that exceptions on the background thread are only logged in the <see cref="ILogger"/>.
        /// </remarks>
        /// <param name="provider"></param>
        /// <param name="config"><see cref="IConfiguration"/> used to retrieve ShutdownTimeoutSeconds from appsettings.</param>
        /// <param name="activityTaskQueue"><see cref="ActivityTaskQueue"/>Queue of activities to be processed.  This class
        /// contains a semaphore which the BackgroundService waits on to be notified of activities to be processed.</param>
        /// <param name="logger">Logger to use for logging BackgroundService processing and exception information.</param>
        /// <param name="options"></param>
        public HostedActivityService(IServiceProvider provider, IConfiguration config, IActivityTaskQueue activityTaskQueue, ILogger<HostedActivityService> logger, AdapterOptions options = null)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(activityTaskQueue);
            ArgumentNullException.ThrowIfNull(provider);

            _shutdownTimeoutSeconds = options != null ? options.ShutdownTimeoutSeconds : 60;
            _activityQueue = activityTaskQueue;
            _logger = logger ?? NullLogger<HostedActivityService>.Instance;
            _serviceProvider = provider;
        }

        /// <summary>
        /// Called by BackgroundService when the hosting service is shutting down.
        /// </summary>
        /// <param name="stoppingToken"><see cref="CancellationToken"/> sent from BackgroundService for shutdown.</param>
        /// <returns>The Task to be executed asynchronously.</returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is stopping.");

            // Obtain a write lock and do not release it, preventing new tasks from starting
            if (_lock.TryEnterWriteLock(TimeSpan.FromSeconds(_shutdownTimeoutSeconds)))
            {
                // Wait for currently running tasks, but only n seconds.
                await Task.WhenAny(Task.WhenAll(_activitiesProcessing.Values), Task.Delay(TimeSpan.FromSeconds(_shutdownTimeoutSeconds), stoppingToken));
            }

            await base.StopAsync(stoppingToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Queued Hosted Service is running.");

            await BackgroundProcessing(stoppingToken);
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var activityWithClaims = await _activityQueue.WaitForActivityAsync(stoppingToken);
                if (activityWithClaims != null)
                {
                    try
                    {
                        // The read lock will not be acquirable if the app is shutting down.
                        // New tasks should not be starting during shutdown.
                        if (_lock.TryEnterReadLock(500))
                        {
                            // Create the task which will execute the work item.
                            var task = GetTaskFromWorkItem(activityWithClaims, stoppingToken)
                                .ContinueWith(t =>
                                {
                                    // After the work item completes, clear the running tasks of all completed tasks.
                                    foreach (var kv in _activitiesProcessing.Where(tsk => tsk.Value.IsCompleted))
                                    {
                                        _activitiesProcessing.TryRemove(kv.Key, out Task removed);
                                    }
                                }, stoppingToken);

                            _activitiesProcessing.TryAdd(activityWithClaims, task);
                        }
                        else
                        {
                            _logger.LogError("Work item for '{ConversationId}' not processed.  Server is shutting down?", activityWithClaims.Activity.Conversation.Id);
                        }
                    }
                    finally
                    {
                        _lock.ExitReadLock();
                    }
                }
            }
        }

        private Task GetTaskFromWorkItem(ActivityWithClaims activityWithClaims, CancellationToken stoppingToken)
        {
            // Start the work item, and return the task
            return Task.Run(
                async () =>
            {
                try
                {
                    // We must go back through DI to get the IAgent. This is because the IAgent is typically transient, and anything
                    // else that is transient as part of the Agent, that uses IServiceProvider will encounter error since that is scoped
                    // and disposed before this gets called.
                    var agent = _serviceProvider.GetService(activityWithClaims.AgentType ?? typeof(IAgent));
                    HeaderPropagationContext.HeadersFromRequest = activityWithClaims.Headers;

                    if (activityWithClaims.IsProactive)
                    {
                        await activityWithClaims.ChannelAdapter.ProcessProactiveAsync(
                            activityWithClaims.ClaimsIdentity,
                            activityWithClaims.Activity,
                            activityWithClaims.ProactiveAudience ?? AgentClaims.GetTokenAudience(activityWithClaims.ClaimsIdentity),
                            ((IAgent)agent).OnTurnAsync, 
                            stoppingToken).ConfigureAwait(false);
                    }
                    else
                    {
                        var response = await activityWithClaims.ChannelAdapter.ProcessActivityAsync(
                            activityWithClaims.ClaimsIdentity, 
                            activityWithClaims.Activity,
                            ((IAgent)agent).OnTurnAsync, 
                            stoppingToken).ConfigureAwait(false);

                        activityWithClaims.OnComplete?.Invoke(response);
                    }
                }
                catch (Exception ex)
                {
                    // Agent Errors should be processed in the Adapter.OnTurnError.  Unlikely this will be hit.
                    _logger.LogError(ex, "Error occurred executing WorkItem.");

                    InvokeResponse invokeResponse = null;
                    if (activityWithClaims.Activity.IsType(ActivityTypes.Invoke))
                    {
                        invokeResponse = new InvokeResponse() {  Status = (int)HttpStatusCode.InternalServerError };
                    }

                    if (activityWithClaims.OnComplete != null)
                    {
                        await activityWithClaims.OnComplete(invokeResponse).ConfigureAwait(false);
                    }
                }
            }, stoppingToken);
        }
    }
}
