using System;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Neo.Cryptography;

namespace Phantasma.Neo.Utils
{
    public static class CryptoUtils
    {
        public static string ToAddress(this UInt160 scriptHash)
        {
            byte[] data = new byte[21];
            data[0] = 23;
            Buffer.BlockCopy(scriptHash.ToArray(), 0, data, 1, 20);
            return data.Base58CheckEncode();
        }

        public static UInt160 ToScriptHash(this byte[] script)
        {
            return new UInt160(Hash160(script));
        }

        public static byte[] Hash160(byte[] message)
        {
            return message.Sha256().RIPEMD160();
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static byte[] AddressToScriptHash(this string s)
        {
            var bytes = s.Base58CheckDecode();
            var data = bytes.Skip(1).Take(20).ToArray();
            return data;
        }
    }
}
