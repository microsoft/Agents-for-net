// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Builder.Tests
{
    public class DynamicIntervalTextChunkBatcherTests
    {
        [Fact]
        public async Task TestBasicBatching()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(500);
            string text = string.Empty;
            var subscription = batcher.Subscribe(value => text = value);

            batcher.OnNext("this");
            await Task.Delay(600);
            batcher.OnNext(" is a ");
            await Task.Delay(600);
            batcher.OnNext("test");
            batcher.OnCompleted();

            Assert.Equal("this is a test", text);
            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestMultipleEmissions()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(300);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            batcher.OnNext("first");
            await Task.Delay(400);

            batcher.OnNext("second");
            await Task.Delay(400);

            batcher.OnNext("third");
            batcher.OnCompleted();

            Assert.True(emissions.Count >= 2);
            Assert.Contains("first", emissions[0]);
            Assert.Contains("third", emissions[emissions.Count - 1]);

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestDynamicIntervalChange()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(1000);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            batcher.OnNext("chunk1");

            // Change interval to shorter
            batcher.SetInterval(200);
            await Task.Delay(300);

            batcher.OnNext("chunk2");
            await Task.Delay(300);

            batcher.OnCompleted();

            Assert.True(emissions.Count >= 1);

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestOnCompletedFlushesRemainingText()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(5000); // Long interval
            string text = string.Empty;
            var subscription = batcher.Subscribe(value => text = value);

            batcher.OnNext("quick");
            batcher.OnNext(" flush");

            // Complete immediately without waiting for interval
            batcher.OnCompleted();

            // Give a small delay for the completion to process
            await Task.Delay(50);

            Assert.Equal("quick flush", text);

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestMultipleSubscribers()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(300);
            string text1 = string.Empty;
            string text2 = string.Empty;

            var subscription1 = batcher.Subscribe(value => text1 = value);
            var subscription2 = batcher.Subscribe(value => text2 = value);

            batcher.OnNext("shared");
            await Task.Delay(400);

            batcher.OnCompleted();

            Assert.Equal("shared", text1);
            Assert.Equal("shared", text2);

            subscription1.Dispose();
            subscription2.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestEmptyBatch()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(200);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            // Wait without adding any chunks
            await Task.Delay(300);

            batcher.OnCompleted();

            Assert.Empty(emissions);

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestBatchingAccumulation()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(400);
            string text = string.Empty;
            var subscription = batcher.Subscribe(value => text = value);

            // Add multiple chunks quickly (before interval expires)
            batcher.OnNext("a");
            batcher.OnNext("b");
            batcher.OnNext("c");

            await Task.Delay(500);

            Assert.Equal("abc", text);

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public void TestOnNextAfterCompleted()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(200);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            batcher.OnNext("before");
            batcher.OnCompleted();

            // This should be ignored
            batcher.OnNext("after");

            Assert.DoesNotContain("after", string.Join("", emissions));

            subscription.Dispose();
            batcher.Dispose();
        }

        [Fact]
        public async Task TestDispose()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(200);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            batcher.OnNext("test");
            batcher.Dispose();

            // Should not throw
            batcher.OnNext("after dispose");

            await Task.Delay(300);

            subscription.Dispose();
        }

        [Fact]
        public async Task TestConcatenationOrder()
        {
            var batcher = new DynamicIntervalTextChunkBatcher(300);
            var emissions = new List<string>();
            var subscription = batcher.Subscribe(emissions.Add);

            batcher.OnNext("1");
            batcher.OnNext("2");
            batcher.OnNext("3");

            await Task.Delay(400);

            batcher.OnNext("4");
            batcher.OnNext("5");

            batcher.OnCompleted();
            await Task.Delay(50);

            // Check that order is preserved and final emission has all text
            Assert.True(emissions.Count >= 1);
            Assert.Equal("12345", emissions[emissions.Count - 1]);

            subscription.Dispose();
            batcher.Dispose();
        }
    }
}
