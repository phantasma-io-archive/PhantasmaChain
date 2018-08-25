using Phantasma.Cryptography;
using Phantasma.Mathematics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Phantasma.Utils
{
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ToUInt32(this byte[] value, int startIndex)
        {
            var a = value[startIndex]; startIndex++;
            var b = value[startIndex]; startIndex++;
            var c = value[startIndex]; startIndex++;
            var d = value[startIndex]; startIndex++;
            return (uint)(a + (b << 8) + (c << 16) + (d << 24));
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

        public static byte[] Concat(this byte[] a1, byte[] a2)
        {
            byte[] res = new byte[a1.Length + a2.Length];
            Buffer.BlockCopy(a1, 0, res, 0, a1.Length);
            Buffer.BlockCopy(a2, 0, res, a1.Length, a2.Length);
            return res;
        }

        public static BigInteger HexToUnsignedInteger(this string hex)
        {
            hex = ("0" + hex).Replace(" ", "").Replace("\n", "").Replace("\r", "");
            return BigInteger.Parse(hex, 16);
        }

        public static BigInteger Mod(this BigInteger x, BigInteger module)
        {
            return x >= 0 ? (x % module) : module + (x % module);
        }

        public static BigInteger GenerateInteger(this HMACDRBG rng, BigInteger max, int securityParameter = 64)
        { 
            // The simple modular method from the NIST SP800-90A recommendation
            Throw.If(securityParameter < 64, "Given security parameter, " + securityParameter + ", is too low.");

            var bytesToRepresent = max.ToByteArray().Length;
            var bytes = new byte[bytesToRepresent + securityParameter / 8 + 1];
            rng.GetBytes(bytes);
            bytes[bytes.Length - 1] = 0;
            return new BigInteger(bytes) % max;
        }

        public static BigInteger FlipBit(this BigInteger number, int bit)
        {
            return number ^ (BigInteger.One << bit);
        }

        /*public static int BitLength(this BigInteger number)
        {
            return (int)Math.Ceiling(BigInteger.Log(number, 2));
        }*/
    }
}
