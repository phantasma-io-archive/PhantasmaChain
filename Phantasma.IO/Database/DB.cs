using Phantasma.Core.Utils;
using Phantasma.Cryptography.Hashing;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Phantasma.IO.Database
{
    internal struct DBEntry
    {
        public byte[] Key { get; set; }
        public object Value { get; set; }
    }

    internal enum TransactionKind
    {
        Write,
        Delete
    }

    internal struct Transaction
    {
        public TransactionKind Kind;
        public byte[] Key;
        public byte[] Value;
    }

    public class DB
    {
        private BlockingCollection<Transaction> TransactionQueue = new BlockingCollection<Transaction>();
        private readonly object LockObject = new object();

        private Dictionary<uint, Cluster> _clusters = new Dictionary<uint, Cluster>();

        private Dictionary<byte[], DBEntry> Cache = new Dictionary<byte[], DBEntry>(new ByteArrayComparer());
        private int Counter = 0;

        public string Path { get; private set; }

        public static readonly string Extension = ".odb";

        public DB(string path)
        {
            path = path.Replace("\\", "/");

            if (!path.EndsWith("/"))
            {
                path += "/";
            }

            this.Path = path;
        }

        public void Insert<T>(byte[] key, T value)
        {
            Interlocked.Increment(ref Counter);
            var entry = new DBEntry { Key = key, Value = value};

            //Add to cache
            lock (LockObject)
            {
                Cache[key] = entry;
            }

            ThreadPool.QueueUserWorkItem(state =>
            {
                var bytes = Serialization.Serialize(value);
                bytes = Compression.CompressGZip(bytes);

                var tx = new Transaction() { Kind = TransactionKind.Write, Key = key, Value = bytes };
                TransactionQueue.Add(tx);
            });
        }

        public void Remove(byte[] key, string filelocation)
        {
            lock (LockObject)
            {
                Cache.Remove(key);
            }

            var data = new Transaction() { Kind = TransactionKind.Delete, Key = key, Value = null};
            TransactionQueue.Add(data);
        }

        public T Get<T>(byte[] key)
        {
            //Try getting the object from cache first
            lock (LockObject)
            {
                DBEntry entry;
                if (Cache.TryGetValue(key, out entry))
                {
                    return (T)entry.Value;
                }
            }

            var cluster = FindClusterForKey(key, false);
            if (cluster == null)
            {
                return default(T);
            }

            var bytes = cluster.Get(key);
            if (bytes == null)
            {
                return default(T);
            }

            bytes = Compression.DecompressGZip(bytes);
            T umcompressedObject = Serialization.Unserialize<T>(bytes);
            return umcompressedObject;
        }

        private Cluster FindClusterForKey(byte[] key, bool canCreate)
        {
            var hash = Murmur32.Hash(key);

            lock (LockObject)
            {
                if (_clusters.ContainsKey(hash))
                {
                    return _clusters[hash];
                }

                if (canCreate)
                {
                    var cluster = new Cluster(hash, this.Path);
                    _clusters[hash] = cluster;
                    return cluster;
                }
            }

            return null;
        }

        public bool Exists(byte[] key)
        {
            var cluster = FindClusterForKey(key, false);
            if (cluster == null)
            {
                return false;
            }

            return cluster.Contains(key);
        }

        public void WaitForCompletion()
        {
            while (Counter > 0)
            {
                Thread.Sleep(10);
            }
        }

    }
}
