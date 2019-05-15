using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Storage
{
    public interface IKeyValueStoreAdapter
    {
        void SetValue(byte[] key, byte[] value);
        byte[] GetValue(byte[] key);
        bool ContainsKey(byte[] key);
        bool Remove(byte[] key);
        uint Count { get; }
        void Visit(Action<byte[], byte[]> visitor);
    }

    public class MemoryStore : IKeyValueStoreAdapter
    {
        private Dictionary<byte[], byte[]> _entries = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public uint Count => (uint)_entries.Count;

        public MemoryStore()
        {
        }

        public void SetValue(byte[] key, byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Remove(key);
                return;
            }

            _entries[key] = value;
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                return _entries[key];
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            var result = _entries.ContainsKey(key);
            return result;
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                _entries.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Visit(Action<byte[], byte[]> visitor)
        {
            foreach (var entry in _entries)
            {
                visitor(entry.Key, entry.Value);
            }
        }
    }

    public class BasicDiskStore : IKeyValueStoreAdapter
    {
        private Dictionary<byte[], byte[]> _cache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public uint Count => (uint)_cache.Count;

        private string path;

        public BasicDiskStore(string path)
        {
            this.path = path;
            if (File.Exists(path))
            {
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    var temp = line.Split(',');
                    var key = Convert.FromBase64String(temp[0]);
                    var val = Convert.FromBase64String(temp[1]);
                    _cache[key] = val;
                }
            }
        }

        public void Visit(Action<byte[], byte[]> visitor)
        {
            foreach (var entry in _cache)
            {
                visitor(entry.Key, entry.Value);
            }
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.IfNull(key, nameof(key));

            if (value == null || value.Length == 0)
            {
                Remove(key);
            }
            else
            {
                _cache[key] = value;
            }

            File.WriteAllLines(path, _cache.Select(x => Convert.ToBase64String(x.Key) + "," + Convert.ToBase64String(x.Value)));
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                return _cache[key];
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            var result = _cache.ContainsKey(key);
            return result;
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                _cache.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class KeyValueStore<K, V>
    {
        public readonly string Name;

        public readonly IKeyValueStoreAdapter Adapter;

        public uint Count => Adapter.Count;

        // TODO increase default size
        public KeyValueStore(IKeyValueStoreAdapter adapter)
        {
            Adapter = adapter;
        }

        public V this[K key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public void Set(K key, V value)
        {
            var keyBytes = Serialization.Serialize(key);
            var valBytes = Serialization.Serialize(value);
            Adapter.SetValue(keyBytes, valBytes);
        }

        public V Get(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            var bytes = Adapter.GetValue(keyBytes);
            if (bytes == null)
            {
                Throw.If(bytes == null, "item not found in keystore");

            }
            return Serialization.Unserialize<V>(bytes);
        }

        public bool ContainsKey(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return Adapter.ContainsKey(keyBytes);
        }

        public bool Remove(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return Adapter.Remove(keyBytes);
        }

        public void Visit(Action<K, V> visitor)
        {
            Adapter.Visit((keyBytes, valBytes) =>
            {
                var key = Serialization.Unserialize<K>(keyBytes);
                var val = Serialization.Unserialize<V>(valBytes);
                visitor(key, val);
            });
        }
}
}
