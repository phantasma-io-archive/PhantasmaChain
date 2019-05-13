using Phantasma.Core;
using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Phantasma.IO
{
    public interface IKeyValueStore
    {
        void SetValue(Hash key, byte[] value);
        byte[] GetValue(Hash key);
        bool ContainsKey(Hash key);
        bool Remove(Hash key);
        uint Count { get; }
    }

    public enum KeyStoreDataSize
    {
        Small, // up to 255 bytes per entry
        Medium, // up to 64kb per entry
        Large, // up to 16mb per entry
        Huge, // up to 4gb per entry
    }

    public class MemoryStore : IKeyValueStore
    {
        public readonly int CacheSize;

        private Dictionary<Hash, byte[]> _cache = new Dictionary<Hash, byte[]>();
        private SortedList<Hash, long> _dates = new SortedList<Hash, long>();

        public uint Count => (uint)_cache.Count;

        public MemoryStore(int cacheSize)
        {
            Throw.If(cacheSize != -1 && cacheSize < 4, "invalid maxsize");
            this.CacheSize = cacheSize;
        }

        public void SetValue(Hash key, byte[] value)
        {
            if (value == null || value.Length == 0)
            {
                Remove(key);
                return;
            }

            if (CacheSize != -1)
            {
                while (_cache.Count >= CacheSize)
                {
                    var first = _dates.Keys[0];
                    Remove(first);
                }
            }

            _cache[key] = value;
            _dates[key] = DateTime.UtcNow.Ticks;
        }

        public byte[] GetValue(Hash key)
        {
            if (ContainsKey(key))
            {
                _dates[key] = DateTime.UtcNow.Ticks;
                return _cache[key];
            }

            return null;
        }

        public bool ContainsKey(Hash key)
        {
            var result = _cache.ContainsKey(key);

            if (result)
            {
                _dates[key] = DateTime.UtcNow.Ticks;
            }

            return result;
        }

        public bool Remove(Hash key)
        {
            if (ContainsKey(key))
            {
                _cache.Remove(key);
                _dates.Remove(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class KeyValueStore<T> : IKeyValueStore
    {
        public readonly string Name;

        private MemoryStore _memory;

        public uint Count => _memory.Count;

        // TODO increase default size
        public KeyValueStore(string name, KeyStoreDataSize dataSize, int cacheSize)
        {
            var fileName = name + ".bin";
            _memory = new MemoryStore(-1);
        }

        public KeyValueStore(Address address, string name, KeyStoreDataSize dataSize, int cacheSize = 16) : this(address.Text + "_" + name, dataSize, cacheSize)
        {
        }

        public T this[Hash key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public void Set(Hash key, T value)
        {
            var bytes = Serialization.Serialize(value);
            SetValue(key, bytes);
        }

        public T Get(Hash key)
        {
            var bytes = GetValue(key);
            Throw.If(bytes == null, "item not found in keystore");
            return Serialization.Unserialize<T>(bytes);
        }

        public void SetValue(Hash key, byte[] value)
        {
            _memory.SetValue(key, value);
        }

        public byte[] GetValue(Hash key)
        {
            if (_memory.ContainsKey(key))
            {
                return _memory.GetValue(key);
            }
                return null;
        }

        public bool ContainsKey(Hash key)
        {
            if (_memory.ContainsKey(key))
            {
                return true;
            }

            return false;
        }

        public bool Remove(Hash key)
        {
                return _memory.Remove(key);
        }
    }
}
