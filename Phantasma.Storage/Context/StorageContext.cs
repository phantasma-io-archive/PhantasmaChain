using System;
using System.Text;
using Phantasma.Numerics;
using Phantasma.Storage.Utils;

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

        public void Put<T>(byte[] key, T obj) where T : struct
        {
            var bytes = Serialization.Serialize(obj);
            Put(key, bytes);
        }

        public void Put(byte[] key, string value) { Put(key, Encoding.UTF8.GetBytes(value)); }

        public void Put(string key, byte[] value) { Put(Encoding.UTF8.GetBytes(key), value); }

        public void Put(string key, BigInteger value) { Put(Encoding.UTF8.GetBytes(key), value.ToByteArray()); }

        public void Put(string key, string value) { Put(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value)); }

        public void Delete(string key) { Delete(Encoding.UTF8.GetBytes(key)); }

        /*
        public StorageList FindCollectionForAddress(string name, Address address)
        {
            return FindCollectionForAddress(Encoding.UTF8.GetBytes(name), address);
        }

        public StorageList FindCollectionForContract(string name, SmartContract contract)
        {
            return FindCollectionForContract(Encoding.UTF8.GetBytes(name), contract);
        }

        public StorageList FindCollectionForAddress(byte[] name, Address address)
        {
            return new StorageList(this, name, address.PublicKey);
        }

        public StorageList FindCollectionForContract(byte[] name, SmartContract contract)
        {
            return new StorageList(this, name, MakeContractPrefix(contract));
        }

        public StorageMap FindMapForAddress(string name, Address address)
        {
            return FindMapForAddress<K, V>(Encoding.UTF8.GetBytes(name), address);

        }

        public StorageMap<K, V> FindMapForContract<K, V>(string name, SmartContract contract)
        {
            return FindMapForContract<K, V>(Encoding.UTF8.GetBytes(name), contract);
        }

        public StorageMap<K, V> FindMapForAddress<K, V>(byte[] name, Address address)
        {
            return new StorageMap<K, V>(this, name, address.PublicKey);
        }

        public StorageMap<K, V> FindMapForContract<K, V>(byte[] name, SmartContract contract)
        {
            return new StorageMap<K, V>(this, name, MakeContractPrefix(contract));
        }*/
    }
}
