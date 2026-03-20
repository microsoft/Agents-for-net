using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Agents.Builder.Dialogs
{
    public class PersistedState : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _state;

        public PersistedState()
        {
            _state = [];
        }

        public PersistedState(IDictionary<string, object> initialState)
        {
            _state = new Dictionary<string, object>(initialState);
        }

        public static implicit operator PersistedState(Dictionary<string, object> state)
        {
            return [.. state];
        }

        public static explicit operator Dictionary<string, object>(PersistedState persistedState)
        {
            return (new PersistedState(persistedState))._state;
        }

        public object this[string key] { get => _state[key]; set => _state[key] = value; }

        public ICollection<string> Keys => _state.Keys;

        public ICollection<object> Values => _state.Values;

        public int Count => _state.Count;

        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            _state.Add(key, value);
        }

        public void Add(KeyValuePair<string, object> item)
        {
            _state.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _state.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item)
        {
            return _state.TryGetValue(item.Key, out var value) && Equals(value, item.Value);
        }

        public bool ContainsKey(string key)
        {
            return _state.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            return _state.GetEnumerator();
        }

        public bool Remove(string key)
        {
            return _state.Remove(key);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            return _state.Remove(item.Key);
        }

        public bool TryGetValue(string key, out object value)
        {
            return _state.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
