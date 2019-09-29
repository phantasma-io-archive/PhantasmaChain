using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
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

        public static byte[] AES256Decrypt(this byte[] block, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(block, 0, block.Length);
                }
            }
        }

        public static byte[] AES256Encrypt(this byte[] block, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(block, 0, block.Length);
                }
            }
        }

        public static byte[] AesDecrypt(this byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || key == null || iv == null) throw new ArgumentNullException();
            if (data.Length % 16 != 0 || key.Length != 32 || iv.Length != 16) throw new ArgumentException();
            using (Aes aes = Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform decryptor = aes.CreateDecryptor(key, iv))
                {
                    return decryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        public static byte[] AesEncrypt(this byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || key == null || iv == null) throw new ArgumentNullException();
            if (data.Length % 16 != 0 || key.Length != 32 || iv.Length != 16) throw new ArgumentException();
            using (Aes aes = Aes.Create())
            {
                aes.Padding = PaddingMode.None;
                using (ICryptoTransform encryptor = aes.CreateEncryptor(key, iv))
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        internal static byte[] ToAesKey(this string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] passwordHash = sha256.ComputeHash(passwordBytes);
                byte[] passwordHash2 = sha256.ComputeHash(passwordHash);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(passwordHash, 0, passwordHash.Length);
                return passwordHash2;
            }
        }

        public static byte[] AddressToScriptHash(this string s)
        {
            var bytes = s.Base58CheckDecode();
            var data = bytes.Skip(1).Take(20).ToArray();
            return data;
        }
    }
}
