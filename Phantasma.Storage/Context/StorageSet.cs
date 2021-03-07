using System.Numerics;
using Phantasma.Numerics;
using Phantasma.Core.Utils;
using Phantasma.Core;
using System.Collections.Generic;
using System.Linq;
using System;

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

        public static BigInteger Count(this StorageSet set)
        {
            // TODO maintain a count var of type bigint instead of creating a new instance each time Count is called.
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

        public static V[] AllValues<V>(this StorageSet set)
        {
            var values = new List<V>();
            var countKey = CountKey(set.BaseKey);
            var found = false;
            var countKeyRun = false;

            set.Context.Visit((key, value) =>
            {
                if (!found && key.SequenceEqual(countKey))
                {
                    countKeyRun = true;
                    found = true;
                }

                if (!countKeyRun)
                {
                    V Val;
                    if (typeof(IStorageCollection).IsAssignableFrom(typeof(V)))
                    {
                        var args = new object[] { value, set.Context };
                        var obj = (V)Activator.CreateInstance(typeof(V), args);
                        Val = obj;
                        values.Add(Val);
                    }
                    else
                    {
                        Val = Serialization.Unserialize<V>(value);
                        values.Add(Val);
                    }
                }
                else
                {
                    countKeyRun = false;
                }
            }, (uint)set.Count(), set.BaseKey);

            return values.ToArray();
        }

    }
}
