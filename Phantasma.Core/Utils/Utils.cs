using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Phantasma.Core.Utils
{
    public static class Utils
    {
        public static string ToTitleCase(this string str)
        {
            var tokens = str.Split(new[] { " ", "-" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                tokens[i] = char.ToUpper(token[0]) + token.Substring(1).ToLower();
            }

            return string.Join(" ", tokens);
        }

        public static byte[] ConcatBytes(this byte[] a1, byte[] a2)
        {
            byte[] res = new byte[a1.Length + a2.Length];
            Array.Copy(a1, 0, res, 0, a1.Length);
            Array.Copy(a2, 0, res, a1.Length, a2.Length);
            return res;
        }

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32(this byte[] value, int startIndex)
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

        /// <summary>
        /// Merges two byte arrays
        /// </summary>
        /// <param name="source1">first byte array</param>
        /// <param name="source2">second byte array</param>
        /// <returns>A byte array which contains source1 bytes followed by source2 bytes</returns>
        public static byte[] MergeByteArrays(byte[] source1, byte[] source2)
        {
            //Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
            var buffer = new byte[source1.Length + source2.Length];
            Buffer.BlockCopy(source1, 0, buffer, 0, source1.Length);
            Buffer.BlockCopy(source2, 0, buffer, source1.Length, source2.Length);

            return buffer;
        }
    }
}
