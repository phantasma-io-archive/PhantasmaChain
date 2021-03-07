using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using System;
using System.Text;
using System.Numerics;

namespace Phantasma.Storage.Context
{
    public struct StorageList : IStorageCollection
    {
        public StorageList(string baseKey, StorageContext context) : this(Encoding.UTF8.GetBytes(baseKey), context)
        {
        }

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
                right = ByteArrayUtils.ConcatBytes(element_begin_prefix, index.ToByteArray());
            }

            right = ByteArrayUtils.ConcatBytes(right, element_end_prefix);

            return ByteArrayUtils.ConcatBytes(baseKey, right);
        }

        public static BigInteger Count(this StorageList list)
        {
            //var result = list.Context.Get(CountKey(list.BaseKey)).ToBigInteger();
            var count = list.Context.Get(CountKey(list.BaseKey)).AsBigInteger();
            return count;
        }

        public static BigInteger Add<T>(this StorageList list, T element)
        {
            var index = list.Count();
            list.Context.Put(CountKey(list.BaseKey), index + 1);

            list.Replace(index, element);
            return index;
        }

        public static BigInteger AddRaw(this StorageList list, byte[] bytes)
        {
            var index = list.Count();
            list.Context.Put(CountKey(list.BaseKey), index + 1);

            list.ReplaceRaw(index, bytes);
            return index;
        }

        public static void Replace<T>(this StorageList list, BigInteger index, T element)
        {           
            byte[] bytes;
            if (typeof(IStorageCollection).IsAssignableFrom(typeof(T)))
            {
                var collection = (IStorageCollection)element;
                //bytes = MergeKey(map.BaseKey, key);
                bytes = collection.BaseKey;
            }
            else
            {
                bytes = Serialization.Serialize(element);
            }

            ReplaceRaw(list, index, bytes);
        }

        public static void ReplaceRaw(this StorageList list, BigInteger index, byte[] bytes)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new StorageException("outside of range");
            }

            var key = ElementKey(list.BaseKey, index);
            list.Context.Put(key, bytes);
        }

        public static T Get<T>(this StorageList list, BigInteger index)
        {
            var bytes = GetRaw(list, index);

            if (typeof(IStorageCollection).IsAssignableFrom(typeof(T)))
            {
                var args = new object[] { bytes, list.Context };
                var obj = (T)Activator.CreateInstance(typeof(T), args);
                return obj;
            }
            else
            {
                return Serialization.Unserialize<T>(bytes);
            }
        }

        public static byte[] GetRaw(this StorageList list, BigInteger index)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new StorageException("outside of range");
            }

            var key = ElementKey(list.BaseKey, index);
            var bytes = list.Context.Get(key);

            return bytes;
        }

        public static void RemoveAt(this StorageList list, BigInteger index)
        {
            var size = list.Count();
            if (index < 0 || index >= size)
            {
                throw new StorageException("outside of range");
            }

            size = size - 1;

            if (size > index)
            {
                var last = list.GetRaw(size);
                list.ReplaceRaw(index, last);
            }

            var key = ElementKey(list.BaseKey, size);
            list.Context.Delete(key);

            list.Context.Put(CountKey(list.BaseKey), size);
        }

        public static T[] Range<T>(this StorageList list, BigInteger minIndex, BigInteger maxIndex)
        {
            if (minIndex > maxIndex)
            {
                throw new StorageException("outside of range");
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
                list.RemoveAt(index);
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

        // TODO should this delete all entries instead of just adjusting the count()?
        public static void Clear(this StorageList list)
        {
            BigInteger count = 0;
            list.Context.Put(CountKey(list.BaseKey), count);
        }

    }
}
