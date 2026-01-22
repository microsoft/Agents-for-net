// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.Testing;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests.BackgroundActivityService
{
    public class ActivityTaskQueueTests
    {
        [Fact]
        public async Task WaitForActivityAsync_ShouldResolveQueuedActivity()
        {
            var queue = new ActivityTaskQueue();
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var adapter = new TestAdapter();

            queue.QueueBackgroundActivity(claims, adapter, activity);
            var waited = await queue.WaitForActivityAsync(CancellationToken.None);

            Assert.Equal(claims, waited.ClaimsIdentity);
            Assert.Equal(activity, waited.Activity);
        }

        [Fact]
        public void Stop_ShouldPreventFurtherQueueing()
        {
            var queue = new ActivityTaskQueue();
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var adapter = new TestAdapter();

            // Queue one activity before stopping
            var result1 = queue.QueueBackgroundActivity(claims, adapter, activity);
            Assert.True(result1);

            // Stop the queue (don't wait for empty since we have an item)
            queue.Stop(waitForEmpty: false);

            // Attempt to queue after stopping should return false
            var result2 = queue.QueueBackgroundActivity(claims, adapter, activity);
            Assert.False(result2);
        }

        [Fact]
        public async Task Stop_ConcurrentAccess_ShouldBeThreadSafe()
        {
            // Test that concurrent Stop and QueueBackgroundActivity calls are thread-safe
            const int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                var queue = new ActivityTaskQueue();
                var claims = new ClaimsIdentity();
                var activity = new Activity();
                var adapter = new TestAdapter();

                var queueResults = new List<bool>();
                var stopCompleted = false;
                var queueTasks = new List<Task>();

                // Start multiple queue operations and a stop operation concurrently
                var stopTask = Task.Run(() =>
                {
                    queue.Stop(waitForEmpty: false);
                    stopCompleted = true;
                });

                for (int j = 0; j < 10; j++)
                {
                    queueTasks.Add(Task.Run(() =>
                    {
                        var result = queue.QueueBackgroundActivity(claims, adapter, activity);
                        lock (queueResults)
                        {
                            queueResults.Add(result);
                        }
                    }));
                }

                await Task.WhenAll(queueTasks);
                await stopTask;

                Assert.True(stopCompleted);
                // After stop, any subsequent queues should return false
                var afterStopResult = queue.QueueBackgroundActivity(claims, adapter, activity);
                Assert.False(afterStopResult);
            }
        }

        [Fact]
        public async Task Stop_WithWaitForEmpty_ShouldWaitForQueueToDrain()
        {
            var queue = new ActivityTaskQueue();
            var claims = new ClaimsIdentity();
            var activity = new Activity();
            var adapter = new TestAdapter();

            // Queue an activity
            queue.QueueBackgroundActivity(claims, adapter, activity);

            // Start draining in background
            var drainTask = Task.Run(async () =>
            {
                await Task.Delay(50); // Small delay to ensure Stop is waiting
                await queue.WaitForActivityAsync(CancellationToken.None);
            });

            // Stop with wait - should block until queue is drained
            var stopTask = Task.Run(() => queue.Stop(waitForEmpty: true));

            await Task.WhenAll(drainTask, stopTask);

            // Verify queue is now stopped
            var result = queue.QueueBackgroundActivity(claims, adapter, activity);
            Assert.False(result);
        }
    }
}