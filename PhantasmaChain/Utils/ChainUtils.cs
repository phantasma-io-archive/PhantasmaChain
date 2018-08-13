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
        public static byte[] HexToBytes(this string value)
        {
            if (value == null || value.Length == 0)
            {
                return new byte[0];
            }

            if (value.StartsWith("0x"))
            {
                return value.Substring(2).HexToBytes();
            }

            if (value.Length % 2 == 1)
            {
                throw new FormatException();
            }

            byte[] result = new byte[value.Length / 2];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            }

            return result;
        }

        // constant time hex conversion
        // see http://stackoverflow.com/a/14333437/445517
        //
        // An explanation of the weird bit fiddling:
        //
        // 1. `bytes[i] >> 4` extracts the high nibble of a byte  
        //   `bytes[i] & 0xF` extracts the low nibble of a byte
        // 2. `b - 10`  
        //    is `< 0` for values `b < 10`, which will become a decimal digit  
        //    is `>= 0` for values `b > 10`, which will become a letter from `A` to `F`.
        // 3. Using `i >> 31` on a signed 32 bit integer extracts the sign, thanks to sign extension.
        //    It will be `-1` for `i < 0` and `0` for `i >= 0`.
        // 4. Combining 2) and 3), shows that `(b-10)>>31` will be `0` for letters and `-1` for digits.
        // 5. Looking at the case for letters, the last summand becomes `0`, and `b` is in the range 10 to 15. We want to map it to `A`(65) to `F`(70), which implies adding 55 (`'A'-10`).
        // 6. Looking at the case for digits, we want to adapt the last summand so it maps `b` from the range 0 to 9 to the range `0`(48) to `9`(57). This means it needs to become -7 (`'0' - 55`).  
        // Now we could just multiply with 7. But since -1 is represented by all bits being 1, we can instead use `& -7` since `(0 & -7) == 0` and `(-1 & -7) == -7`.
        //
        // Some further considerations:
        //
        // * I didn't use a second loop variable to index into `c`, since measurement shows that calculating it from `i` is cheaper. 
        // * Using exactly `i < bytes.Length` as upper bound of the loop allows the JITter to eliminate bounds checks on `bytes[i]`, so I chose that variant.
        // * Making `b` an int avoids unnecessary conversions from and to byte.
        public static string ByteToHex(this byte[] data)
        {
            if (data == null)
                return null;
            char[] c = new char[data.Length * 2];
            int b;
            for (int i = 0; i < data.Length; i++)
            {
                b = data[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = data[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c);
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
        internal static uint ToUInt32(this byte[] value, int startIndex)
        {
            var a = value[startIndex]; startIndex++;
            var b = value[startIndex]; startIndex++;
            var c = value[startIndex]; startIndex++;
            var d = value[startIndex]; startIndex++;
            return (uint)(a + (b << 8) + (c << 16) + (d << 24));
            /*fixed (byte* pbyte = &value[startIndex])
            {
                return *((uint*)pbyte);
            }*/
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
