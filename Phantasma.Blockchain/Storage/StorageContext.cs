using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System.Text;

namespace Phantasma.Blockchain.Storage
{
    public abstract class StorageContext
    {
        public abstract bool Has(byte[] key);
        public abstract byte[] Get(byte[] key);
        public abstract void Put(byte[] key, byte[] value);
        public abstract void Delete(byte[] key);

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

        private static readonly byte[] global_prefix = "{global}".AsByteArray();

        public Collection<T> FindCollectionForAddress<T>(byte[] name, Address address)
        {
            return new Collection<T>(this, name, address.PublicKey);
        }

        public Collection<T> FindCollectionForContract<T>(byte[] name)
        {
            return new Collection<T>(this, name, global_prefix);
        }

        public Map<K, V> FindMapForAddress<K, V>(byte[] name, Address address)
        {
            return new Map<K, V>(this, name, address.PublicKey);
        }

        public Map<K, V> FindMapForContract<K, V>(byte[] name)
        {
            return new Map<K, V>(this, name, global_prefix);
        }
    }
}
