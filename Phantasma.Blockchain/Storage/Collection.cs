using Phantasma.Core.Utils;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Storage
{
    public class Collection<T>
    {
        private static readonly byte[] count_prefix = "{count}".AsByteArray();
        public static readonly byte[] element_begin_prefix = new byte[] { (byte)'<' };
        public static readonly byte[] element_end_prefix = new byte[] { (byte)'>' };
        private readonly byte[] BaseKey;

        public readonly StorageContext Context;

        internal Collection(StorageContext context, byte[] name, byte[] prefix)
        {
            this.Context = context;
            this.BaseKey = ByteArrayUtils.ConcatBytes(prefix, name);
        }

        private byte[] CountKey()
        {
            return ByteArrayUtils.ConcatBytes(BaseKey, count_prefix);
        }

        private byte[] ElementKey(LargeInteger index)
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

            return ByteArrayUtils.ConcatBytes(BaseKey, right);
        }

        public LargeInteger Count()
        {
            var result = Context.Get(CountKey()).AsLargeInteger();
            return result;
        }

        public void Add(T element)
        {
            var count = Count();
            Context.Put(CountKey(), count + 1);

            Replace(count, element);
        }

        public void Replace(LargeInteger index, T element)
        {
            var size = Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var key = ElementKey(index);
            var bytes = Serialization.Serialize(element);
            Context.Put(key, bytes);
        }

        public T Get(LargeInteger index)
        {
            var size = Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var key = ElementKey(index);
            var bytes = Context.Get(key);
            return Serialization.Unserialize<T>(bytes);
        }

        public void Delete(LargeInteger index)
        {
            var size = Count();
            if (index < 0 || index >= size)
            {
                throw new ContractException("outside of range");
            }

            var indexKey = ElementKey(index);

            size = size - 1;

            if (size > index)
            {
                var last = Get(size);
                Replace(index, last);
            }

            var key = ElementKey(size);
            Context.Delete(key);

            Context.Put(CountKey(), size);
        }

        public T[] Range(LargeInteger minIndex, LargeInteger maxIndex)
        {
            if (minIndex > maxIndex)
            {
                throw new ContractException("outside of range");
            }

            int total = 1 + (int)(maxIndex - minIndex);
            
            var result = new T[total];

            int offset = 0;
            LargeInteger index = minIndex;
            while (offset < total)
            {
                result[offset] = Get(index);
                offset = offset + 1;
                index++;                
            }

            return result;
        }

        public bool Contains(T obj)
        {
            return IndexOf(obj) >= 0;
        }

        public void Remove(T obj)
        {
            var index = IndexOf(obj);
            if (index >= 0)
            {
                Delete(index);
            }
        }

        public LargeInteger IndexOf(T obj)
        {
            LargeInteger index = 0;
            var size = Count();
            while (index < size)
            {
                var val = Get(index);
                if (val.Equals(obj))
                {
                    return index;
                }
                index++;
            }

            return -1;
        }

        public T[] All()
        {
            var size = Count();
            var items = new T[(int)size];
            for (int i=0; i<size; i++)
            {
                items[i] = Get(i);
            }
            return items;
        }

        public void Clear()
        {
            LargeInteger count = 0;
            Context.Put(CountKey(), count);
        }

    }
}
