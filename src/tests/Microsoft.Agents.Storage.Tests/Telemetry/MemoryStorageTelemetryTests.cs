// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.TestSupport;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests.Telemetry
{
    [Collection("TelemetryTests")]
    public class MemoryStorageTelemetryTests : TelemetryScopeTestBase
    {
        [Fact]
        public async Task MemoryStorage_ReadAsync_CreatesTelemetryActivity()
        {
            IStorage storage = new MemoryStorage();

            await storage.ReadAsync(["missing"], CancellationToken.None);

            var started = Assert.Single(StartedActivities);
            var stopped = Assert.Single(StoppedActivities);

            Assert.Equal("agents.storage.read", started.OperationName);
            Assert.Equal("read", stopped.GetTagItem(TagNames.StorageOperation));
            Assert.Equal(1, stopped.GetTagItem(TagNames.KeyCount));
        }
    }
}