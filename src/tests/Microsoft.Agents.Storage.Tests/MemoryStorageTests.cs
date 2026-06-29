// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class MemoryStorageTests : StorageBaseTests, IDisposable
    {
        private IStorage storage;

        public MemoryStorageTests()
        {
            storage = new MemoryStorage();
        }

        public void Dispose()
        {
            storage = new MemoryStorage();
        }

        [Fact]
        public async Task MemoryStorage_ReadValidation()
        {
            await ReadValidation(storage);
        }

        [Fact]
        public async Task MemoryStorage_CreateObjectTest()
        {
            await CreateObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_ReadUnknownTest()
        {
            await ReadUnknownTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_UpdateObjectTest()
        {
            await UpdateObjectTest<Exception>(storage);
        }

        [Fact]
        public async Task MemoryStorage_DeleteObjectTest()
        {
            await DeleteObjectTest(storage);
        }

        [Fact]
        public async Task MemoryStorage_HandleCrazyKeys()
        {
            await HandleCrazyKeys(storage);
        }

        [Fact]
        public async Task Nested()
        {
            var storage = new MemoryStorage();

            var outer = new Outer()
            {
                State = new Dictionary<string, object> 
                { 
                    ["key1"] = new Inner() { Name = "inner" } 
                }
            };

            var changes = new Dictionary<string, object>
                {
                    { "change1", outer.State },
                };

            await storage.WriteAsync((IDictionary<string, object>)changes, default);
            var items = await storage.ReadAsync(new[] { "change1" }, default);

            Assert.NotEmpty(items);
        }

        [Fact]
        public async Task WriteAsync_ShouldThrowOnNullStoreItemChanges()
        {
            var storage = new MemoryStorage();

            await Assert.ThrowsAsync<ArgumentNullException>(() => storage.WriteAsync((IDictionary<string, StoreItem>)null, CancellationToken.None));
        }

        [Fact]
        public async Task ReadAsync_ShouldReturnWrittenStoreItem()
        {
            var storeItem = new StoreItem
            {
                Id = 1,
                Topic = "topic",
                ETag = "test",
            };

            var key = "key1";

            var changes = new Dictionary<string, StoreItem>
            {
                { key, storeItem }
            };

            var storage = new MemoryStorage();

            await storage.WriteAsync((IDictionary<string, StoreItem>)changes, CancellationToken.None);

            var readStoreItems = new Dictionary<string, StoreItem>(await storage.ReadAsync<StoreItem>([key], CancellationToken.None));

            Assert.NotNull(readStoreItems);
            Assert.Single(readStoreItems);
            Assert.Equal(storeItem.Id, readStoreItems[key].Id);
            Assert.Equal(storeItem.Topic, readStoreItems[key].Topic);
            Assert.NotNull(readStoreItems[key].ETag);
        }

        [Fact]
        public async Task MemoryStorageV2_ReadAsync_ReturnsExplicitResults()
        {
            IStorageV2 storageV2 = new MemoryStorage();

            var writeResults = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["existing"] = new PocoItem() { Id = "1", Count = 7 },
                },
                cancellationToken: CancellationToken.None);

            var readResults = await storageV2.ReadAsync(
                new List<string> { "existing", "missing" },
                cancellationToken: CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, writeResults["existing"].Status);
            Assert.NotNull(writeResults["existing"].Version);

            Assert.Equal(2, readResults.Count);
            Assert.Equal("existing", readResults["existing"].Key);
            Assert.Equal(StorageOperationStatus.Succeeded, readResults["existing"].Status);
            Assert.NotNull(readResults["existing"].Version);
            Assert.IsType<PocoItem>(readResults["existing"].Value);
            Assert.Equal("1", ((PocoItem)readResults["existing"].Value).Id);

            Assert.Equal("missing", readResults["missing"].Key);
            Assert.Equal(StorageOperationStatus.NotFound, readResults["missing"].Status);
            Assert.Null(readResults["missing"].Value);
            Assert.Null(readResults["missing"].Version);
        }

        [Fact]
        public async Task MemoryStorageV2_WriteAsync_CreateOnly_ReturnsConflictWhenItemExists()
        {
            IStorageV2 storageV2 = new MemoryStorage();

            var firstWrite = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "1" },
                },
                new StorageWriteOptions() { Mode = StorageWriteMode.CreateOnly },
                cancellationToken: CancellationToken.None);

            var secondWrite = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "2" },
                },
                new StorageWriteOptions() { Mode = StorageWriteMode.CreateOnly },
                cancellationToken: CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, firstWrite["item"].Status);
            Assert.Equal(StorageOperationStatus.Conflict, secondWrite["item"].Status);
            Assert.Equal(firstWrite["item"].Version, secondWrite["item"].Version);
        }

        [Fact]
        public async Task MemoryStorageV2_WriteAsync_Replace_UsesVersionCondition()
        {
            IStorageV2 storageV2 = new MemoryStorage();

            var initialWrite = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "1", Count = 1 },
                },
                cancellationToken: CancellationToken.None);

            var replaceWrite = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "1", Count = 2 },
                },
                new StorageWriteOptions() { Mode = StorageWriteMode.Replace, ExpectedVersion = initialWrite["item"].Version },
                cancellationToken: CancellationToken.None);

            var staleWrite = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "1", Count = 3 },
                },
                new StorageWriteOptions() { Mode = StorageWriteMode.Replace, ExpectedVersion = initialWrite["item"].Version },
                cancellationToken: CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, replaceWrite["item"].Status);
            Assert.NotEqual(initialWrite["item"].Version, replaceWrite["item"].Version);
            Assert.Equal(StorageOperationStatus.ConditionNotMet, staleWrite["item"].Status);
            Assert.Equal(replaceWrite["item"].Version, staleWrite["item"].Version);
        }

        [Fact]
        public async Task MemoryStorageV2_DeleteAsync_ReturnsExplicitOutcome()
        {
            IStorageV2 storageV2 = new MemoryStorage();

            var writeResults = await storageV2.WriteAsync(
                new Dictionary<string, object>
                {
                    ["item"] = new PocoItem() { Id = "1" },
                },
                cancellationToken: CancellationToken.None);

            var conditionalDelete = await storageV2.DeleteAsync(
                new List<string>
                {
                    "item",
                },
                new StorageDeleteOptions() { ExpectedVersion = "stale" },
                cancellationToken: CancellationToken.None);

            var successfulDelete = await storageV2.DeleteAsync(
                new List<string>
                {
                    "item",
                },
                new StorageDeleteOptions() { ExpectedVersion = writeResults["item"].Version },
                cancellationToken: CancellationToken.None);

            var missingDelete = await storageV2.DeleteAsync(
                new List<string>
                {
                    "item",
                },
                cancellationToken: CancellationToken.None);

            Assert.Equal(StorageOperationStatus.ConditionNotMet, conditionalDelete["item"].Status);
            Assert.Equal(StorageOperationStatus.Succeeded, successfulDelete["item"].Status);
            Assert.Equal(writeResults["item"].Version, successfulDelete["item"].Version);
            Assert.Equal(StorageOperationStatus.NotFound, missingDelete["item"].Status);
        }
    }

    class Inner
    {
        public string Name { get; set; }
    }

    class Outer
    {
        public IDictionary<string, object> State { get; set; }
    }
}
