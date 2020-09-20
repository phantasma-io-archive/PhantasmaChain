using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Storage;
using Phantasma.Core;
using Logger = Phantasma.Core.Log.Logger;
using RocksDbSharp;

namespace Phantasma.RocksDB
{
    public class DBPartition : IKeyValueStoreAdapter
    {
	    private RocksDb _db;
        private ColumnFamilyHandle partition;
        private string partitionName;
        private string path;
        private readonly Logger logger;

        public uint Count => GetCount();

        public DBPartition(Logger logger, string fileName)
        {
            this.partitionName = Path.GetFileName(fileName);
            this.path = Path.GetDirectoryName(fileName);
            this.logger = logger;

            logger.Message("FileName: " + fileName);

            if (!path.EndsWith("/"))
            {
                path += '/';
            }

	        this._db = RocksDbStore.Instance(logger, path);

            // Create partition if it doesn't exist already
            try
            {
                logger.Message("Getting partition: " + this.partitionName);
                this.partition = this._db.GetColumnFamily(partitionName);
            }
            catch
            {
                logger.Message("Partition not found, create it now: " + this.partitionName);
                var cf = new ColumnFamilyOptions();
                // TODO different partitions might need different options...
                this.partition = this._db.CreateColumnFamily(cf, partitionName);
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

        public void CreateCF(string name)
        {
            var cf = new ColumnFamilyOptions();
            _db.CreateColumnFamily(cf, name);
        }

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

        public void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null)
        {
            var readOptions = new ReadOptions().SetPrefixSameAsStart(true);
            using (var iter = _db.NewIterator(readOptions: readOptions, cf: GetPartition()))
            {
                if (prefix == null || prefix.Length == 0)
                {
                    iter.SeekToFirst();
                    while (iter.Valid())
                    {
                        visitor(iter.Key(), iter.Value());
                        iter.Next();
                    }
                }
                else
                {
                    ulong _count = 0;
                    iter.Seek(prefix);
                    while (iter.Valid() && _count < searchCount)
                    {
                        visitor(iter.Key(), iter.Value());
                        iter.Next();
                        _count++;
                    }
                }
            }
        }

        public void SetValue(byte[] key, byte[] value)
        {
            Throw.IfNull(key, nameof(key));
            Put_Internal(key, value);
        }

        public byte[] GetValue(byte[] key)
        {
            byte[] value;
            if (ContainsKey_Internal(key, out value))
            {
                return value;
            }

            return null;
        }

        public bool ContainsKey_Internal(byte[] key, out byte[] value)
        {
            value = Get_Internal(key);
            return (value != null) ? true : false;
        }

        public bool ContainsKey(byte[] key)
        {
            var value = Get_Internal(key);
            return (value != null) ? true : false;
        }

        public void Remove(byte[] key)
        {
            Remove_Internal(key);
        }
    }

    public class RocksDbStore
    {
	    private static Dictionary<string, RocksDb> _db = new Dictionary<string, RocksDb>();
	    private static Dictionary<string, RocksDbStore> _rdb = new Dictionary<string, RocksDbStore>();
        private readonly Logger logger;

        private string fileName;

        private RocksDbStore(string fileName, Logger logger)
        {
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
            this.logger = logger;
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

            logger.Message("Opening database at: " + path);
	        _db.Add(fileName, RocksDb.Open(options, path, columnFamilies));
        }

        public static RocksDb Instance(Logger logger, string name)
        {
            if (!_db.ContainsKey(name))
            {
                if (string.IsNullOrEmpty(name)) throw new System.ArgumentException("Parameter cannot be null", "name");

                _rdb.Add(name, new RocksDbStore(name, logger));
            }

            return _db[name];
        }

        private void Shutdown()
        {
            if (_db.Count > 0)
            {
                var toRemove = new List<String>();
                logger.Message($"Shutting down databases...");
                foreach (var db in _db)
                {
                    db.Value.Dispose();
                    toRemove.Add(db.Key);
                }

                foreach (var key in toRemove)
                {
                    _db.Remove(key);
                }
                logger.Message("Databases shut down!");
            }
        }

    }
}
