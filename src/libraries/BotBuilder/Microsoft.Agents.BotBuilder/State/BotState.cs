// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.State
{
    /// <summary>
    /// Base class for BotState key/value state.
    /// </summary>
    /// <seealso cref="IStorage"/>
    public abstract class BotState : IPropertyManager, IBotState
    {
        private readonly IStorage _storage;
        private CachedBotState _cachedBotState;

        /// <summary>
        /// Initializes a new instance of the <see cref="BotState"/> class.
        /// </summary>
        /// <param name="storage">The storage layer this state management object will use to store
        /// and retrieve state.</param>
        /// <param name="stateName">The key for the state cache for this <see cref="BotState"/>.</param>
        /// <remarks>This constructor creates a state management object and associated scope.
        /// The object uses <paramref name="storage"/> to persist state property values.
        /// The object uses the <paramref name="stateName"/> to cache state within the context for each turn.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="storage"/> or <paramref name="stateName"/>
        /// is <c>null</c>.</exception>
        /// <seealso cref="ITurnContext"/>
        public BotState(IStorage storage, string stateName)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Name = stateName ?? throw new ArgumentNullException(nameof(stateName));
        }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <summary>
        /// Creates a named state property within the scope of a <see cref="BotState"/> and returns
        /// an accessor for the property.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <returns>An accessor for the property.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> is <c>null</c>.</exception>
        [Obsolete("Use BotState.GetValue and BotState.SetValue")]
        public IStatePropertyAccessor<T> CreateProperty<T>(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return new BotStatePropertyAccessor<T>(this, name);
        }

        /// <inheritdoc/>
        public bool HasValue(string name)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            var cachedState = GetCachedState();
            return cachedState.State.ContainsKey(name);
        }

        /// <inheritdoc/>
        public void DeleteValue(string name)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            DeletePropertyValue(name);
        }

        /// <inheritdoc/>
        public T GetValue<T>(string name, Func<T> defaultValueFactory = null)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            T result = default;

            try
            {
                // if T is a value type, lookup up will throw key not found if not found, but as perf
                // optimization it will return null if not found for types which are not value types (string and object).
                result = GetPropertyValue<T>(name);

                if (result == null && defaultValueFactory != null)
                {
                    // use default Value Factory and save default value for any further calls
                    result = defaultValueFactory();
                    SetValue(name, result);
                }
            }
            catch (KeyNotFoundException)
            {
                if (defaultValueFactory != null)
                {
                    // use default Value Factory and save default value for any further calls
                    result = defaultValueFactory();
                    SetValue(name, result);
                }
            }

            return result;
        }

        public bool TryGetValue<T>(string name, out T result)
        {
            if (!IsLoaded())
            {
                result = default;
                return false;
            }

            if (!HasValue(name))
            {
                result = default;
                return false;
            }

            result = GetPropertyValue<T>(name);
            return true;
        }

        /// <inheritdoc/>
        public void SetValue<T>(string name, T value)
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            SetPropertyValue(name, value);
        }

        /// <summary>
        /// True if state has been loaded.
        /// </summary>
        public bool IsLoaded()
        {
            return _cachedBotState != null;
        }

        /// <inheritdoc/>
        public virtual async Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var storageKey = GetStorageKey(turnContext);

            if (ShouldLoad(turnContext, storageKey, force))
            {
                var items = await _storage.ReadAsync([storageKey], cancellationToken).ConfigureAwait(false);
                items.TryGetValue(storageKey, out object val);

                if (val is IDictionary<string, object> asDictionary)
                {
                    _cachedBotState = new CachedBotState(storageKey, asDictionary);
                }
                else if (val is JsonObject || val is JsonElement)
                {
                    _cachedBotState = new CachedBotState(storageKey, ProtocolJsonSerializer.ToObject<IDictionary<string, object>>(val));
                }
                else if (val == null)
                {
                    // This is the case where the dictionary did not exist in the store.
                    _cachedBotState = new CachedBotState(storageKey);
                }
                else
                {
                    throw new InvalidOperationException("Data is not in the correct format for BotState.");
                }

                turnContext.StackState.Set<CachedBotState>(Name, _cachedBotState);
            }
        }

        private bool ShouldLoad(ITurnContext turnContext, string storageKey, bool force)
        {
            _cachedBotState = turnContext.StackState.Get<CachedBotState>(Name);
            return force || _cachedBotState == null || _cachedBotState.State == null;
        }

        /// <inheritdoc/>
        public virtual async Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var cachedState = GetCachedState();
            if (cachedState != null && (force || cachedState.IsChanged()))
            {
                var key = GetStorageKey(turnContext);
                var changes = new Dictionary<string, object>
                {
                    { key, cachedState.State },
                };
                await _storage.WriteAsync(changes, cancellationToken).ConfigureAwait(false);
                cachedState.Hash = CachedBotState.ComputeHash(cachedState.State);
                return;
            }
        }

        /// <inheritdoc/>
        public virtual void ClearState()
        {
            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            // Explicitly setting the hash will mean IsChanged is always true. And that will force a Save.
            GetCachedState().Clear();
        }

        /// <inheritdoc/>
        public virtual async Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (IsLoaded())
            {
                ClearState();
            }

            var storageKey = GetStorageKey(turnContext);
            await _storage.DeleteAsync(new[] { storageKey }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// When overridden in a derived class, gets the key to use when reading and writing state to and from storage.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <returns>The storage key.</returns>
        protected abstract string GetStorageKey(ITurnContext turnContext);

        /// <summary>
        /// Gets the value of a property from the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <typeparam name="T">The value type of the property.</typeparam>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains the property value, otherwise it will be default(T).</remarks>
#pragma warning disable CA1801 // Review unused parameters (we can't change this without breaking binary compat)
        protected T GetPropertyValue<T>(string propertyName)
#pragma warning restore CA1801 // Review unused parameters
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            if (!IsLoaded())
            {
                throw new InvalidOperationException($"{Name} is not loaded");
            }

            var cachedState = GetCachedState();
            if (cachedState.State.TryGetValue(propertyName, out object result))
            {
                if (result is T t)
                {
                    return t;
                }

                if (result == null)
                {
                    return default(T);
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

            return default(T);
        }

        /// <summary>
        /// Deletes a property from the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected void DeletePropertyValue(string propertyName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            var cachedState = GetCachedState();
            cachedState.State.Remove(propertyName);
        }

        /// <summary>
        /// Sets the value of a property in the state cache for this <see cref="BotState"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set on the property.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected void SetPropertyValue(string propertyName, object value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

            var cachedState = GetCachedState();
            cachedState.State[propertyName] = value;
        }

        internal JsonElement Get()
        {
            var cachedState = GetCachedState();
            return JsonSerializer.SerializeToElement(cachedState.State, ProtocolJsonSerializer.SerializationOptions);
        }

        internal CachedBotState GetCachedState()
        {
            return _cachedBotState;
        }

        /// <summary>
        /// Internal cached bot state.
        /// </summary>
        internal class CachedBotState
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CachedBotState"/> class.
            /// </summary>
            /// <param name="key">Unique state key.  Typically the storage key.</param>
            /// <param name="state">Initial state for the <see cref="CachedBotState"/>.</param>
            public CachedBotState(string key, IDictionary<string, object> state = null)
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
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking binary compat)
            public IDictionary<string, object> State { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

            internal string Hash { get; set; }

            internal string Key { get; set; }

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

        #region Obsolete BotStatePropertyAccessor
        /// <summary>
        /// Implements an <see cref="IStatePropertyAccessor{T}"/> for a property container.
        /// Note the semantics of this accessor are intended to be lazy, this means the Get, Set and Delete
        /// methods will first call LoadAsync. This will be a no-op if the data is already loaded.
        /// The implication is you can just use this accessor in the application code directly without first calling LoadAsync
        /// this approach works with the AutoSaveStateMiddleware which will save as needed at the end of a turn.
        /// </summary>
        /// <typeparam name="T">type of value the propertyAccessor accesses.</typeparam>
        private class BotStatePropertyAccessor<T> : IStatePropertyAccessor<T>
        {
            private BotState _botState;

            public BotStatePropertyAccessor(BotState botState, string name)
            {
                _botState = botState;
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
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task DeleteAsync(ITurnContext turnContext, CancellationToken cancellationToken)
            {
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                _botState.DeleteValue(Name);
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
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task<T> GetAsync(ITurnContext turnContext, Func<T> defaultValueFactory, CancellationToken cancellationToken)
            {
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);

                // if T is a value type, lookup up will throw key not found if not found, but as perf
                // optimization it will return null if not found for types which are not value types (string and object).
                return _botState.GetValue<T>(Name, defaultValueFactory);
            }

            /// <summary>
            /// Set the property value. The semantics are intended to be lazy, note the use of LoadAsync at the start.
            /// </summary>
            /// <param name="turnContext">turn context.</param>
            /// <param name="value">value.</param>
            /// <param name="cancellationToken">The cancellation token.</param>
            /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
            public async Task SetAsync(ITurnContext turnContext, T value, CancellationToken cancellationToken)
            {
                await _botState.LoadAsync(turnContext, false, cancellationToken).ConfigureAwait(false);
                _botState.SetValue(Name, value);
            }
        }
        #endregion
    }
}
