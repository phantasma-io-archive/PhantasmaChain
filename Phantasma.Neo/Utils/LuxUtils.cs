using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Phantasma.Neo.Utils
{
    public static class LuxUtils
    {
        public static BigInteger ToBigInteger(this decimal val, int places = 8)
        {
            while (places > 0)
            {
                val *= 10;
                places--;
            }

            return (long)val;
        }

        public static decimal ToDecimal(this BigInteger val, int places = 8)
        {
            var result = (decimal)((long)val);
            while (places > 0)
            {
                result /= 10m;
                places--;
            }

            return result;
        }

        public static string ReverseHex(this string hex)
        {

            string result = "";
            for (var i = hex.Length - 2; i >= 0; i -= 2)
            {
                result += hex.Substring(i, 2);
            }
            return result;
        }

        public static bool IsValidAddress(this string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }

            if (address.Length != 34)
            {
                return false;
            }

            byte[] buffer;
            try
            {
                buffer = Numerics.Base58.Decode(address);

            }
            catch
            {
                return false;
            }

            if (buffer.Length < 4) return false;

            byte[] checksum = buffer.Sha256(0, (uint)(buffer.Length - 4)).Sha256();
            return buffer.Skip(buffer.Length - 4).SequenceEqual(checksum.Take(4));
        }

        public static byte[] ReadVarBytes(this BinaryReader reader, int max = 0X7fffffc7)
        {
            var len = (int)reader.ReadVarInt((ulong)max);
            if (len == 0) return null;
            return reader.ReadBytes(len);
        }

        public static ulong ReadVarInt(this BinaryReader reader, ulong max = ulong.MaxValue)
        {
            byte fb = reader.ReadByte();
            ulong value;
            if (fb == 0xFD)
                value = reader.ReadUInt16();
            else if (fb == 0xFE)
                value = reader.ReadUInt32();
            else if (fb == 0xFF)
                value = reader.ReadUInt64();
            else
                value = fb;
            if (value > max) throw new FormatException();
            return value;
        }

        public static string ReadVarString(this BinaryReader reader)
        {
            return Encoding.UTF8.GetString(reader.ReadVarBytes());
        }

        //public static void WriteVarBytes(this BinaryWriter writer, byte[] value)
        //{
        //    if (value == null)
        //    {
        //        writer.WriteVarInt(0);
        //        return;
        //    }
        //    writer.WriteVarInt(value.Length);
        //    writer.Write(value);
        //}

        //public static void WriteVarInt(this BinaryWriter writer, long value)
        //{
        //    if (value < 0)
        //        throw new ArgumentOutOfRangeException();
        //    if (value < 0xFD)
        //    {
        //        writer.Write((byte)value);
        //    }
        //    else if (value <= 0xFFFF)
        //    {
        //        writer.Write((byte)0xFD);
        //        writer.Write((ushort)value);
        //    }
        //    else if (value <= 0xFFFFFFFF)
        //    {
        //        writer.Write((byte)0xFE);
        //        writer.Write((uint)value);
        //    }
        //    else
        //    {
        //        writer.Write((byte)0xFF);
        //        writer.Write(value);
        //    }
        //}

        //public static void WriteVarString(this BinaryWriter writer, string value)
        //{
        //    writer.WriteVarBytes(value != null ? Encoding.UTF8.GetBytes(value) : null);
        //}

        public static void WriteFixed(this BinaryWriter writer, decimal value)
        {
            long D = 100000000;
            value *= D;
            writer.Write((long)value);
        }

        public static decimal ReadFixed(this BinaryReader reader)
        {
            var val = reader.ReadInt64();
            long D = 100000000;
            decimal r = val;
            r /= (decimal)D;
            return r;
        }

        public static string ByteToHex(this byte[] data)
        {
            string hex = BitConverter.ToString(data).Replace("-", "").ToLower();
            return hex;
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

        public static byte[] AddressToScriptHash(this string s)
        {
            var bytes = s.Base58CheckDecode();
            var data = bytes.Skip(1).Take(20).ToArray();
            return data;
        }

        internal static int GetBitLength(this BigInteger i)
        {
            byte[] b = i.ToByteArray();
            return (b.Length - 1) * 8 + BitLen(i.Sign > 0 ? b[b.Length - 1] : 255 - b[b.Length - 1]);
        }

        internal static int GetLowestSetBit(this BigInteger i)
        {
            if (i.Sign == 0)
                return -1;
            byte[] b = i.ToByteArray();
            int w = 0;
            while (b[w] == 0)
                w++;
            for (int x = 0; x < 8; x++)
                if ((b[w] & 1 << x) > 0)
                    return x + w * 8;
            throw new Exception();
        }

        internal static BigInteger Mod(this BigInteger x, BigInteger y)
        {
            x %= y;
            if (x.Sign < 0)
                x += y;
            return x;
        }

        internal static BigInteger ModInverse(this BigInteger a, BigInteger n)
        {
            BigInteger i = n, v = 0, d = 1;
            while (a > 0)
            {
                BigInteger t = i / a, x = a;
                a = i % x;
                i = x;
                x = d;
                d = v - t * x;
                v = x;
            }
            v %= n;
            if (v < 0) v = (v + n) % n;
            return v;
        }

        internal static BigInteger NextBigInteger(this Random rand, int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;
            byte[] b = new byte[sizeInBits / 8 + 1];
            rand.NextBytes(b);
            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);
            return new BigInteger(b);
        }

        internal static bool TestBit(this BigInteger i, int index)
        {
            return (i & (BigInteger.One << index)) > BigInteger.Zero;
        }

        internal static long WeightedAverage<T>(this IEnumerable<T> source, Func<T, long> valueSelector, Func<T, long> weightSelector)
        {
            long sum_weight = 0;
            long sum_value = 0;
            foreach (T item in source)
            {
                long weight = weightSelector(item);
                sum_weight += weight;
                sum_value += valueSelector(item) * weight;
            }
            if (sum_value == 0) return 0;
            return sum_value / sum_weight;
        }

        public static string HexToString(this string hex)
        {
            if (hex.Length % 2 != 0)
            {
                throw new ArgumentException();
            }
            var output = "";
            for (int i = 0; i <= hex.Length - 2; i += 2)
            {
                try
                {
                    var result = Convert.ToByte(new string(hex.Skip(i).Take(2).ToArray()), 16);
                    output += (Convert.ToChar(result));
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return output;
        }

        //public static byte[] HexToBytes(this string value)
        //{
        //    if (value == null || value.Length == 0)
        //        return new byte[0];
        //    if (value.Length % 2 == 1)
        //        throw new FormatException();

        //    if (value.StartsWith("0x"))
        //    {
        //        value = value.Substring(2);
        //    }

        //    byte[] result = new byte[value.Length / 2];
        //    for (int i = 0; i < result.Length; i++)
        //        result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
        //    return result;
        //}

        internal static BigInteger NextBigInteger(this RandomNumberGenerator rng, int sizeInBits)
        {
            if (sizeInBits < 0)
                throw new ArgumentException("sizeInBits must be non-negative");
            if (sizeInBits == 0)
                return 0;
            byte[] b = new byte[sizeInBits / 8 + 1];
            rng.GetBytes(b);
            if (sizeInBits % 8 == 0)
                b[b.Length - 1] = 0;
            else
                b[b.Length - 1] &= (byte)((1 << sizeInBits % 8) - 1);
            return new BigInteger(b);
        }

        internal static IEnumerable<TResult> WeightedFilter<T, TResult>(this IList<T> source, double start, double end, Func<T, long> weightSelector, Func<T, long, TResult> resultSelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (start < 0 || start > 1) throw new ArgumentOutOfRangeException(nameof(start));
            if (end < start || start + end > 1) throw new ArgumentOutOfRangeException(nameof(end));
            if (weightSelector == null) throw new ArgumentNullException(nameof(weightSelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));
            if (source.Count == 0 || start == end) yield break;
            double amount = source.Sum(weightSelector);
            long sum = 0;
            double current = 0;
            foreach (T item in source)
            {
                if (current >= end) break;
                long weight = weightSelector(item);
                sum += weight;
                double old = current;
                current = sum / amount;
                if (current <= start) continue;
                if (old < start)
                {
                    if (current > end)
                    {
                        weight = (long)((end - start) * amount);
                    }
                    else
                    {
                        weight = (long)((current - start) * amount);
                    }
                }
                else if (current > end)
                {
                    weight = (long)((end - old) * amount);
                }
                yield return resultSelector(item, weight);
            }
        }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToDateTime(this uint timestamp)
        {
            return unixEpoch.AddSeconds(timestamp);
        }

        public static DateTime ToDateTime(this ulong timestamp)
        {
            return unixEpoch.AddSeconds(timestamp);
        }

    }

}
