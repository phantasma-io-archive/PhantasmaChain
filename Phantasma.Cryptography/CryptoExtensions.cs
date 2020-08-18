using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NetCrypto = System.Security.Cryptography;
using System.Text;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;

namespace Phantasma.Cryptography
{
    public static class CryptoExtensions
    {
        /*public static byte[] AES256Decrypt(this byte[] block, byte[] key)
        {
            using (var aes = Aes.Create())
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
            using (Aes aes = Aes.Create))
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
            using (var sha256 = SHA256.Create())
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] passwordHash = sha256.ComputeHash(passwordBytes);
                byte[] passwordHash2 = sha256.ComputeHash(passwordHash);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(passwordHash, 0, passwordHash.Length);
                return passwordHash2;
            }
        }

        internal static byte[] ToAesKey(this SecureString password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] passwordBytes = password.ToArray();
                byte[] passwordHash = sha256.ComputeHash(passwordBytes);
                byte[] passwordHash2 = sha256.ComputeHash(passwordHash);
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
                Array.Clear(passwordHash, 0, passwordHash.Length);
                return passwordHash2;
            }
        }

             */

        public static byte[] Base58CheckDecode(this string input)
        {
            byte[] buffer = Base58.Decode(input);
            if (buffer.Length < 4) throw new FormatException();
            byte[] expected_checksum = buffer.Sha256(0, (uint)(buffer.Length - 4)).SHA256();
            expected_checksum = expected_checksum.Take(4).ToArray();
            var src_checksum = buffer.Skip(buffer.Length - 4).ToArray();

            Throw.If(!src_checksum.SequenceEqual(expected_checksum), "WIF checksum failed");
            return buffer.Take(buffer.Length - 4).ToArray();
        }

        public static string Base58CheckEncode(this byte[] data)
        {
            byte[] checksum = data.SHA256().SHA256();
            byte[] buffer = new byte[data.Length + 4];
            Array.Copy(data, 0, buffer, 0, data.Length);
            ByteArrayUtils.CopyBytes(checksum, 0, buffer, data.Length, 4); 
            return Base58.Encode(buffer);
        }

        public static byte[] RIPEMD160(this IEnumerable<byte> value)
        {
            return new RIPEMD160().ComputeHash(value.ToArray());
        }

        public static byte[] SHA256(this IEnumerable<byte> value)
        {
            return new Hashing.SHA256().ComputeHash(value.ToArray());
        }

        public static byte[] Sha256(this string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            return bytes.SHA256();
        }

        public static byte[] Sha256(this byte[] value)
        {
            return new Hashing.SHA256().ComputeHash(value, 0, (uint)value.Length);
        }

        public static byte[] Sha256(this byte[] value, uint offset, uint count)
        {
            return new Hashing.SHA256().ComputeHash(value, offset, count);
        }

        private static byte[] TranscodeSignatureToConcat(byte[] derSignature, int outputLength)
        {
            if (derSignature.Length < 8 || derSignature[0] != 48) throw new Exception("Invalid ECDSA signature format");

            int offset;
            if (derSignature[1] > 0)
                offset = 2;
            else if (derSignature[1] == 0x81)
                offset = 3;
            else
                throw new Exception("Invalid ECDSA signature format");

            var rLength = derSignature[offset + 1];

            int i = rLength;
            while (i > 0
                   && derSignature[offset + 2 + rLength - i] == 0)
                i--;

            var sLength = derSignature[offset + 2 + rLength + 1];

            int j = sLength;
            while (j > 0
                   && derSignature[offset + 2 + rLength + 2 + sLength - j] == 0)
                j--;

            var rawLen = Math.Max(i, j);
            rawLen = Math.Max(rawLen, outputLength / 2);

            if ((derSignature[offset - 1] & 0xff) != derSignature.Length - offset
                || (derSignature[offset - 1] & 0xff) != 2 + rLength + 2 + sLength
                || derSignature[offset] != 2
                || derSignature[offset + 2 + rLength] != 2)
                throw new Exception("Invalid ECDSA signature format");

            var concatSignature = new byte[2 * rawLen];

            Array.Copy(derSignature, offset + 2 + rLength - i, concatSignature, rawLen - i, i);
            Array.Copy(derSignature, offset + 2 + rLength + 2 + sLength - j, concatSignature, 2 * rawLen - j, j);

            return concatSignature;
        }

        private static NetCrypto.ECPoint ECPointDecode(byte[] pubKey, ECDsaCurve curve)
        {
            ECCurve usedCurve = ECC.ECCurve.Secp256r1;
            switch (curve)
            {
                case ECDsaCurve.Secp256r1:
                    // default
                    break;

                case ECDsaCurve.Secp256k1:
                    var oid = NetCrypto.Oid.FromFriendlyName("secP256k1", NetCrypto.OidGroup.PublicKeyAlgorithm);
                    usedCurve = ECC.ECCurve.Secp256k1;
                    break;
            };
            byte[] bytes;

            if (pubKey.Length == 32)
            {
                pubKey = ByteArrayUtils.ConcatBytes(new byte[] { 2 }, pubKey.Skip(1).ToArray());
            }

            if (pubKey.Length == 33 && (pubKey[0] == 0x02 || pubKey[0] == 0x03))
            {
                try
                {
                    bytes = ECC.ECPoint.DecodePoint(pubKey, usedCurve).EncodePoint(false).Skip(1).ToArray();
                }
                catch
                {
                    throw new ArgumentException();
                }
            }
            else if (pubKey.Length == 65 && pubKey[0] == 0x04)
            {
                bytes = pubKey.Skip(1).ToArray();
            }
            else 
            if (pubKey.Length != 64)
            {
                throw new ArgumentException();
            }
            else
            {
                bytes = pubKey;
            }

            return new NetCrypto.ECPoint
            {
                X = bytes.Take(32).ToArray(),
                Y = bytes.Skip(32).ToArray()
            };
        }

        public static byte[] SignECDsa(byte[] message, byte[] prikey, byte[] pubkey, ECDsaCurve curve)
        {
            NetCrypto.ECCurve usedCurve = NetCrypto.ECCurve.NamedCurves.nistP256;
            switch (curve)
            {
                case ECDsaCurve.Secp256r1:
                    // default
                    break;

                case ECDsaCurve.Secp256k1:
                    var oid = NetCrypto.Oid.FromFriendlyName("secP256k1", NetCrypto.OidGroup.PublicKeyAlgorithm);
                    usedCurve = NetCrypto.ECCurve.CreateFromOid(oid);
                    break;
            };

            using (var ecdsa = NetCrypto.ECDsa.Create(new NetCrypto.ECParameters
            {
                Curve = usedCurve,
                D = prikey,
                Q = ECPointDecode(pubkey, curve)
            }))
            {
                return ecdsa.SignData(message, NetCrypto.HashAlgorithmName.SHA256);
            }
        }

        public static bool VerifySignatureECDsa(byte[] message, byte[] signature, byte[] pubkey, ECDsaCurve curve)
        {
            NetCrypto.ECCurve usedCurve = NetCrypto.ECCurve.NamedCurves.nistP256;
            switch (curve)
            {
                case ECDsaCurve.Secp256r1:
                    // default
                    break;

                case ECDsaCurve.Secp256k1:
                    var oid = NetCrypto.Oid.FromFriendlyName("secP256k1", NetCrypto.OidGroup.PublicKeyAlgorithm);
                    usedCurve = NetCrypto.ECCurve.CreateFromOid(oid);
                    break;
            };
#if NET461
            const int ECDSA_PUBLIC_P256_MAGIC = 0x31534345;
            pubkey = BitConverter.GetBytes(ECDSA_PUBLIC_P256_MAGIC).Concat(BitConverter.GetBytes(32)).Concat(pubkey).ToArray();
            using (CngKey key = CngKey.Import(pubkey, CngKeyBlobFormat.EccPublicBlob))
            using (ECDsaCng ecdsa = new ECDsaCng(key))
            {
                return ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);
            }
#else
            using (var ecdsa = NetCrypto.ECDsa.Create(new NetCrypto.ECParameters
            {
                Curve = usedCurve,
                Q = ECPointDecode(pubkey, curve)
            }))
            {
                return ecdsa.VerifyData(message, signature, NetCrypto.HashAlgorithmName.SHA256);
            }
#endif
        }

        public static bool ConstantTimeEquals(byte[] x, byte[] y)
        {
            if (x == null)
                throw new ArgumentNullException("x");
            if (y == null)
                throw new ArgumentNullException("y");
            if (x.Length != y.Length)
                throw new ArgumentException("x.Length must equal y.Length");
            return InternalConstantTimeEquals(x, 0, y, 0, x.Length) != 0;
        }

        public static bool ConstantTimeEquals(ArraySegment<byte> x, ArraySegment<byte> y)
        {
            if (x.Array == null)
                throw new ArgumentNullException("x.Array");
            if (y.Array == null)
                throw new ArgumentNullException("y.Array");
            if (x.Count != y.Count)
                throw new ArgumentException("x.Count must equal y.Count");

            return InternalConstantTimeEquals(x.Array, x.Offset, y.Array, y.Offset, x.Count) != 0;
        }

        public static bool ConstantTimeEquals(byte[] x, int xOffset, byte[] y, int yOffset, int length)
        {
            if (x == null)
                throw new ArgumentNullException("x");
            if (xOffset < 0)
                throw new ArgumentOutOfRangeException("xOffset", "xOffset < 0");
            if (y == null)
                throw new ArgumentNullException("y");
            if (yOffset < 0)
                throw new ArgumentOutOfRangeException("yOffset", "yOffset < 0");
            if (length < 0)
                throw new ArgumentOutOfRangeException("length", "length < 0");
            if (x.Length - xOffset < length)
                throw new ArgumentException("xOffset + length > x.Length");
            if (y.Length - yOffset < length)
                throw new ArgumentException("yOffset + length > y.Length");

            return InternalConstantTimeEquals(x, xOffset, y, yOffset, length) != 0;
        }

        private static uint InternalConstantTimeEquals(byte[] x, int xOffset, byte[] y, int yOffset, int length)
        {
            int differentbits = 0;
            for (int i = 0; i < length; i++)
                differentbits |= x[xOffset + i] ^ y[yOffset + i];
            return (1 & (unchecked((uint)differentbits - 1) >> 8));
        }

        public static void Wipe(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            InternalWipe(data, 0, data.Length);
        }

        public static void Wipe(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "Requires count >= 0");
            if ((uint)offset + (uint)count > (uint)data.Length)
                throw new ArgumentException("Requires offset + count <= data.Length");
            InternalWipe(data, offset, count);
        }

        public static void Wipe(ArraySegment<byte> data)
        {
            if (data.Array == null)
                throw new ArgumentNullException("data.Array");
            InternalWipe(data.Array, data.Offset, data.Count);
        }

        // Secure wiping is hard
        // * the GC can move around and copy memory
        //   Perhaps this can be avoided by using unmanaged memory or by fixing the position of the array in memory
        // * Swap files and error dumps can contain secret information
        //   It seems possible to lock memory in RAM, no idea about error dumps
        // * Compiler could optimize out the wiping if it knows that data won't be read back
        //   I hope this is enough, suppressing inlining
        //   but perhaps `RtlSecureZeroMemory` is needed
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void InternalWipe(byte[] data, int offset, int count)
        {
            Array.Clear(data, offset, count);
        }

        // shallow wipe of structs
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void InternalWipe<T>(ref T data)
            where T : struct
        {
            data = default(T);
        }

        private static int BitLen(int w)
        {
            return (w < 1 << 15 ? (w < 1 << 7
                ? (w < 1 << 3 ? (w < 1 << 1
                ? (w < 1 << 0 ? (w < 0 ? 32 : 0) : 1)
                : (w < 1 << 2 ? 2 : 3)) : (w < 1 << 5
                ? (w < 1 << 4 ? 4 : 5)
                : (w < 1 << 6 ? 6 : 7)))
                : (w < 1 << 11
                ? (w < 1 << 9 ? (w < 1 << 8 ? 8 : 9) : (w < 1 << 10 ? 10 : 11))
                : (w < 1 << 13 ? (w < 1 << 12 ? 12 : 13) : (w < 1 << 14 ? 14 : 15)))) : (w < 1 << 23 ? (w < 1 << 19
                ? (w < 1 << 17 ? (w < 1 << 16 ? 16 : 17) : (w < 1 << 18 ? 18 : 19))
                : (w < 1 << 21 ? (w < 1 << 20 ? 20 : 21) : (w < 1 << 22 ? 22 : 23))) : (w < 1 << 27
                ? (w < 1 << 25 ? (w < 1 << 24 ? 24 : 25) : (w < 1 << 26 ? 26 : 27))
                : (w < 1 << 29 ? (w < 1 << 28 ? 28 : 29) : (w < 1 << 30 ? 30 : 31)))));
        }

        internal static BigInteger NextBigInteger(int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;

            var b = Entropy.GetRandomBytes(sizeInBits / 8 + 1);

            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);

            return BigInteger.FromUnsignedArray(b, isPositive: true);
        }

        public static BigInteger GenerateInteger(this HMACDRBG rng, BigInteger max, int securityParameter = 64)
        {
            // The simple modular method from the NIST SP800-90A recommendation
            Throw.If(securityParameter < 64, "Given security parameter, " + securityParameter + ", is too low.");

            var bytesToRepresent = max.ToSignedByteArray().Length;
            var bytes = new byte[bytesToRepresent + securityParameter / 8 + 1];
            rng.GetBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            return BigInteger.FromSignedArray(bytes) % max;
        }

        public static byte[] Hash256(byte[] message)
        {
            return message.SHA256().SHA256();
        }
    }
}
