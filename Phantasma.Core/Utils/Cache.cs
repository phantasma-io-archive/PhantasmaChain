using System;
using System.Collections.Generic;

namespace Phantasma.Core.Utils
{
    public class Cache<T>
    {
        private List<KeyValuePair<DateTime, T>> _content;

        public readonly int MaxCount;
        public readonly TimeSpan MaxDuration;

        public IEnumerable<T> Items
        {
            get
            {
                foreach (var item in _content)
                {
                    yield return item.Value;
                }

                yield break;
            }
        }

        public Cache(int maxItems, TimeSpan maxDuration)
        {
            this.MaxCount = maxItems;
            this.MaxDuration = maxDuration;
            this._content = new List<KeyValuePair<DateTime, T>>(maxItems);
        }

        public void Add(T item)
        {
            // keep removing all items that are too old
            while (_content.Count > 0)
            {
                var date = _content[0].Key;
                var diff = DateTime.UtcNow - date;
                if (diff >= MaxDuration)
                {
                    _content.RemoveAt(0);
                }
                else
                {
                    break;
                }
            }

            if (_content.Count >= MaxCount - 1)
            {
                _content.RemoveAt(0);
            }

            _content.Add(new KeyValuePair<DateTime, T>(DateTime.UtcNow, item));
        }
    }

    public class CacheDictionary<K, V> 
    {
        private Dictionary<K, KeyValuePair<DateTime, V>> _items = new Dictionary<K, KeyValuePair<DateTime, V>>();
        private K[] _order;

        public int Count { get; private set; }
        public readonly int Capacity;
        public readonly TimeSpan Duration;

        public readonly bool Infinite;

        public CacheDictionary(int capacity, TimeSpan duration, bool infinite)
        {
            this.Capacity = capacity;
            this.Duration = duration;
            this.Infinite = infinite;
            _order = new K[capacity];
        }

        public void Add(K key, V value)
        {
            lock (_items)
            {
                int index = 0;
                var now = DateTime.UtcNow;

                if (Count == Capacity)
                {
                    Count--;
                    for (int i = 0; i < Count; i++)
                    {
                        _order[i] = _order[i + 1];
                    }
                }

                _order[Count] = key;
                _items[key] = new KeyValuePair<DateTime, V>(DateTime.UtcNow, value);
                Count++;
            }
        }

        public bool TryGet(K key, out V value)
        {
            lock (_items)
            {
                if (_items.ContainsKey(key))
                {
                    var result =_items[key];
                    var diff = DateTime.UtcNow - result.Key;
                    if (Infinite || diff < Duration)
                    {
                        value = result.Value;
                        return true;
                    }
                }

                value = default(V);
                return false;
            }
        }

        public void Remove(K key)
        {
            lock (_items)
            {
                if (_items.ContainsKey(key))
                {
                    _items.Remove(key);

                    int index = -1;

                    for (int i = 0; i < Count; i++)
                    {
                        if (_order[i].Equals(key))
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        throw new Exception("something wrong with cached dictionary");
                    }

                    Count--;
                    for (int i = index; i < Count; i++)
                    {
                        _order[i] = _order[i + 1];
                    }
                }
            }
        }
    }
}
