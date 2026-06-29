// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage.Telemetry.Scopes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage
{
    /// <summary>
    /// A storage layer that uses an in-memory dictionary.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="Microsoft.Agents.Storage.MemoryStorage"/> class.
    /// </remarks>
    /// <param name="jsonSerializer">Optional: JsonSerializerOptions.</param>
    /// <param name="dictionary">Optional: A pre-existing dictionary to use. Or null to use a new one.</param>
    public class MemoryStorage(JsonSerializerOptions jsonSerializer = null, Dictionary<string, JsonObject> dictionary = null) : IStorageV2
    {
        private const string ETagPropertyName = "ETag";

        // If a JsonSerializer is not provided during construction, this will be the default static JsonSerializer.
        private readonly JsonSerializerOptions _stateJsonSerializer = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;
        private readonly Dictionary<string, JsonObject> _memory = dictionary ?? [];
        private readonly object _syncroot = new();
        private int _eTag = 0;

        /// <summary>
        /// Deletes storage items from storage.
        /// </summary>
        /// <param name="keys">Keys for the <see cref="Microsoft.Agents.Storage.IStoreItem"/> objects to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.ReadAsync(string[], System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.WriteAsync(System.Collections.Generic.IDictionary{string, object}, System.Threading.CancellationToken)"/>
        public Task DeleteAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            using var telemetryScope = new ScopeDelete(keys.Length);

            lock (_syncroot)
            {
                foreach (var key in keys)
                {
                    _memory.Remove(key);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Reads storage items from storage.
        /// </summary>
        /// <param name="keys">Keys of the <see cref="Microsoft.Agents.Storage.IStoreItem"/> objects to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// the items read, indexed by key.</remarks>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.DeleteAsync(string[], System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.WriteAsync(System.Collections.Generic.IDictionary{string, object}, System.Threading.CancellationToken)"/>
        public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            using var telemetryScope = new ScopeRead(keys.Length);

            var storeItems = new Dictionary<string, object>(keys.Length);
            lock (_syncroot)
            {
                foreach (var key in keys)
                {
                    if (_memory.TryGetValue(key, out var state))
                    {
                        storeItems.Add(key, DeserializeState(state));
                    }
                }
            }

            return Task.FromResult<IDictionary<string, object>>(storeItems);
        }

        //<inheritdoc/>
        public Task<IReadOnlyDictionary<string, StorageReadResult>> ReadAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            using var telemetryScope = new ScopeRead(keys.Count);

            Dictionary<string, StorageReadResult> results = new(keys.Count);
            lock (_syncroot)
            {
                foreach (var key in keys)
                {
                    AssertionHelpers.ThrowIfNullOrWhiteSpace(key, nameof(key));

                    if (_memory.TryGetValue(key, out var state))
                    {
                        results[key] = new StorageReadResult()
                        {
                            Key = key,
                            Status = StorageOperationStatus.Succeeded,
                            Value = DeserializeState(state),
                            Version = GetStateETag(state),
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
            }

            return Task.FromResult<IReadOnlyDictionary<string, StorageReadResult>>(results);
        }

        //<inheritdoc/>
        public async Task<IDictionary<string, TStoreItem>> ReadAsync<TStoreItem>(string[] keys, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            var storeItems = await ReadAsync(keys, cancellationToken).ConfigureAwait(false);
            var values = new Dictionary<string, TStoreItem>(keys.Length);
            foreach (var entry in storeItems)
            {
                if (entry.Value is TStoreItem valueAsType)
                {
                    values.Add(entry.Key, valueAsType);
                }
            }
            return values;
        }

        /// <summary>
        /// Writes storage items to storage.
        /// </summary>
        /// <param name="changes">The items to write, indexed by key.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute. Throws ArgumentException for an ETag conflict.</returns>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.DeleteAsync(string[], System.Threading.CancellationToken)"/>
        /// <seealso cref="Microsoft.Agents.Storage.MemoryStorage.ReadAsync(string[], System.Threading.CancellationToken)"/>
        public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            using var telemetryScope = new ScopeWrite(changes.Count);

            lock (_syncroot)
            {
                foreach (var change in changes)
                {
                    var newValue = change.Value;

                    var oldStateETag = _memory.TryGetValue(change.Key, out var oldState)
                        ? GetStateETag(oldState)
                        : null;

                    string newStateETag = null;

                    // Set ETag if applicable
                    if (newValue is IStoreItem newStoreItem)
                    {
                        if (oldStateETag != null
                                &&
                           newStoreItem.ETag != "*"
                                &&
                           newStoreItem.ETag != oldStateETag)
                        {
                            throw new EtagException($"Etag conflict.\r\n\r\nOriginal: {newStoreItem.ETag}\r\nCurrent: {oldStateETag}");
                        }

                        newStateETag = NextETag();
                    }

                    _memory[change.Key] = CreateState(change.Value, newStateETag);
                }
            }

            return Task.CompletedTask;
        }

        //<inheritdoc/>
        private Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteCoreAsync(Dictionary<string, object> changes, StorageWriteOptions options, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            options ??= new StorageWriteOptions();
            ValidateExpectedVersion(options.ExpectedVersion, nameof(options));

            using var telemetryScope = new ScopeWrite(changes.Count);

            Dictionary<string, StorageWriteResult> results = new(changes.Count);
            lock (_syncroot)
            {
                foreach (var change in changes)
                {
                    var exists = _memory.TryGetValue(change.Key, out var oldState);
                    var currentVersion = GetStateETag(oldState);

                    if (options.Mode == StorageWriteMode.CreateOnly && exists)
                    {
                        results[change.Key] = new StorageWriteResult()
                        {
                            Key = change.Key,
                            Status = StorageOperationStatus.Conflict,
                            Version = currentVersion,
                        };
                        continue;
                    }

                    if (options.Mode == StorageWriteMode.Replace && !exists)
                    {
                        results[change.Key] = new StorageWriteResult()
                        {
                            Key = change.Key,
                            Status = StorageOperationStatus.NotFound,
                        };
                        continue;
                    }

                    if (!VersionMatches(options.ExpectedVersion, currentVersion))
                    {
                        results[change.Key] = new StorageWriteResult()
                        {
                            Key = change.Key,
                            Status = StorageOperationStatus.ConditionNotMet,
                            Version = currentVersion,
                        };
                        continue;
                    }

                    var newVersion = NextETag();
                    _memory[change.Key] = CreateState(change.Value, newVersion);
                    results[change.Key] = new StorageWriteResult()
                    {
                        Key = change.Key,
                        Status = StorageOperationStatus.Succeeded,
                        Version = newVersion,
                    };
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, StorageWriteResult>>(results);
        }

        //<inheritdoc/>
        public Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, CancellationToken cancellationToken = default) where TValue : class
        {
            return WriteAsync(changes, options: null, cancellationToken);
        }

        //<inheritdoc/>
        public Task<IReadOnlyDictionary<string, StorageWriteResult>> WriteAsync<TValue>(IReadOnlyDictionary<string, TValue> changes, StorageWriteOptions options, CancellationToken cancellationToken = default) where TValue : class
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            Dictionary<string, object> changesAsObject = new(changes.Count);
            foreach (var change in changes)
            {
                changesAsObject.Add(change.Key, change.Value);
            }

            return WriteCoreAsync(changesAsObject, options, cancellationToken);
        }

        //<inheritdoc/>
        public Task WriteAsync<TStoreItem>(IDictionary<string, TStoreItem> changes, CancellationToken cancellationToken = default) where TStoreItem : class
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            Dictionary<string, object> changesAsObject = new(changes.Count);
            foreach (var change in changes)
            {
                changesAsObject.Add(change.Key, change.Value);
            }
            return WriteAsync((IDictionary<string, object>)changesAsObject, cancellationToken);
        }

        //<inheritdoc/>
        public Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        {
            return DeleteAsync(keys, options: null, cancellationToken);
        }

        //<inheritdoc/>
        public Task<IReadOnlyDictionary<string, StorageDeleteResult>> DeleteAsync(IReadOnlyList<string> keys, StorageDeleteOptions options, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

            options ??= new StorageDeleteOptions();
            ValidateExpectedVersion(options.ExpectedVersion, nameof(options));

            using var telemetryScope = new ScopeDelete(keys.Count);

            Dictionary<string, StorageDeleteResult> results = new(keys.Count);
            lock (_syncroot)
            {
                foreach (var key in keys)
                {
                    AssertionHelpers.ThrowIfNullOrWhiteSpace(key, nameof(key));

                    var exists = _memory.TryGetValue(key, out var existingState);
                    var currentVersion = GetStateETag(existingState);

                    if (!exists)
                    {
                        results[key] = new StorageDeleteResult()
                        {
                            Key = key,
                            Status = StorageOperationStatus.NotFound,
                        };
                        continue;
                    }

                    if (!VersionMatches(options.ExpectedVersion, currentVersion))
                    {
                        results[key] = new StorageDeleteResult()
                        {
                            Key = key,
                            Status = StorageOperationStatus.ConditionNotMet,
                            Version = currentVersion,
                        };
                        continue;
                    }

                    _memory.Remove(key);
                    results[key] = new StorageDeleteResult()
                    {
                        Key = key,
                        Status = StorageOperationStatus.Succeeded,
                        Version = currentVersion,
                    };
                }
            }

            return Task.FromResult<IReadOnlyDictionary<string, StorageDeleteResult>>(results);
        }

        private static bool VersionMatches(string expectedVersion, string currentVersion)
        {
            return expectedVersion == null || expectedVersion == currentVersion;
        }

        private static string GetStateETag(JsonObject state)
        {
            if (state != null && state.TryGetPropertyValue(ETagPropertyName, out var etag) && etag != null)
            {
                return etag.ToString();
            }

            return null;
        }

        private static void ValidateExpectedVersion(string expectedVersion, string parameterName)
        {
            if (expectedVersion != null && expectedVersion.Length == 0)
            {
                throw new ArgumentException("ExpectedVersion cannot be empty.", parameterName);
            }
        }

        private JsonObject CreateState(object value, string etag)
        {
            var newState = value != null ? JsonObject.Create(JsonSerializer.SerializeToElement(value, _stateJsonSerializer)) : null;
            if (newState != null)
            {
                if (etag != null)
                {
                    newState[ETagPropertyName] = etag;
                }

                newState.AddTypeInfo(value);
            }

            return newState;
        }

        private object DeserializeState(JsonObject state)
        {
            if (state == null)
            {
                return null;
            }

            if (state.GetTypeInfo(out var type))
            {
                var hasETag = state.TryGetPropertyValue(ETagPropertyName, out var etagValue);
                if (hasETag)
                {
                    state.Remove(ETagPropertyName);
                }

                var typeProps = state.RemoveTypeInfoProperties();
                var value = state.Deserialize(type, _stateJsonSerializer);
                state.SetTypeInfoProperties(typeProps);

                if (hasETag)
                {
                    state[ETagPropertyName] = etagValue;
                }

                if (value is IStoreItem storeItem)
                {
                    storeItem.ETag = etagValue?.ToString();
                }

                return value;
            }

            return state;
        }

        private string NextETag()
        {
            return (_eTag++).ToString(CultureInfo.InvariantCulture);
        }
    }
}
