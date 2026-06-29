// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage
{
    internal static class StorageCompatibility
    {
        public static IStorageV2 AsV2(IStorage storage)
        {
            AssertionHelpers.ThrowIfNull(storage, nameof(storage));

            if (storage is IStorageV2 storageV2)
            {
                return storageV2;
            }

            return new StorageV1ToV2Adapter(storage);
        }
    }

    internal sealed class StorageV1ToV2Adapter : IStorageV2
    {
        private readonly IStorage _storage;

        public StorageV1ToV2Adapter(IStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            return _storage.ReadAsync(keys, cancellationToken);
        }

        public Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            return _storage.ReadAsync<TStoreItem>(keys, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, StorageReadResult>> ReadAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            if (keys.Count == 0)
            {
                return new Dictionary<string, StorageReadResult>();
            }

            string[] keysArray = new string[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                AssertionHelpers.ThrowIfNullOrWhiteSpace(keys[index], nameof(keys));
                keysArray[index] = keys[index];
            }

            var items = await _storage.ReadAsync(keysArray, cancellationToken).ConfigureAwait(false);

            Dictionary<string, StorageReadResult> results = new(keys.Count);
            foreach (var key in keysArray)
            {
                if (items.TryGetValue(key, out var value))
                {
                    results[key] = new StorageReadResult()
                    {
                        Key = key,
                        Status = StorageOperationStatus.Succeeded,
                        Value = value,
                        Version = (value as IStoreItem)?.ETag,
                    };
                }
                else
                {
                    results[key] = new StorageReadResult()
                    {
                        Key = key,
                        Status = StorageOperationStatus.NotFound,
                    };
                }
            }

            return results;
        }

        public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken = default)
        {
            return _storage.WriteAsync(changes, cancellationToken);
        }

        public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            return _storage.WriteAsync(changes, cancellationToken);
        }

        public Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, CancellationToken cancellationToken = default) where TValue : class
        {
            return WriteAsync(changes, options: null, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, StorageWriteOptions options, CancellationToken cancellationToken = default) where TValue : class
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            if (options != null)
            {
                List<string> unsupportedOptions = new();
                if (options.Mode != StorageWriteMode.Upsert)
                {
                    unsupportedOptions.Add($"{nameof(StorageWriteOptions.Mode)}={options.Mode}");
                }

                if (options.ExpectedVersion != null)
                {
                    unsupportedOptions.Add(nameof(StorageWriteOptions.ExpectedVersion));
                }

                if (unsupportedOptions.Count > 0)
                {
                    throw new NotSupportedException($"StorageCompatibility does not support {string.Join(", ", unsupportedOptions)}. Handle optimistic concurrency and create-only/replace semantics outside this adapter.");
                }
            }

            if (changes.Count == 0)
            {
                return new Dictionary<string, StorageWriteResult>();
            }

            Dictionary<string, object> changesAsObject = new(changes.Count);
            foreach (var change in changes)
            {
                AssertionHelpers.ThrowIfNullOrWhiteSpace(change.Key, nameof(changes));
                changesAsObject.Add(change.Key, change.Value);
            }

            await _storage.WriteAsync(changesAsObject, cancellationToken).ConfigureAwait(false);

            Dictionary<string, StorageWriteResult> results = new(changes.Count);
            foreach (var change in changes)
            {
                results[change.Key] = new StorageWriteResult()
                {
                    Key = change.Key,
                    Status = StorageOperationStatus.Succeeded,
                };
            }

            return results;
        }

        public Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(keys, options: null, cancellationToken);
        }

        public Task DeleteAsync(string[] keys, CancellationToken cancellationToken = default)
        {
            return _storage.DeleteAsync(keys, cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, StorageDeleteOptions options, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            if (!string.IsNullOrEmpty(options?.ExpectedVersion))
            {
                throw new NotSupportedException($"StorageCompatibility does not support {nameof(StorageDeleteOptions.ExpectedVersion)}. Handle optimistic concurrency outside this adapter.");
            }

            if (keys.Count == 0)
            {
                return new Dictionary<string, StorageDeleteResult>();
            }

            string[] keysArray = new string[keys.Count];
            for (int index = 0; index < keys.Count; index++)
            {
                AssertionHelpers.ThrowIfNullOrWhiteSpace(keys[index], nameof(keys));
                keysArray[index] = keys[index];
            }

            var existingItems = await _storage.ReadAsync(keysArray, cancellationToken).ConfigureAwait(false);
            await _storage.DeleteAsync(keysArray, cancellationToken).ConfigureAwait(false);

            Dictionary<string, StorageDeleteResult> results = new(keys.Count);
            foreach (var key in keysArray)
            {
                results[key] = new StorageDeleteResult()
                {
                    Key = key,
                    Status = existingItems.ContainsKey(key) ? StorageOperationStatus.Succeeded : StorageOperationStatus.NotFound,
                    Version = existingItems.TryGetValue(key, out var value) ? (value as IStoreItem)?.ETag : null,
                };
            }

            return results;
        }
    }
}
