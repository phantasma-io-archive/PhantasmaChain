using Phantasma.Cryptography;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Phantasma.Utils
{
    public delegate void Logger(string text);

    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }
            return left.SequenceEqual(right);
        }
        public int GetHashCode(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException("key");
            return key.Sum(b => b);
        }
    }

    public static class ChainUtils
    {
        public static void WriteBigInteger(this BinaryWriter writer, BigInteger n)
        {
            var bytes = n.ToByteArray();
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteByteArray(this BinaryWriter writer, byte[] bytes)
        {
            if (bytes == null)
            {
                writer.Write((byte)0);
                return;
            }
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static void WriteShortString(this BinaryWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                writer.Write((byte)0);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        public static BigInteger ReadBigInteger(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            var bytes = reader.ReadBytes(length);
            return new BigInteger(bytes);
        }

        public static byte[] ReadByteArray(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return null;
            var bytes = reader.ReadBytes(length);
            return bytes;
        }

        public static string ReadShortString(this BinaryReader reader)
        {
            var length = reader.ReadByte();
            if (length == 0)
                return null;
            var bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static byte[] HexToBytes(this string value)
        {
            if (value == null || value.Length == 0)
                return new byte[0];
            if (value.Length % 2 == 1)
                throw new FormatException();
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            return result;
        }

        public static string ByteToHex(this byte[] data)
        {
            string hex = BitConverter.ToString(data).Replace("-", "");
            return hex;
        }

        internal static bool TestBit(this BigInteger i, int index)
        {
            return (i & (BigInteger.One << index)) > BigInteger.Zero;
        }

        private static readonly DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToDateTime(this uint timestamp)
        {
            return unixEpoch.AddSeconds(timestamp).ToLocalTime();
        }

        public static DateTime ToDateTime(this ulong timestamp)
        {
            return unixEpoch.AddSeconds(timestamp).ToLocalTime();
        }


        public static uint ToTimestamp(this DateTime time)
        {
            return (uint)(time.ToUniversalTime() - unixEpoch).TotalSeconds;
        }

        public static string ToHexString(this IEnumerable<byte> value)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in value)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe internal static int ToInt32(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((int*)pbyte);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe internal static long ToInt64(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((long*)pbyte);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe internal static ushort ToUInt16(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((ushort*)pbyte);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe internal static uint ToUInt32(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((uint*)pbyte);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe internal static ulong ToUInt64(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((ulong*)pbyte);
            }
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

        public static ushort Adler16(this IEnumerable<byte> data)
        {
            const byte mod = 251;
            ushort a = 1, b = 0;
            foreach (byte c in data)
            {
                a = (ushort)((a + c) % mod);
                b = (ushort)((b + a) % mod);
            }
            return (ushort)((b << 16) | a);
        }

        public static uint Adler32(this IEnumerable<byte> data)
        {
            const int mod = 65521;
            uint a = 1, b = 0;
            foreach (byte c in data)
            {
                a = (a + c) % mod;
                b = (b + a) % mod;
            }
            return (b << 16) | a;
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

    }
}
