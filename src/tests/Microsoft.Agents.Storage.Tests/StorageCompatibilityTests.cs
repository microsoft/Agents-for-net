// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Storage.Tests
{
    public class StorageCompatibilityTests
    {
        [Fact]
        public void StorageCompatibility_AsV2_ReturnsExistingV2Implementation()
        {
            var storage = new MemoryStorage();

            var storageV2 = StorageCompatibility.AsV2(storage);

            Assert.Same(storage, storageV2);
        }

        [Fact]
        public void StorageCompatibility_AsV2_WithBaseStorage_ReturnsExistingV2Implementation()
        {
            IStorage storage = new MemoryStorage();

            var storageV2 = StorageCompatibility.AsV2(storage);

            Assert.Same(storage, storageV2);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_ReadAsync_ReturnsExplicitResults()
        {
            var storage = new FakeStorage();
            await storage.WriteAsync(new Dictionary<string, object>() { ["existing"] = new PocoItem() { Id = "1" } }, CancellationToken.None);

            var storageV2 = StorageCompatibility.AsV2(storage);
            var results = await storageV2.ReadAsync(["existing", "missing"], CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, results["existing"].Status);
            Assert.IsType<PocoItem>(results["existing"].Value);
            Assert.Equal(StorageOperationStatus.NotFound, results["missing"].Status);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_WriteAsync_UpsertWithoutOptions_Succeeds()
        {
            var storageV2 = StorageCompatibility.AsV2(new FakeStorage());

            var results = await storageV2.WriteAsync(new Dictionary<string, PocoItem>() { ["item"] = new PocoItem() { Id = "1" } }, CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, results["item"].Status);
            Assert.Null(results["item"].Version);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_WriteAsync_WithExpectedVersion_ThrowsNotSupported()
        {
            var storageV2 = StorageCompatibility.AsV2(new FakeStorage());

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => storageV2.WriteAsync(
                new Dictionary<string, PocoItem>() { ["item"] = new PocoItem() { Id = "1" } },
                new StorageWriteOptions() { ExpectedVersion = "abc" },
                CancellationToken.None));

            Assert.Contains(nameof(StorageWriteOptions.ExpectedVersion), exception.Message);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_WriteAsync_WithNonDefaultMode_ThrowsNotSupported()
        {
            var storageV2 = StorageCompatibility.AsV2(new FakeStorage());

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => storageV2.WriteAsync(
                new Dictionary<string, PocoItem>() { ["item"] = new PocoItem() { Id = "1" } },
                new StorageWriteOptions() { Mode = StorageWriteMode.CreateOnly },
                CancellationToken.None));

            Assert.Contains(nameof(StorageWriteOptions.Mode), exception.Message);
            Assert.Contains(nameof(StorageWriteMode.CreateOnly), exception.Message);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_WriteAsync_WithIStoreItem_Succeeds()
        {
            var storageV2 = StorageCompatibility.AsV2(new FakeStorage());

            var results = await storageV2.WriteAsync(
                new Dictionary<string, PocoStoreItem>() { ["item"] = new PocoStoreItem() { Id = "1" } },
                CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, results["item"].Status);
            Assert.Null(results["item"].Version);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_DeleteAsync_ReturnsExplicitResults()
        {
            var storage = new FakeStorage();
            await storage.WriteAsync(new Dictionary<string, object>() { ["existing"] = new PocoItem() { Id = "1" } }, CancellationToken.None);

            var storageV2 = StorageCompatibility.AsV2(storage);
            var results = await storageV2.DeleteAsync(["existing", "missing"], CancellationToken.None);

            Assert.Equal(StorageOperationStatus.Succeeded, results["existing"].Status);
            Assert.Equal(StorageOperationStatus.NotFound, results["missing"].Status);
        }

        [Fact]
        public async Task StorageCompatibility_AsV2_DeleteAsync_WithExpectedVersion_ThrowsNotSupported()
        {
            var storage = new FakeStorage();
            await storage.WriteAsync(new Dictionary<string, object>() { ["item"] = new PocoItem() { Id = "1" } }, CancellationToken.None);

            var storageV2 = StorageCompatibility.AsV2(storage);

            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => storageV2.DeleteAsync(
                ["item"],
                new StorageDeleteOptions() { ExpectedVersion = "abc" },
                CancellationToken.None));

            Assert.Contains(nameof(StorageDeleteOptions.ExpectedVersion), exception.Message);
        }

        private class FakeStorage : IStorage
        {
            private readonly Dictionary<string, object> _store = new();

            public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                foreach (var key in keys)
                {
                    _store.Remove(key);
                }

                return Task.CompletedTask;
            }

            public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
            {
                IDictionary<string, object> results = new Dictionary<string, object>();
                foreach (var key in keys)
                {
                    if (_store.TryGetValue(key, out var value))
                    {
                        results[key] = value;
                    }
                }

                return Task.FromResult(results);
            }

            public async Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                var results = await ReadAsync(keys, cancellationToken);
                IDictionary<string, TStoreItem> typedResults = new Dictionary<string, TStoreItem>();
                foreach (var result in results)
                {
                    if (result.Value is TStoreItem item)
                    {
                        typedResults[result.Key] = item;
                    }
                }

                return typedResults;
            }

            public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
            {
                foreach (var change in changes)
                {
                    _store[change.Key] = change.Value;
                }

                return Task.CompletedTask;
            }

            public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
            {
                Dictionary<string, object> changesAsObject = new(changes.Count);
                foreach (var change in changes)
                {
                    changesAsObject.Add(change.Key, change.Value);
                }

                return WriteAsync(changesAsObject, cancellationToken);
            }
        }
    }
}
