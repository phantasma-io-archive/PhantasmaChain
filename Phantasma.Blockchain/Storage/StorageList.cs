using Phantasma.Core.Utils;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Storage
{
    public struct StorageList : IStorageCollection
    {
        public StorageList(byte[] baseKey, StorageContext context) : this()
        {
            BaseKey = baseKey;
            Context = context;
        }

        public byte[] BaseKey { get; }
        public StorageContext Context { get; }
    }

    public static class CollectionUtils
    { 
        private static readonly byte[] count_prefix = "{count}".AsByteArray();
        public static readonly byte[] element_begin_prefix = new byte[] { (byte)'<' };
        public static readonly byte[] element_end_prefix = new byte[] { (byte)'>' };

/*       internal Collection(StorageContext context, byte[] name, byte[] prefix)
        {
            this.Context = context;
            this.BaseKey = ByteArrayUtils.ConcatBytes(prefix, name);
        }
        */

        private static byte[] CountKey(byte[] baseKey)
        {
            return ByteArrayUtils.ConcatBytes(baseKey, count_prefix);
        }

        private static byte[] ElementKey(byte[] baseKey, BigInteger index)
        {
            byte[] right;

            if (index == 0)
            {
                right = ByteArrayUtils.ConcatBytes(element_begin_prefix, new byte[] { 0 });
            }
            else
            {
                right = ByteArrayUtils.ConcatBytes(element_begin_prefix, index.AsByteArray());
            }

            right = ByteArrayUtils.ConcatBytes(right, element_end_prefix);

            return ByteArrayUtils.ConcatBytes(baseKey, right);
        }

        public static BigInteger Count(this StorageList list)
        {
            var result = list.Context.Get(CountKey(list.BaseKey)).AsLargeInteger();
            return result;
        }

        public static void Add<T>(this StorageList list, T element)
        {
            var count = list.Count();
            list.Context.Put(CountKey(list.BaseKey), count + 1);

            list.Replace(count, element);
        }

        public static void Replace<T>(this StorageList list, BigInteger index, T element)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var key = ElementKey(list.BaseKey, index);
            var bytes = Serialization.Serialize(element);
            list.Context.Put(key, bytes);
        }

        public static T Get<T>(this StorageList list, BigInteger index)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var key = ElementKey(list.BaseKey, index);
            var bytes = list.Context.Get(key);
            return Serialization.Unserialize<T>(bytes);
        }

        public static void Delete<T>(this StorageList list, BigInteger index)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var indexKey = ElementKey(list.BaseKey, index);

            size = size - 1;

            if (size > index)
            {
                // TODO <T> would not really be necessary here, this swap could be improved by using byte[]
                var last = list.Get<T>(size);
                list.Replace(index, last);
            }

            var key = ElementKey(list.BaseKey, size);
            list.Context.Delete(key);

            list.Context.Put(CountKey(list.BaseKey), size);
        }

        public static T[] Range<T>(this StorageList list, BigInteger minIndex, BigInteger maxIndex)
        {
            if (minIndex > maxIndex)
            {
                throw new ContractException("outside of range");
            }

            int total = 1 + (int)(maxIndex - minIndex);
            
            var result = new T[total];

            int offset = 0;
            BigInteger index = minIndex;
            while (offset < total)
            {
                result[offset] = list.Get<T>(index);
                offset = offset + 1;
                index++;                
            }

            return result;
        }

        public static bool Contains<T>(this StorageList list, T obj)
        {
            return list.IndexOf(obj) >= 0;
        }

        public static void Remove<T>(this StorageList list, T obj)
        {
            var index = list.IndexOf(obj);
            if (index >= 0)
            {
                list.Delete<T>(index);
            }
        }

        public static BigInteger IndexOf<T>(this StorageList list, T obj)
        {
            BigInteger index = 0;
            var size = list.Count();
            while (index < size)
            {
                var val = list.Get<T>(index);
                if (val.Equals(obj))
                {
                    return index;
                }
                index++;
            }

            return -1;
        }

        public static T[] All<T>(this StorageList list)
        {
            var size = list.Count();
            var items = new T[(int)size];
            for (int i=0; i<size; i++)
            {
                items[i] = list.Get<T>(i);
            }
            return items;
        }

        public static void Clear(this StorageList list)
        {
            BigInteger count = 0;
            list.Context.Put(CountKey(list.BaseKey), count);
        }

    }
}
