// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
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
    /// Initializes a new instance of the <see cref="MemoryStorage"/> class.
    /// </remarks>
    /// <param name="jsonSerializer">Optional: JsonSerializerOptions.</param>
    /// <param name="dictionary">Optional: A pre-existing dictionary to use. Or null to use a new one.</param>
    public class MemoryStorage(JsonSerializerOptions jsonSerializer = null, Dictionary<string, JsonObject> dictionary = null) : IStorageExt
    {
        // If a JsonSerializer is not provided during construction, this will be the default static JsonSerializer.
        private readonly JsonSerializerOptions _stateJsonSerializer = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;
        private readonly Dictionary<string, JsonObject> _memory = dictionary ?? [];
        private readonly object _syncroot = new();
        private int _eTag = 0;

        /// <summary>
        /// Deletes storage items from storage.
        /// </summary>
        /// <param name="keys">Keys for the <see cref="IStoreItem"/> objects to delete.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        public Task DeleteAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));

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
        /// <param name="keys">Keys of the <see cref="IStoreItem"/> objects to read.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the activities are successfully sent, the task result contains
        /// the items read, indexed by key.</remarks>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="WriteAsync(IDictionary{string, object}, CancellationToken)"/>
        public Task<IDictionary<string, object>> ReadAsync(string[] keys, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(keys, nameof(keys));
            var storeItems = new Dictionary<string, object>(keys.Length);
            lock (_syncroot)
            {
                foreach (var key in keys)
                {
                    if (_memory.TryGetValue(key, out var state))
                    {
                        if (state.GetTypeInfo(out var type))
                        {
                            var typeProps = state.RemoveTypeInfoProperties();
                            storeItems.Add(key, state.Deserialize(type, _stateJsonSerializer));
                            state.SetTypeInfoProperties(typeProps);
                        }
                        else
                        {
                            storeItems.Add(key, state);
                        }
                    }
                }
            }

            return Task.FromResult<IDictionary<string, object>>(storeItems);
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
        /// <returns>A task that represents the work queued to execute. Throws EtagException for an ETag conflict.</returns>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken)
        {
            return WriteAsync(changes, new StorageWriteOptions(), cancellationToken);
        }

        //<inheritdoc/>
        public Task<IDictionary<string, IStoreItem>> WriteAsync(IDictionary<string, object> changes, StorageWriteOptions writeOptions, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            writeOptions ??= new StorageWriteOptions();

            var results = new Dictionary<string, IStoreItem>(changes.Count);

            lock (_syncroot)
            {
                foreach (var change in changes)
                {
                    var newValue = change.Value;

                    var oldStateETag = default(string);

                    if (_memory.TryGetValue(change.Key, out var oldState))
                    {
                        if (writeOptions.IfNotExists)
                        {
                            throw new ItemExistsException($"Unable to write '{change.Key}' because it already exists.");
                        }

                        if (oldState.TryGetPropertyValue("ETag", out var etag))
                        {
                            oldStateETag = etag.ToString();
                        }
                    }

                    var newState = newValue != null ? JsonObject.Create(JsonSerializer.SerializeToElement(newValue, _stateJsonSerializer)) : null;

                    // Set ETag if applicable
                    if (newValue is IStoreItem newStoreItem)
                    {
                        if (oldStateETag != null
                                &&
                            newStoreItem.ETag != "*"
                                &&
                            newStoreItem.ETag != oldStateETag)
                        {
                            throw new EtagException($"Unable to write '{change.Key}' due to an ETag conflict. Old: {oldStateETag} New: {newStoreItem.ETag}.");
                        }

                        var newETag = (_eTag++).ToString(CultureInfo.InvariantCulture);
                        results[change.Key] = new WriteResult() { ETag = newETag };
                        newState["ETag"] = newETag;
                    }

                    newState?.AddTypeInfo(change.Value);
                    _memory[change.Key] = newState;
                }
            }

            return Task.FromResult((IDictionary<string, IStoreItem>)results);
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
            return WriteAsync(changesAsObject, cancellationToken);
        }
    }
}
