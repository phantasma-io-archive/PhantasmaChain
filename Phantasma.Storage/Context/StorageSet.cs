using System.Text;
using Phantasma.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Core;
using Phantasma.Storage.Utils;

namespace Phantasma.Storage.Context
{
    public struct StorageSet: IStorageCollection
    {
        public StorageSet(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }

    public static class SetUtils
    { 
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

        private static byte[] MergeKey(byte[] parentKey, object childKey)
        {
            var bytes = Encoding.UTF8.GetBytes($".{childKey}");
            return ByteArrayUtils.ConcatBytes(parentKey, bytes);
        }

        public static BigInteger Count(this StorageSet set)
        {
            return set.Context.Get(CountKey(set.BaseKey)).AsBigInteger();
        }

        public static bool Contains<K>(this StorageSet set, K key)
        {
            return set.Context.Has(ElementKey(set.BaseKey, key));
        }

        public static void Add<K>(this StorageSet set, K key)
        {
            bool exists = set.Contains(key);

            if (!exists)
            {
                set.Context.Put(ElementKey(set.BaseKey, key), new byte[] { 1 });

                var size = set.Count() + 1;
                set.Context.Put(CountKey(set.BaseKey), size);
            }
        }

        public static void Remove<K>(this StorageSet set, K key)
        {
            if (set.Contains(key))
            {
                set.Context.Delete(ElementKey(set.BaseKey, key));
                var size = set.Count() - 1;
                set.Context.Put(CountKey(set.BaseKey), size);
            }
        }
    }
}
