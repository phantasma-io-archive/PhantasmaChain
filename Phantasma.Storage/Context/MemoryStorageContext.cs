using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Storage.Context
{
    public class MemoryStorageContext: StorageContext
    {
        private Dictionary<StorageKey, byte[]> _entries = new Dictionary<StorageKey, byte[]>(new StorageKeyComparer());

        public override void Clear()
        {
            _entries.Clear();
        }

        /*
        public static byte[] FromHumanKey(string key, bool forceSep = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                return new byte[0];
            }

            if (key.Contains("."))
            {
                var temp = key.Split('.');
                byte[] result = new byte[0];

                foreach (var entry in temp)
                {
                    var sub = FromHumanKey(entry, true);
                    result = ByteArrayUtils.ConcatBytes(result, sub);
                }
            }

            try
            {
                var address = Address.FromText(key);
                return address.PublicKey;
            }
            catch
            {
            }

            if (key.StartsWith("0x"))
            {
                return Base16.Decode(key.Substring(0));
            }

            if (key.StartsWith("[") && key.EndsWith("["))
            {
                key = key.Substring(1, key.Length - 2);
                var num = BigInteger.Parse(key);
                var result = num.ToByteArray();
                result = ByteArrayUtils.ConcatBytes(new byte[] { (byte)'<' }, result);
                result = ByteArrayUtils.ConcatBytes(result, new byte[] { (byte)'>' });
            }

            {
                var result = global::System.Text.Encoding.ASCII.GetBytes(key);
                if (forceSep)
                {
                    result = ByteArrayUtils.ConcatBytes(new byte[] { (byte)'{' }, result);
                    result = ByteArrayUtils.ConcatBytes(result, new byte[] { (byte)'}' });
                }
                return result;
            }
        }*/

        public override bool Has(StorageKey key)
        {
            return _entries.ContainsKey(key);
        }

        public override byte[] Get(StorageKey key)
        {
            var value = _entries.ContainsKey(key) ? _entries[key] : new byte[0];

            //Log($"GET: {StorageKey.ToHumanKey(key.keyData)} => {StorageKey.ToHumanValue(key.keyData, value)}");

            return value;
        }

        public override void Put(StorageKey key, byte[] value)
        {
            //Log($"PUT: {StorageKey.ToHumanKey(key.keyData)} => {StorageKey.ToHumanValue(key.keyData, value)}");
            if (value == null)
            {
                value = new byte[0];
            }
            _entries[key] = value;
        }

        public override void Delete(StorageKey key)
        {
            //Log($"DELETE: {StorageKey.ToHumanKey(key.keyData)}");

            if (_entries.ContainsKey(key))
            {
                _entries.Remove(key);
            }
        }

        public override void Visit(Action<byte[], byte[]> visitor, ulong searchCount = 0, byte[] prefix = null)
        {
            ulong count = 0;
            searchCount += 1; // ugly hack, count key is filterd in StorageMap
            foreach(var entry in _entries)
            {
                var bytePrefix = entry.Key.keyData.Take(prefix.Length);
                if (count < searchCount && bytePrefix.SequenceEqual(prefix))
                {
                    visitor(entry.Key.keyData, entry.Value);
                    count++;
                }

                if (count == searchCount)
                {
                    break;
                }
            }
        }
    }
}
