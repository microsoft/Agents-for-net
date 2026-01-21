// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class DynamicIntervalTextChunkBatcherTests
    {
        [Fact]
        public async Task BatcherAccumulatesTextChunks()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(100);
            batcher.Subscribe(text => emissions.Add(text));

            // Act
            batcher.OnNext("Hello");
            batcher.OnNext(" World");
            await Task.Delay(250);

            // Assert
            Assert.Contains("Hello World", emissions);
        }

        [Fact]
        public async Task BatcherEmitsOnInterval()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(100);
            batcher.Subscribe(text => emissions.Add(text));

            // Act
            batcher.OnNext("Chunk1");
            await Task.Delay(200);
            batcher.OnNext("Chunk2");
            await Task.Delay(200);

            // Assert
            Assert.True(emissions.Count >= 2, $"Expected at least 2 emissions, got {emissions.Count}");
            Assert.Contains("Chunk1", emissions);
            Assert.Contains("Chunk1Chunk2", emissions);
        }

        [Fact]
        public async Task BatcherIgnoresEmptyText()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(100);
            batcher.Subscribe(text => emissions.Add(text));

            // Act
            batcher.OnNext("");
            await Task.Delay(200);

            // Assert - No emissions for empty text
            Assert.DoesNotContain("", emissions);
        }

        [Fact]
        public async Task BatcherUsesDistinctUntilChanged()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(50);
            batcher.Subscribe(text => emissions.Add(text));

            // Act - Send one chunk and wait multiple intervals
            batcher.OnNext("Test");
            await Task.Delay(300); // Multiple intervals pass

            // Assert - Should only have one emission of "Test" since text doesn't change
            var testCount = emissions.FindAll(e => e == "Test").Count;
            Assert.Equal(1, testCount);
        }

        [Fact]
        public void BatcherCompletesCorrectly()
        {
            // Arrange
            var completed = false;
            using var batcher = new DynamicIntervalTextChunkBatcher(50);
            batcher.Subscribe(
                _ => { },
                _ => { },
                () => completed = true);

            // Act
            batcher.OnNext("Test");
            batcher.OnCompleted();

            // Assert
            Assert.True(completed);
        }

        [Fact]
        public void BatcherIgnoresInputAfterCompletion()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(50);
            batcher.Subscribe(text => emissions.Add(text));

            // Act
            batcher.OnNext("Before");
            batcher.OnCompleted();
            batcher.OnNext("After"); // Should be ignored

            // Assert - "After" should not appear in any emission
            Assert.DoesNotContain(emissions, e => e.Contains("After"));
        }

        [Fact]
        public async Task BatcherAllowsDynamicIntervalChange()
        {
            // Arrange
            var emissions = new List<(string text, DateTime time)>();
            using var batcher = new DynamicIntervalTextChunkBatcher(200);
            batcher.Subscribe(text => emissions.Add((text, DateTime.Now)));

            // Act
            batcher.OnNext("A");
            await Task.Delay(100);
            batcher.SetInterval(50); // Speed up the interval
            batcher.OnNext("B");
            await Task.Delay(200);

            // Assert
            Assert.True(emissions.Count >= 1);
            Assert.Contains(emissions, e => e.text.Contains("B"));
        }

        [Fact]
        public void DisposeStopsProcessing()
        {
            // Arrange
            var emissions = new List<string>();
            var batcher = new DynamicIntervalTextChunkBatcher(50);
            batcher.Subscribe(text => emissions.Add(text));

            // Act
            batcher.OnNext("Before");
            batcher.Dispose();
            batcher.OnNext("After"); // Should be ignored

            // Assert - No exception thrown and "After" should not appear
            Assert.DoesNotContain(emissions, e => e.Contains("After"));
        }

        [Fact]
        public async Task LateSubscriberGetsLatestValue()
        {
            // Arrange
            using var batcher = new DynamicIntervalTextChunkBatcher(100);

            // First subscriber
            var firstEmissions = new List<string>();
            batcher.Subscribe(text => firstEmissions.Add(text));

            batcher.OnNext("Initial");
            await Task.Delay(200);

            // Act - Late subscriber
            var lateEmissions = new List<string>();
            batcher.Subscribe(text => lateEmissions.Add(text));
            await Task.Delay(100);

            // Assert - Late subscriber should get the latest value due to ReplaySubject(1)
            Assert.True(lateEmissions.Count >= 1);
            Assert.Contains("Initial", lateEmissions);
        }

        [Fact]
        public async Task BatcherHandlesRapidChunks()
        {
            // Arrange
            var emissions = new List<string>();
            using var batcher = new DynamicIntervalTextChunkBatcher(100);
            batcher.Subscribe(text => emissions.Add(text));

            // Act - Send many chunks rapidly
            for (int i = 0; i < 10; i++)
            {
                batcher.OnNext($"Chunk{i}");
            }
            await Task.Delay(300);

            // Assert - Should have accumulated all chunks
            var lastEmission = emissions[^1];
            Assert.Contains("Chunk0", lastEmission);
            Assert.Contains("Chunk9", lastEmission);
        }
    }
}
