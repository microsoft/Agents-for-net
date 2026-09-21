// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    /// Base class for AgentState key/value state.
    /// </summary>
    /// <seealso cref="Microsoft.Agents.Storage.IStorage"/>
    public abstract class AgentState : IPropertyManager, IAgentState
    {
        private readonly IStorageV2 _storage;
        private readonly bool _supportsOptimisticConcurrency;
        private readonly object _stateLock = new object();
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private CachedAgentState _cachedAgentState;

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Agents.Builder.State.AgentState"/> class.
        /// </summary>
        /// <param name="storage">The storage layer this state management object will use to store
        /// and retrieve state.</param>
        /// <param name="stateName">The key for the state cache for this <see cref="Microsoft.Agents.Builder.State.AgentState"/>.</param>
        /// <remarks>This constructor creates a state management object and associated scope.
        /// The object uses <paramref name="storage"/> to persist state property values.
        /// The object uses the <paramref name="stateName"/> to cache state within the context for each turn.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException"><paramref name="storage"/> or <paramref name="stateName"/>
        /// is <c>null</c>.</exception>
        /// <seealso cref="Microsoft.Agents.Builder.ITurnContext"/>
        public AgentState(IStorage storage, string stateName)
        {
            var storageInstance = storage ?? throw new ArgumentNullException(nameof(storage));
            _supportsOptimisticConcurrency = storageInstance is IStorageV2;
            _storage = StorageCompatibility.AsV2(storageInstance);
            Name = stateName ?? throw new ArgumentNullException(nameof(stateName));
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <summary>
        /// Creates a named state property within the scope of a <see cref="Microsoft.Agents.Builder.State.AgentState"/> and returns
        /// an accessor for the property.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <returns>An accessor for the property.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        [Obsolete("Use AgentState.GetValue and AgentsState.SetValue")]
        public IStatePropertyAccessor<T> CreateProperty<T>(string name)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));
            return new AgentStatePropertyAccessor<T>(this, name);
        }

        /// <inheritdoc/>
        public bool HasValue(string name)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            lock (_stateLock)
            {
                var cachedState = GetCachedState();
                return cachedState.State.ContainsKey(name);
            }
        }

        /// <inheritdoc/>
        public void DeleteValue(string name)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            lock (_stateLock)
            {
                DeletePropertyValueCore(name);
            }
        }

        /// <inheritdoc/>
        public T GetValue<T>(string name, Func<T> defaultValueFactory = null)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            lock (_stateLock)
            {
                T result = default;

                try
                {
                    // if T is a value type, lookup up will throw key not found if not found, but as perf
                    // optimization it will return null if not found for types which are not value types (string and object).
                    result = GetPropertyValueCore<T>(name);

                    if (result == null && defaultValueFactory != null)
                    {
                        // use default Value Factory and save default value for any further calls
                        result = defaultValueFactory();
                        SetPropertyValueCore(name, result);
                    }
                }
                catch (KeyNotFoundException)
                {
                    if (defaultValueFactory != null)
                    {
                        // use default Value Factory and save default value for any further calls
                        result = defaultValueFactory();
                        SetPropertyValueCore(name, result);
                    }
                }

                return result;
            }
        }

        public bool TryGetValue<T>(string name, out T result)
        {
            if (!IsLoaded())
            {
                result = default;
                return false;
            }

            lock (_stateLock)
            {
                var cachedState = GetCachedState();
                if (!cachedState.State.ContainsKey(name))
                {
                    result = default;
                    return false;
                }

                result = GetPropertyValueCore<T>(name);
                return true;
            }
        }

        /// <inheritdoc/>
        public void SetValue<T>(string name, T value)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));

            lock (_stateLock)
            {
                SetPropertyValueCore(name, value);
            }
        }

        /// <summary>
        /// True if state has been loaded.
        /// </summary>
        public bool IsLoaded()
        {
            return _cachedAgentState != null;
        }

        /// <inheritdoc/>
        public virtual async Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            var storageKey = GetStorageKey(turnContext);

            if (ShouldLoad(turnContext, storageKey, force))
            {
                var results = await _storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
                var readResult = results[storageKey];
                var storedState = readResult.Status == StorageOperationStatus.Succeeded ? readResult.Value : null;

                if (storedState is IDictionary<string, object> asDictionary)
                {
                    _cachedAgentState = new CachedAgentState(storageKey, asDictionary);
                }
                else if (storedState is JsonObject || storedState is JsonElement)
                {
                    _cachedAgentState = new CachedAgentState(storageKey, ProtocolJsonSerializer.ToObject<IDictionary<string, object>>(storedState));
                }
                else if (storedState == null)
                {
                    // This is the case where the dictionary did not exist in the store.
                    _cachedAgentState = new CachedAgentState(storageKey);
                }
                else
                {
                    throw new InvalidOperationException("Data is not in the correct format for AgentState.");
                }

                _cachedAgentState.Version = readResult.Status == StorageOperationStatus.Succeeded ? readResult.Version : null;
                turnContext.StackState.Set<CachedAgentState>(Name, _cachedAgentState);
            }
        }

        private bool ShouldLoad(ITurnContext turnContext, string storageKey, bool force)
        {
            _cachedAgentState = turnContext.StackState.Get<CachedAgentState>(Name);
            return force || _cachedAgentState == null || _cachedAgentState.State == null;
        }

        /// <inheritdoc/>
        public virtual async Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));

            await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var cachedState = GetCachedState();
                if (cachedState != null)
                {
                    // Snapshot state and compute new hash under lock to prevent concurrent
                    // mutations from being reflected during serialization.
                    Dictionary<string, object> snapshot;
                    string newHash;
                    lock (_stateLock)
                    {
                        snapshot = new Dictionary<string, object>(cachedState.State);
                        newHash = CachedAgentState.ComputeHash(snapshot);
                    }

                    // Check if changed outside the lock (cheap string comparison)
                    if (force || cachedState.Hash != newHash)
                    {
                        var key = GetStorageKey(turnContext);
                        var changes = new Dictionary<string, object>
                        {
                            { key, snapshot },
                        };
                        if (_supportsOptimisticConcurrency)
                        {
                            IReadOnlyDictionary<string, object> readOnlyChanges = changes;
                            IReadOnlyDictionary<string, StorageWriteResult> writeResults;
                            if (cachedState.Version != null)
                            {
                                writeResults = await _storage.WriteAsync(
                                    readOnlyChanges,
                                    new StorageWriteOptions { ExpectedVersion = cachedState.Version },
                                    cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                writeResults = await _storage.WriteAsync(readOnlyChanges, cancellationToken).ConfigureAwait(false);
                            }

                            var writeResult = writeResults[key];
                            if (writeResult.Status != StorageOperationStatus.Succeeded)
                            {
                                throw new EtagException($"AgentState '{Name}' could not save key '{key}' because another turn updated the state first (status: {writeResult.Status}). This turn's state changes were not saved.");
                            }

                            cachedState.Version = writeResult.Version;
                        }
                        else
                        {
                            await _storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);
                        }

                        // Update hash after successful write
                        cachedState.Hash = newHash;
                        return;
                    }
                }
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <inheritdoc/>
        public virtual void ClearState()
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            lock (_stateLock)
            {
                // Explicitly setting the hash will mean IsChanged is always true. And that will force a Save.
                GetCachedState().Clear();
            }
        }

        /// <inheritdoc/>
        public virtual async Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (IsLoaded())
            {
                ClearState();
            }

            var storageKey = GetStorageKey(turnContext);
            await _storage.DeleteAsync([storageKey], cancellationToken).ConfigureAwait(false);

            // DeleteStateAsync removes the backing document, unlike ClearState which is
            // intentionally persisted as an empty document at the end of the turn. Once the
            // delete succeeds, the loaded version is no longer valid and the cleared cache must
            // not trigger the automatic end-of-turn save.
            lock (_stateLock)
            {
                var cachedState = GetCachedState();
                if (cachedState != null)
                {
                    cachedState.Version = null;
                    cachedState.Hash = CachedAgentState.ComputeHash(cachedState.State);
                }
            }
        }

        /// <summary>
        /// When overridden in a derived class, gets the key to use when reading and writing state to and from storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The storage key.</returns>
        protected abstract string GetStorageKey(ITurnContext turnContext);

        /// <summary>
        /// Gets the value of a property from the state cache for this <see cref="Microsoft.Agents.Builder.State.AgentState"/>.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains the property value, otherwise it will be default(T).
        /// This method is thread-safe and can be called directly by derived classes.</remarks>
        protected T GetPropertyValue<T>(string propertyName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(propertyName, nameof(propertyName));

            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            lock (_stateLock)
            {
                return GetPropertyValueCore<T>(propertyName);
            }
        }

        /// <summary>
        /// Deletes a property from the state cache for this <see cref="Microsoft.Agents.Builder.State.AgentState"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This method is thread-safe and can be called directly by derived classes.</remarks>
        protected void DeletePropertyValue(string propertyName)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(propertyName, nameof(propertyName));

            lock (_stateLock)
            {
                DeletePropertyValueCore(propertyName);
            }
        }

        /// <summary>
        /// Sets the value of a property in the state cache for this <see cref="Microsoft.Agents.Builder.State.AgentState"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set on the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>This method is thread-safe and can be called directly by derived classes.</remarks>
        protected void SetPropertyValue(string propertyName, object value)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(propertyName, nameof(propertyName));

            lock (_stateLock)
            {
                SetPropertyValueCore(propertyName, value);
            }
        }

        /// <summary>
        /// Core implementation for getting a property value. Must be called under _stateLock.
        /// </summary>
        private T GetPropertyValueCore<T>(string propertyName)
        {
            var cachedState = GetCachedState();
            if (cachedState.State.TryGetValue(propertyName, out object result))
            {
                if (result is T t)
                {
                    return t;
                }

                if (result == null)
                {
                    return default;
                }

                // If types are not used by storage serialization try to convert the object to the type expected
                // using the serializer.
                var converted = ProtocolJsonSerializer.ToObject<T>(result);
                cachedState.State[propertyName] = converted;
                return converted;
            }

            if (typeof(T).IsValueType)
            {
                throw new KeyNotFoundException(propertyName);
            }

            return default;
        }

        /// <summary>
        /// Core implementation for deleting a property value. Must be called under _stateLock.
        /// </summary>
        private void DeletePropertyValueCore(string propertyName)
        {
            var cachedState = GetCachedState();
            cachedState.State.Remove(propertyName);
        }

        /// <summary>
        /// Core implementation for setting a property value. Must be called under _stateLock.
        /// </summary>
        private void SetPropertyValueCore(string propertyName, object value)
        {
            var cachedState = GetCachedState();
            cachedState.State[propertyName] = value;
        }

        internal JsonElement Get()
        {
            lock (_stateLock)
            {
                var cachedState = GetCachedState();
                return JsonSerializer.SerializeToElement(cachedState.State, ProtocolJsonSerializer.SerializationOptions);
            }
        }

        internal CachedAgentState GetCachedState()
        {
            return _cachedAgentState;
        }

        /// <summary>
        /// Internal cached Agent state.
        /// </summary>
        internal class CachedAgentState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Microsoft.Agents.Builder.State.AgentState.CachedAgentState"/> class.
            /// </summary>
            /// <param name="key">Unique state key.  Typically the storage key.</param>
            /// <param name="state">Initial state for the <see cref="Microsoft.Agents.Builder.State.AgentState.CachedAgentState"/>.</param>
            public CachedAgentState(string key, IDictionary<string, object> state = null)
            {
                State = state ?? new Dictionary<string, object>();
                Hash = ComputeHash(State);
                Key = key;
            }

            /// <summary>
            /// Gets or sets the state as a dictionary of key value pairs.
            /// </summary>
            /// <value>
            /// The state as a dictionary of key value pairs.
            /// </value>
            public IDictionary<string, object> State { get; set; }

            internal string Hash { get; set; }

            internal string Key { get; set; }

            internal string Version { get; set; }

            internal static string ComputeHash(object obj)
            {
                return ProtocolJsonSerializer.ToJson(obj);
            }

            internal bool IsChanged()
            {
                return Hash != ComputeHash(State);
            }

            internal void Clear()
            {
                State = new Dictionary<string, object>();
                Hash = string.Empty;
            }
        }

        #region Obsolete AgentStatePropertyAccessor
        /// <summary>
        /// Implements an <see cref="Microsoft.Agents.Builder.State.IStatePropertyAccessor{T}"/> for a property container.
        /// Note the semantics of this accessor are intended to be lazy, this means the Get, Set and Delete
        /// methods will first call LoadAsync. This will be a no-op if the data is already loaded.
        /// The implication is you can just use this accessor in the application code directly without first calling LoadAsync
        /// this approach works with the AutoSaveStateMiddleware which will save as needed at the end of a turn.
        /// </summary>
        /// <typeparam name="T">type of value the propertyAccessor accesses.</typeparam>
        private class AgentStatePropertyAccessor<T> : IStatePropertyAccessor<T>
        {
            private AgentState _agentState;

            public AgentStatePropertyAccessor(AgentState agentState, string name)
            {
                _agentState = agentState;
                Name = name;
            }

            /// <summary>
            /// Gets name of the property.
            /// </summary>
            /// <value>
            /// name of the property.
            /// </value>
            public string Name { get; private set; }

            /// <summary>
            /// Delete the property. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">The turn context.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
            public async Task DeleteAsync(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                await _agentState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                _agentState.DeleteValue(Name);
            }

            /// <summary>
            /// Get the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">The context object for this turn.</param>
            /// <param name="defaultValueFactory">Defines the default value.
            /// Invoked when no value been set for the requested state property.
            /// If defaultValueFactory is defined as null in that case, the method returns null and
            /// <see cref="SetAsync(ITurnContext, T, CancellationToken)">SetAsync</see> is not called.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
            public async Task<T> GetAsync(ITurnContext turnContext, Func<T> defaultValueFactory, CancellationToken cancellationToken)
            {
                await _agentState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);

                // if T is a value type, lookup up will throw key not found if not found, but as perf
                // optimization it will return null if not found for types which are not value types (string and object).
                return _agentState.GetValue<T>(Name, defaultValueFactory);
            }

            /// <summary>
            /// Set the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">turn context.</param>
            /// <param name="value">value.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="System.Threading.Tasks.Task"/> representing the asynchronous operation.</returns>
            public async Task SetAsync(ITurnContext turnContext, T value, CancellationToken cancellationToken)
            {
                await _agentState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                _agentState.SetValue(Name, value);
            }
        }
        #endregion
    }
}
