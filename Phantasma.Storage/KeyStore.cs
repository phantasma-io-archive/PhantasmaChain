using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage.Utils;
using Logger = Phantasma.Core.Log.Logger;
using ConsoleLogger = Phantasma.Core.Log.ConsoleLogger;
using RocksDbSharp;

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

    public class DBPartition : IKeyValueStoreAdapter
    {
	private RocksDb _db;
        private ColumnFamilyHandle partition;
        private string partitionName;
        private string path;
        private readonly Logger logger = new ConsoleLogger();

        public uint Count => GetCount();

        public DBPartition(string fileName)
        {
            this.partitionName = Path.GetFileName(fileName);
            this.path = Path.GetDirectoryName(fileName);

            if (!path.EndsWith("/"))
            {
                path += '/';
            }

	    this._db = RocksDbStore.Instance(path);


            // Create partition if it doesn't exist already
            try
            {
                logger.Message("Getting partition: " + this.partitionName);
                this.partition = this._db.GetColumnFamily(partitionName);
            }
            catch
            {
                logger.Message("Partition not found, create it now: " + this.partitionName);

                // TODO different partitions might need different options...
                this.partition = this._db.CreateColumnFamily(new ColumnFamilyOptions(), partitionName);
            }
        }

        #region internal_funcs
        private ColumnFamilyHandle GetPartition()
        {
            return _db.GetColumnFamily(partitionName);
        }

        private byte[] Get_Internal(byte[] key)
        {
            return _db.Get(key, cf: GetPartition());
        }

        private void Put_Internal(byte[] key, byte[] value)
        {
            _db.Put(key, value, cf: GetPartition());
        }

        private void Remove_Internal(byte[] key)
        {
            _db.Remove(key, cf: GetPartition());
        }
        #endregion

        public uint GetCount()
        {
            uint count = 0;
            var readOptions = new ReadOptions();
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                iter.SeekToFirst();
                while (iter.Valid())
                {
                    count++;
                    iter.Next();
                }
            }

            return count;
        }

        public void Visit(Action<byte[], byte[]> visitor)
        {
            var readOptions = new ReadOptions();
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                iter.SeekToFirst();
                while (iter.Valid())
                {
                    visitor(iter.Key(), iter.Value());
                    iter.Next();
                }
            }
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.IfNull(key, nameof(key));

            if (value == null || value.Length == 0)
            {
                Remove_Internal(key);
            }
            else
            {
                Put_Internal(key, value);
            }
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                return Get_Internal(key);
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            var result = Get_Internal(key);
            return (result != null  && result.Length > 0) ? true : false;
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                Remove_Internal(key);
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class RocksDbStore
    {
	private static RocksDb _db;
	private static RocksDbStore _rdb;
        private readonly Logger logger = new ConsoleLogger();

        private string fileName;

        private RocksDbStore(string fileName)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            logger.Message("RocksDBStore: " + fileName);
            this.fileName = fileName.Replace("\\", "/");

            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            //TODO check options
            var options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            var columnFamilies = new ColumnFamilies
            {
                { "default", new ColumnFamilyOptions().OptimizeForPointLookup(256) },
                //{ "test", new ColumnFamilyOptions()
                //    //.SetWriteBufferSize(writeBufferSize)
                //    //.SetMaxWriteBufferNumber(maxWriteBufferNumber)
                //    //.SetMinWriteBufferNumberToMerge(minWriteBufferNumberToMerge)
                //    .SetMemtableHugePageSize(2 * 1024 * 1024)
                //    .SetPrefixExtractor(SliceTransform.CreateFixedPrefix((ulong)8))
                //    .SetBlockBasedTableFactory(bbto)
                //},
            };

            try
            {
                var partitionList = RocksDb.ListColumnFamilies(options, path);
                foreach (var partition in partitionList)
                {
                    columnFamilies.Add(partition, new ColumnFamilyOptions());
                }
            }
            catch
            {
                logger.Warning("Inital start, no partitions created yet!");
            }


	    _db = RocksDb.Open(options, path, columnFamilies);
        }

        public static RocksDb Instance(string name=null)
        {
            if (_db == null)
            {
                if (string.IsNullOrEmpty(name)) throw new System.ArgumentException("Parameter cannot be null", "name");

                _rdb = new RocksDbStore(name);
            }

            return _db;
        }

        private void Shutdown()
        {

            logger.Message("Shutting down database...");
            _db.Dispose();
            logger.Message("Database has been shut down!");
        }

    }

    public class BasicDiskStore : IKeyValueStoreAdapter
    {
        private Dictionary<byte[], byte[]> _cache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public uint Count => (uint)_cache.Count;

        private string fileName;

        public BasicDiskStore(string fileName)
        {
            this.fileName = fileName.Replace("\\", "/");

            var path = Path.GetDirectoryName(fileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (File.Exists(fileName))
            {
                var lines = File.ReadAllLines(fileName);
                lock (_cache)
                {
                    foreach (var line in lines)
                    {
                        var temp = line.Split(',');
                        var key = Convert.FromBase64String(temp[0]);
                        var val = Convert.FromBase64String(temp[1]);
                        _cache[key] = val;
                    }
                }
            }
        }

        public void Visit(Action<byte[], byte[]> visitor)
        {
            lock (_cache)
            {
                foreach (var entry in _cache)
                {
                    visitor(entry.Key, entry.Value);
                }
            }
        }

        private void UpdateToDisk()
        {
            File.WriteAllLines(fileName, _cache.Select(x => Convert.ToBase64String(x.Key) + "," + Convert.ToBase64String(x.Value)));
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
                lock (_cache)
                {
                    _cache[key] = value;
                    UpdateToDisk();
                }
            }
        }

        public byte[] GetValue(byte[] key)
        {
            if (ContainsKey(key))
            {
                lock (_cache)
                {
                    return _cache[key];
                }
            }

            return null;
        }

        public bool ContainsKey(byte[] key)
        {
            lock (_cache)
            {
                var result = _cache.ContainsKey(key);
                return result;
            }
        }

        public bool Remove(byte[] key)
        {
            if (ContainsKey(key))
            {
                lock (_cache)
                {
                    _cache.Remove(key);
                    UpdateToDisk();
                }
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
