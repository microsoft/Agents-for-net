// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue;
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

            queue.QueueBackgroundActivity(claims, activity, null);
            var waited = await queue.WaitForActivityAsync(CancellationToken.None);

            Assert.Equal(claims, waited.ClaimsIdentity);
            Assert.Equal(activity, waited.Activity);
        }
    }
}