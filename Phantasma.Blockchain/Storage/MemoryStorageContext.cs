using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Phantasma.Core.Utils;
using Phantasma.IO;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Storage
{
    public class StorageKeyComparer : IEqualityComparer<StorageKey>
    {
        public bool Equals(StorageKey left, StorageKey right)
        {
            return left.data.SequenceEqual(right.data);
        }

        public int GetHashCode(StorageKey key)
        {
            return key.data.Sum(b => b);
        }
    }

    public struct StorageKey
    {
        public byte[] data;

        public StorageKey(byte[] data)
        {
            this.data = data;
        }

        public override string ToString()
        {
            return MemoryStorageContext.ToHumanKey(data);
        }

        public override int GetHashCode()
        {
            return data.GetHashCode();
        }
    }

    public class MemoryStorageContext: StorageContext
    {
        public readonly Dictionary<StorageKey, byte[]> Entries = new Dictionary<StorageKey, byte[]>(new StorageKeyComparer());

        public void Clear()
        {
            Entries.Clear();
        }

        private void Log(string s)
        {
            var temp = global::System.Console.ForegroundColor;
            global::System.Console.ForegroundColor = global::System.ConsoleColor.Yellow;
            global::System.Console.WriteLine(s);
            global::System.Console.ForegroundColor = temp;
        }

        public static bool IsASCII(byte[] key)
        {
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] < 32 || key[i] >= 127)
                {
                    return false;
                }
            }

            return true;
        }

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
                    result = result.ConcatBytes(sub);
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
                var num = Phantasma.Numerics.BigInteger.Parse(key);
                var result = num.ToByteArray();
                result = new byte[] { (byte)'<' }.ConcatBytes(result).ConcatBytes(new byte[] { (byte)'>' });
            }

            {
                var result = global::System.Text.Encoding.ASCII.GetBytes(key);
                if (forceSep)
                {
                    result = new byte[] { (byte)'{' }.ConcatBytes(result).ConcatBytes(new byte[] { (byte)'}' });
                }
                return result;
            }
        }

        public static string ToHumanValue(byte[] key, byte[] value)
        {
            if (key.Length == Address.PublicKeyLength)
            {
                return new global::System.Numerics.BigInteger(value).ToString();
            }

            return "0x" + Base16.Encode(value);
        }

        private static string DecodeKey(byte[] key)
        {
            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == (byte)'}')
                {
                    int index = i + 1;
                    var first = key.Take(index).ToArray();

                    if (IsASCII(first))
                    {
                        var name = global::System.Text.Encoding.ASCII.GetString(first);
                        if (name.StartsWith("{") && name.EndsWith("}"))
                        {
                            name = name.Substring(1, name.Length - 2);

                            if (i == key.Length - 1)
                            {
                                return name;
                            }

                            var temp = key.Skip(index).ToArray();

                            var rest = DecodeKey(temp);

                            if (rest == null)
                            {
                                return null;
                            }

                            return $"{name}.{rest}";
                        }
                    }

                    return null;
                }
                else
                if (key[0] == (byte)'<' && key[i] == (byte)'>')
                {
                    int index = i + 1;
                    var first = key.Take(index - 1).Skip(1).ToArray();

                    var num = new Phantasma.Numerics.BigInteger(first);

                    var name = $"[{num}]";

                    if (i == key.Length - 1)
                    {
                        return name;
                    }

                    var temp = key.Skip(index).ToArray();

                    var rest = DecodeKey(temp);

                    if (rest == null)
                    {
                        return null;
                    }

                    return $"{name}{rest}";
                }
            }

            return null;
        }

        public static string ToHumanKey(byte[] key)
        {
            if (key.Length == Address.PublicKeyLength)
            {
                return new Address(key).Text;
            }

            if (key.Length > Address.PublicKeyLength)
            {
                var address = key.Take(Address.PublicKeyLength).ToArray();
                var temp = key.Skip(Address.PublicKeyLength).ToArray();

                var rest = DecodeKey(temp);
                if (rest != null)
                {
                    return $"{ToHumanKey(address)}.{rest}";
                }
            }

            {
                var rest = DecodeKey(key);
                if (rest != null)
                {
                    return rest;
                }
            }

            return "0x" + Base16.Encode(key);
        }

        public override bool Has(byte[] key)
        {
            var sKey = new StorageKey(key);
            return Entries.ContainsKey(sKey);
        }

        public override byte[] Get(byte[] key)
        {
            var sKey = new StorageKey(key);
            var value = Entries.ContainsKey(sKey) ? Entries[sKey] : new byte[0];

            Log($"GET: {ToHumanKey(key)} => {ToHumanValue(key, value)}");

            return value;
        }

        public override void Put(byte[] key, byte[] value)
        {
            Log($"PUT: {ToHumanKey(key)} => {ToHumanValue(key, value)}");

            var sKey = new StorageKey(key);
            if (value == null) value = new byte[0]; Entries[sKey] = value;
        }

        public override void Delete(byte[] key)
        {
            Log($"DELETE: {ToHumanKey(key)}");

            var sKey = new StorageKey(key);
            if (Entries.ContainsKey(sKey)) Entries.Remove(sKey);
        }
    }
}
