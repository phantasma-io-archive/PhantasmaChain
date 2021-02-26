using System;
using System.Text;
using System.Numerics;

namespace Phantasma.Storage.Context
{
    public abstract class StorageContext
    {
        public abstract void Clear();
        public abstract bool Has(StorageKey key);
        public abstract byte[] Get(StorageKey key);
        public abstract void Put(StorageKey key, byte[] value);
        public abstract void Delete(StorageKey key);

        public bool Has(byte[] key)
        {
            return Has(new StorageKey(key));
        }

        public byte[] Get(byte[] key)
        {
            return Get(new StorageKey(key));
        }

        public void Put(byte[] key, byte[] value)
        {
            Put(new StorageKey(key), value);
        }

        public void Delete(byte[] key)
        {
            Delete(new StorageKey(key));
        }

        public byte[] Get(string key)
        {
            return Get(Encoding.UTF8.GetBytes(key));
        }

        public bool Has(string key)
        {
            return Has(Encoding.UTF8.GetBytes(key));
        }

        public T Get<T>(byte[] key)
        {
            return (T)Get(key, typeof(T));
        }

        public T Get<T>(string key)
        {
            return (T)Get(Encoding.UTF8.GetBytes(key), typeof(T));
        }

        public object Get(byte[] key, Type type)
        {
            var bytes = Get(key);
            return Serialization.Unserialize(bytes, type);
        }

        public void Put(byte[] key, BigInteger value) { Put(key, value.ToByteArray()); }

        public void Put<T>(byte[] key, T obj)
        {
            var bytes = Serialization.Serialize(obj);
            Put(key, bytes);
        }

        public void Put<T>(string key, T obj)
        {
            var bytes = Serialization.Serialize(obj);
            Put(key, bytes);
        }

        public void Put(string key, byte[] value) { Put(Encoding.UTF8.GetBytes(key), value); }

        public void Delete(string key) { Delete(Encoding.UTF8.GetBytes(key)); }

        public abstract void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null);
    }
}
