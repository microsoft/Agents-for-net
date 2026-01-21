// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System.Collections.Concurrent;
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
    public class MemoryStorage : IStorage
    {
        // If a JsonSerializer is not provided during construction, this will be the default static JsonSerializer.
        private readonly JsonSerializerOptions _stateJsonSerializer;
        private readonly ConcurrentDictionary<string, JsonObject> _memory;
        private readonly Dictionary<string, JsonObject> _externalDictionary;
        private int _eTag = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryStorage"/> class.
        /// </summary>
        /// <param name="jsonSerializer">Optional: JsonSerializerOptions.</param>
        /// <param name="dictionary">Optional: A pre-existing dictionary to use. Or null to use a new one.
        /// When provided, this dictionary will be used directly (not copied) for storage operations.</param>
        public MemoryStorage(JsonSerializerOptions jsonSerializer = null, Dictionary<string, JsonObject> dictionary = null)
        {
            _stateJsonSerializer = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;
            _externalDictionary = dictionary;
            _memory = dictionary == null ? new ConcurrentDictionary<string, JsonObject>() : null;
        }

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

            foreach (var key in keys)
            {
                if (_externalDictionary != null)
                {
                    _externalDictionary.Remove(key);
                }
                else
                {
                    _memory.TryRemove(key, out _);
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

            foreach (var key in keys)
            {
                JsonObject state = null;
                if (_externalDictionary != null)
                {
                    _externalDictionary.TryGetValue(key, out state);
                }
                else
                {
                    _memory.TryGetValue(key, out state);
                }

                if (state != null)
                {
                    // Create a copy to avoid concurrent modification issues
                    var stateCopy = JsonNode.Parse(state.ToJsonString())?.AsObject();
                    if (stateCopy != null && stateCopy.GetTypeInfo(out var type))
                    {
                        stateCopy.RemoveTypeInfoProperties();
                        storeItems.Add(key, stateCopy.Deserialize(type, _stateJsonSerializer));
                    }
                    else if (stateCopy != null)
                    {
                        storeItems.Add(key, stateCopy);
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
        /// <returns>A task that represents the work queued to execute. Throws ArgumentException for an ETag conflict.</returns>
        /// <seealso cref="DeleteAsync(string[], CancellationToken)"/>
        /// <seealso cref="ReadAsync(string[], CancellationToken)"/>
        public Task WriteAsync(IDictionary<string, object> changes, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfNull(changes, nameof(changes));

            foreach (var change in changes)
            {
                var newValue = change.Value;

                if (_externalDictionary != null)
                {
                    // For external dictionary (test scenarios), use simple dictionary operations
                    if (_externalDictionary.TryGetValue(change.Key, out var oldState))
                    {
                        string oldStateETag = null;
                        if (oldState.TryGetPropertyValue("ETag", out var etag))
                        {
                            oldStateETag = etag?.ToString();
                        }

                        // Check ETag if applicable
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
                        }
                    }

                    _externalDictionary[change.Key] = CreateNewState(newValue);
                }
                else
                {
                    // Use AddOrUpdate for atomic operations with ConcurrentDictionary
                    _memory.AddOrUpdate(
                        change.Key,
                        // Add factory
                        (_) => CreateNewState(newValue),
                        // Update factory
                        (_, oldState) =>
                        {
                            string oldStateETag = null;
                            if (oldState.TryGetPropertyValue("ETag", out var etag))
                            {
                                oldStateETag = etag?.ToString();
                            }

                            // Check ETag if applicable
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
                            }

                            return CreateNewState(newValue);
                        }
                    );
                }
            }

            return Task.CompletedTask;
        }

        private JsonObject CreateNewState(object value)
        {
            var newState = value != null ? JsonObject.Create(JsonSerializer.SerializeToElement(value, _stateJsonSerializer)) : null;

            // Set ETag if applicable
            if (newState != null && value is IStoreItem)
            {
                // Use post-increment semantics: return current value, then increment
                newState["ETag"] = (Interlocked.Increment(ref _eTag) - 1).ToString(CultureInfo.InvariantCulture);
            }

            newState?.AddTypeInfo(value);
            return newState;
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
