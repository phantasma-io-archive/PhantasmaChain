using System.Text;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Storage
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

        public byte[] Get(string key)
        {
            return Get(Encoding.UTF8.GetBytes(key));
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

        internal static byte[] MakeContractPrefix(SmartContract contract)
        {
            return $"[global.{contract.Name}]".AsByteArray();
        }

        public Collection<T> FindCollectionForAddress<T>(string name, Address address)
        {
            return FindCollectionForAddress<T>(Encoding.UTF8.GetBytes(name), address);
        }

        public Collection<T> FindCollectionForContract<T>(string name, SmartContract contract)
        {
            return FindCollectionForContract<T>(Encoding.UTF8.GetBytes(name), contract);
        }

        public Collection<T> FindCollectionForAddress<T>(byte[] name, Address address)
        {
            return new Collection<T>(this, name, address.PublicKey);
        }

        public Collection<T> FindCollectionForContract<T>(byte[] name, SmartContract contract)
        {
            return new Collection<T>(this, name, MakeContractPrefix(contract));
        }

        public Map<K, V> FindMapForAddress<K, V>(string name, Address address)
        {
            return FindMapForAddress<K, V>(Encoding.UTF8.GetBytes(name), address);

        }

        public Map<K, V> FindMapForContract<K, V>(string name, SmartContract contract)
        {
            return FindMapForContract<K, V>(Encoding.UTF8.GetBytes(name), contract);
        }

        public Map<K, V> FindMapForAddress<K, V>(byte[] name, Address address)
        {
            return new Map<K, V>(this, name, address.PublicKey);
        }

        public Map<K, V> FindMapForContract<K, V>(byte[] name, SmartContract contract)
        {
            return new Map<K, V>(this, name, MakeContractPrefix(contract));
        }
    }
}
