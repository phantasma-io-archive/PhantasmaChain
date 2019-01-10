using Phantasma.Numerics;
using Phantasma.Core.Utils;
using Phantasma.VM.Utils;
using Phantasma.IO;

namespace Phantasma.Blockchain.Storage
{
    public struct StorageMap : IStorageCollection
    {
        public StorageMap(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }

    public static class MapUtils
    { 
/*        public readonly StorageContext Context;


        internal Map(StorageContext context, byte[] name, byte[] prefix)
        {
            this.Context = context;
            this.BaseKey = ByteArrayUtils.ConcatBytes(prefix, name);
        }
        */

        private static byte[] count_prefix = "{count}".AsByteArray();

        private static byte[] CountKey(byte[] baseKey)
        {
            return ByteArrayUtils.ConcatBytes(baseKey, count_prefix);
        }

        private static byte[] ElementKey<K>(byte[] baseKey, K index)
        {
            byte[] bytes = Serialization.Serialize(index);
            return ByteArrayUtils.ConcatBytes(baseKey, bytes);
        }

        public static BigInteger Count(this StorageMap map)
        {
            return map.Context.Get(CountKey(map.BaseKey)).AsLargeInteger();
        }

        public static bool ContainsKey<K>(this StorageMap map, K key)
        {
            return map.Context.Has(ElementKey(map.BaseKey, key));
        }

        public static void Set<K, V>(this StorageMap map, K key, V value)
        {
            bool exists = map.ContainsKey(key);
            var bytes = Serialization.Serialize(value);
            map.Context.Put(ElementKey(map.BaseKey, key), bytes);

            if (!exists)
            {
                var size = map.Count() + 1;
                map.Context.Put(CountKey(map.BaseKey), size);
            }
        }

        public static V Get<K, V>(this StorageMap map, K key)
        {
            if (map.ContainsKey(key))
            {
                var bytes = map.Context.Get(ElementKey(map.BaseKey, key));
                return Serialization.Unserialize<V>(bytes);
            }

            throw new ContractException("key not found");
        }

        public static void Remove<K>(this StorageMap map, K key)
        {
            if (map.ContainsKey(key))
            {
                map.Context.Delete(ElementKey(map.BaseKey, key));
                var size = map.Count() - 1;
                map.Context.Put(CountKey(map.BaseKey), size);
            }
        }

        public static V[] All<K,V>(this StorageMap map, K[] keys)
        {
            var size = keys.Length;
            var items = new V[size];
            for (int i = 0; i < size; i++)
            {
                items[i] = map.Get<K,V>(keys[i]);
            }
            return items;
        }

    }
}
