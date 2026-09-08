using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Prism.Mvvm;

namespace PrismDemo.Core.Models
{
    public class ObservableDictionary<TKey, TValue> : BindableBase, IDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                bool isAdding = !_dictionary.ContainsKey(key);
                _dictionary[key] = value;
                RaiseItemChanged($"Item[{key}]");
                RaiseItemChanged("Item[]");
                if (isAdding)
                {
                    RaiseItemChanged("Count");
                    RaiseItemChanged("Keys");
                }
            }
        }

        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;
        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;

        public void Add(TKey key, TValue value)
        {
            _dictionary.Add(key, value);
            RaiseItemChanged($"Item[{key}]");
            RaiseItemChanged("Item[]");
            RaiseItemChanged("Count");
            RaiseItemChanged("Keys");
        }

        public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

        public void Clear()
        {
            _dictionary.Clear();
            RaiseItemChanged("Item[]");
            RaiseItemChanged("Count");
            RaiseItemChanged("Keys");
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)_dictionary).Contains(item);
        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            => ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

        public bool Remove(TKey key)
        {
            var removed = _dictionary.Remove(key);
            if (removed)
            {
                RaiseItemChanged($"Item[{key}]");
                RaiseItemChanged("Item[]");
                RaiseItemChanged("Count");
                RaiseItemChanged("Keys");
            }
            return removed;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => Remove(item.Key);

        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void UpdateBatch(IEnumerable<KeyValuePair<TKey, TValue>> updates)
        {
            foreach (var kvp in updates)
            {
                _dictionary[kvp.Key] = kvp.Value;
                RaiseItemChanged($"Item[{kvp.Key}]");
            }
            RaiseItemChanged("Item[]");
        }

        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
            => _dictionary.TryGetValue(key, out var value) ? value : defaultValue;

        private void RaiseItemChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
            => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
    }
}
