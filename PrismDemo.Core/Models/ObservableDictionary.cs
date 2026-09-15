// PrismDemo.Core/Models/ObservableDictionary.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

                // 通知特定键的变化
                RaisePropertyChanged($"Item[{key}]");
                RaisePropertyChanged("Item[]");

                if (isAdding)
                {
                    RaisePropertyChanged("Count");
                    RaisePropertyChanged("Keys");
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
            RaisePropertyChanged($"Item[{key}]");
            RaisePropertyChanged("Item[]");
            RaisePropertyChanged("Count");
            RaisePropertyChanged("Keys");
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _dictionary.Clear();
            RaisePropertyChanged("Item[]");
            RaisePropertyChanged("Count");
            RaisePropertyChanged("Keys");
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return _dictionary.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)_dictionary).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            var removed = _dictionary.Remove(key);
            if (removed)
            {
                RaisePropertyChanged($"Item[{key}]");
                RaisePropertyChanged("Item[]");
                RaisePropertyChanged("Count");
                RaisePropertyChanged("Keys");
            }
            return removed;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // 批量更新方法
        public void UpdateBatch(IEnumerable<KeyValuePair<TKey, TValue>> updates)
        {
            foreach (var kvp in updates)
            {
                _dictionary[kvp.Key] = kvp.Value;
                RaisePropertyChanged($"Item[{kvp.Key}]");
            }
            RaisePropertyChanged("Item[]");
        }

        // 获取值，如果不存在返回默认值
        public TValue GetValueOrDefault(TKey key, TValue defaultValue = default)
        {
            return _dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private void RaisePropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }
    }
}
