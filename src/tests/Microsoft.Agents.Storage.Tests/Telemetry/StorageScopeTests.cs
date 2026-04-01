// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Agents.Core.Telemetry;
using Microsoft.Agents.Storage.Telemetry.Scopes;
using Xunit;

namespace Microsoft.Agents.Storage.Tests.Telemetry
{
    public class StorageScopeTests : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _startedActivities = new();
        private readonly List<Activity> _stoppedActivities = new();

        public StorageScopeTests()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == AgentsTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity => _startedActivities.Add(activity),
                ActivityStopped = activity => _stoppedActivities.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public void Dispose()
        {
            _listener.Dispose();
        }

        #region ScopeRead

        [Fact]
        public void ScopeRead_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeRead(3);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.storage.read", started.OperationName);
        }

        [Fact]
        public void ScopeRead_Callback_SetsOperationTag()
        {
            var scope = new ScopeRead(5);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("read", stopped.GetTagItem(TagNames.StorageOperation));
        }

        [Fact]
        public void ScopeRead_Callback_SetsKeyCountTag()
        {
            var scope = new ScopeRead(5);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(5, stopped.GetTagItem(TagNames.KeyCount));
        }

        [Fact]
        public void ScopeRead_SetError_SetsErrorStatus()
        {
            var scope = new ScopeRead(1);
            scope.SetError(new InvalidOperationException("read failed"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
            Assert.Equal("read failed", stopped.StatusDescription);
        }

        #endregion

        #region ScopeWrite

        [Fact]
        public void ScopeWrite_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeWrite(2);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.storage.write", started.OperationName);
        }

        [Fact]
        public void ScopeWrite_Callback_SetsOperationTag()
        {
            var scope = new ScopeWrite(2);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("write", stopped.GetTagItem(TagNames.StorageOperation));
        }

        [Fact]
        public void ScopeWrite_Callback_SetsKeyCountTag()
        {
            var scope = new ScopeWrite(2);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(2, stopped.GetTagItem(TagNames.KeyCount));
        }

        [Fact]
        public void ScopeWrite_SetError_SetsErrorStatus()
        {
            var scope = new ScopeWrite(1);
            scope.SetError(new InvalidOperationException("write failed"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region ScopeDelete

        [Fact]
        public void ScopeDelete_CreatesActivity_WithCorrectName()
        {
            using var scope = new ScopeDelete(1);

            var started = Assert.Single(_startedActivities);
            Assert.Equal("agents.storage.delete", started.OperationName);
        }

        [Fact]
        public void ScopeDelete_Callback_SetsOperationTag()
        {
            var scope = new ScopeDelete(4);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal("delete", stopped.GetTagItem(TagNames.StorageOperation));
        }

        [Fact]
        public void ScopeDelete_Callback_SetsKeyCountTag()
        {
            var scope = new ScopeDelete(4);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(4, stopped.GetTagItem(TagNames.KeyCount));
        }

        [Fact]
        public void ScopeDelete_SetError_SetsErrorStatus()
        {
            var scope = new ScopeDelete(1);
            scope.SetError(new InvalidOperationException("delete failed"));
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(ActivityStatusCode.Error, stopped.Status);
        }

        #endregion

        #region Key-count variants

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        public void ScopeRead_Callback_SetsCorrectKeyCount(int keyCount)
        {
            var scope = new ScopeRead(keyCount);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(keyCount, stopped.GetTagItem(TagNames.KeyCount));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(50)]
        public void ScopeWrite_Callback_SetsCorrectKeyCount(int keyCount)
        {
            var scope = new ScopeWrite(keyCount);
            scope.Dispose();

            var stopped = Assert.Single(_stoppedActivities);
            Assert.Equal(keyCount, stopped.GetTagItem(TagNames.KeyCount));
        }

        #endregion
    }
}
