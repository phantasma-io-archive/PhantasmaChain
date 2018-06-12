using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Utils
{
    public static class ExtensionMethods
    {
        public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
        {
            foreach (var item in collection)
            {
                action(item);
            }
        }

        /// <summary>
        /// ForEach with an index.
        /// </summary>
        public static void ForEachWithIndex<T>(this IEnumerable<T> collection, Action<T, int> action)
        {
            int n = 0;

            foreach (var item in collection)
            {
                action(item, n++);
            }
        }

        /// <summary>
        /// Implements ForEach for non-generic enumerators.
        /// </summary>
        // Usage: Controls.ForEach<Control>(t=>t.DoSomething());
        public static void ForEach<T>(this IEnumerable collection, Action<T> action)
        {
            foreach (T item in collection)
            {
                action(item);
            }
        }

        public static void ForEach(this int n, Action action)
        {
            for (int i = 0; i < n; i++)
            {
                action();
            }
        }

        public static void ForEach(this int n, Action<int> action)
        {
            for (int i = 0; i < n; i++)
            {
                action(i);
            }
        }

        public static IEnumerable<int> Range(this int n)
        {
            return Enumerable.Range(0, n);
        }

        public static void MoveToTail<T>(this List<T> list, T item, Predicate<T> pred)
        {
            int idx = list.FindIndex(pred);
            list.RemoveAt(idx);
            list.Add(item);
        }

        public static void AddMaximum<T>(this List<T> list, T item, int max)
        {
            list.Add(item);

            if (list.Count > max)
            {
                list.RemoveAt(0);
            }
        }

        public static void AddDistinct<T>(this List<T> list, T item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        public static bool ContainsBy<T, TKey>(this List<T> list, T item, Func<T, TKey> keySelector)
        {
            TKey itemKey = keySelector(item);

            return list.Any(n => keySelector(n).Equals(itemKey));
        }

        public static void AddDistinctBy<T, TKey>(this List<T> list, T item, Func<T, TKey> keySelector)
        {
            TKey itemKey = keySelector(item);

            // no items in the list must match the item.
            if (list.None(q => keySelector(q).Equals(itemKey)))
            {
                list.Add(item);
            }
        }

        // TODO: Change the equalityComparer to a KeySelector for the these extension methods:
        public static void AddRangeDistinctBy<T>(this List<T> target, IEnumerable<T> src, Func<T, T, bool> equalityComparer)
        {
            src.ForEach(item =>
            {
                // no items in the list must match the item.
                if (target.None(q => equalityComparer(q, item)))
                {
                    target.Add(item);
                }
            });
        }

        public static IEnumerable<T> ExceptBy<T, TKey>(this IEnumerable<T> src, T item, Func<T, TKey> keySelector)
        {
            TKey itemKey = keySelector(item);

            using (var enumerator = src.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    T current = enumerator.Current;

                    if (!keySelector(current).Equals(itemKey))
                    {
                        yield return current;
                    }
                }
            }
        }

        public static IEnumerable<T> ExceptBy<T, TKey>(this IEnumerable<T> src, IEnumerable<T> items, Func<T, TKey> keySelector)
        {
            using (var enumerator = src.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    T current = enumerator.Current;

                    if (items.None(i => keySelector(current).Equals(keySelector(i))))
                    {
                        yield return current;
                    }
                }
            }
        }

        public static bool None<TSource>(this IEnumerable<TSource> source)
        {
            return !source.Any();
        }

        public static bool None<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            return !source.Any(predicate);
        }

        public static void RemoveRange<T>(this List<T> target, List<T> src)
        {
            src.ForEach(s => target.Remove(s));
        }

        public static void RemoveRange<T>(this List<T> target, List<T> src, Func<T, T, bool> equalityComparer)
        {
            src.ForEach(s =>
            {
                int idx = target.FindIndex(t => equalityComparer(t, s));

                if (idx != -1)
                {
                    target.RemoveAt(idx);
                }
            });
        }

        public static int Mod(this int a, int b)
        {
            return (a % b + b) % b;
        }

        public static T Second<T>(this List<T> items)
        {
            return items[1];
        }

        /// <summary>
        /// Little endian conversion of bytes to bits.
        /// </summary>
		public static IEnumerable<bool> Bits(this byte[] bytes)
        {
            IEnumerable<bool> GetBits(byte b)
            {
                byte shifter = 0x01;

                for (int i = 0; i < 8; i++)
                {
                    yield return (b & shifter) != 0;
                    shifter <<= 1;
                }
            }

            return bytes.SelectMany(GetBits);
        }

        /// <summary>
        /// Value cannot exceed max.
        /// </summary>
        public static int Min(this int a, int max)
        {
            return (a > max) ? max : a;
        }

        /// <summary>
        /// Value cannot be less than min.
        /// </summary>
        public static int Max(this int a, int min)
        {
            return (a < min) ? min : a;
        }

        public static T Next<T>(this IEnumerable<T> source)
        {
            using (var enumerator = source.GetEnumerator())
            {
                enumerator.MoveNext();

                return enumerator.Current;
            }
        }

        public static bool IsNext<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator())
            {
                enumerator.MoveNext();
                return predicate(enumerator.Current);
            }
        }

        /// <summary>
        /// Append a 0 to the byte array so that when converting to a BigInteger, the value remains positive.
        /// </summary>
        public static byte[] Append0(this byte[] b)
        {
            return b.Concat(new byte[] { 0 }).ToArray();
        }

        public static bool ApproximatelyEquals(this double d, double val, double range)
        {
            return d >= val - range && d <= val + range;
        }

        // Welford's method: https://mathoverflow.net/questions/70345/numerically-most-robust-way-to-compute-sum-of-products-standard-deviation-in-f
        // From: https://stackoverflow.com/questions/2253874/standard-deviation-in-linq
        public static double StdDev(this IEnumerable<double> values)
        {
            double mean = 0.0;
            double sum = 0.0;
            double stdDev = 0.0;
            int n = 0;
            foreach (double val in values)
            {
                n++;
                double delta = val - mean;
                mean += delta / n;
                sum += delta * (val - mean);
            }
            if (1 < n)
                stdDev = Math.Sqrt(sum / (n - 1));

            return stdDev;
        }

        public static byte[] to_Utf8(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }
    }
}
