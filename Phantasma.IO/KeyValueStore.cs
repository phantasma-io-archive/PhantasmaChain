using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.IO
{
    public interface IKeyValueStoreAdapter
    {
        void SetValue(byte[] key, byte[] value);
        byte[] GetValue(byte[] key);
        bool ContainsKey(byte[] key);
        bool Remove(byte[] key);
        uint Count { get; }
    }

    public class MemoryStore : IKeyValueStoreAdapter
    {
        private Dictionary<byte[], byte[]> _cache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public uint Count => (uint)_cache.Count;

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

            _cache[key] = value;
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

    public class BasicDiskStore : IKeyValueStoreAdapter
    {
        private Dictionary<byte[], byte[]> _cache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public uint Count => (uint)_cache.Count;

        private string fileName;

        public BasicDiskStore(string fileName)
        {
            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            this.fileName = fileName;
            if (File.Exists(fileName))
            {
                var lines = File.ReadAllLines(fileName);
                foreach (var line in lines)
                {
                    var temp = line.Split(',');
                    var key = Convert.FromBase64String(temp[0]);
                    var val = Convert.FromBase64String(temp[1]);
                    _cache[key] = val;
                }
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

            File.WriteAllLines(fileName, _cache.Select(x => Convert.ToBase64String(x.Key) + ","+ Convert.ToBase64String(x.Value)));
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

        private IKeyValueStoreAdapter _adapter;

        public uint Count => _adapter.Count;

        // TODO increase default size
        public KeyValueStore(IKeyValueStoreAdapter adapter)
        {
            _adapter = adapter;
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
            _adapter.SetValue(keyBytes, valBytes);
        }

        public V Get(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            var bytes = _adapter.GetValue(keyBytes);
            if (bytes == null)
            {
                Throw.If(bytes == null, "item not found in keystore");

            }
            return Serialization.Unserialize<V>(bytes);
        }

        public bool ContainsKey(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return _adapter.ContainsKey(keyBytes);
        }

        public bool Remove(K key)
        {
            var keyBytes = Serialization.Serialize(key);
            return _adapter.Remove(keyBytes);
        }
    }
}
