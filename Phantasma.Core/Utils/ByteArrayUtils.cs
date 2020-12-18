using System;

namespace Phantasma.Core.Utils
{
    public static class ByteArrayUtils
    {
        /// <summary>
        /// Merges two byte arrays
        /// </summary>
        /// <param name="source1">first byte array</param>
        /// <param name="source2">second byte array</param>
        /// <returns>A byte array which contains source1 bytes followed by source2 bytes</returns>
        public static byte[] ConcatBytes(byte[] source1, byte[] source2)
        {
            //Most efficient way to merge two arrays this according to http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp
            var buffer = new byte[source1.Length + source2.Length];
            CopyBytes(source1, 0, buffer, 0, source1.Length);
            CopyBytes(source2, 0, buffer, source1.Length, source2.Length);

            return buffer;
        }

        public static byte[] ConcatBytes(byte source1, byte[] source2)
        {
            var buffer = new byte[1 + source2.Length];
            buffer[0] = source1;
            CopyBytes(source2, 0, buffer, 1, source2.Length);
            return buffer;
        }

        public static byte[] DupBytes(byte[] src)
        {
            if (src == null)
            {
                return null;
            }

            var tmp = new byte[src.Length];
            CopyBytes(src, 0, tmp, 0, src.Length);
            return tmp;
        }

        public static void CopyBytes(byte[] src, int spos, byte[] dst, int dpos, int len)
        {
#if BRIDGE_NET
            Array.Copy(src, spos, dst, dpos, len);
#else
            Buffer.BlockCopy(src, spos, dst, dpos, len);
#endif
        }

        public static byte[] RangeBytes(this byte[] data, int index, int length)
        {
            byte[] result = new byte[length];
            CopyBytes(data, index, result, 0, length);
            return result;
        }

        public static byte[] ReverseBytes(byte[] source)
        {
            Throw.IfNull(source, nameof(source));
            var result = new byte[source.Length];
            var last = source.Length - 1;
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = source[last];
                last--;
            }

            return result;
        }

        public static int SearchBytes(this byte[] haystack, byte[] needle)
        {
            var len = needle.Length;
            var limit = haystack.Length - len;
            for (var i = 0; i <= limit; i++)
            {
                var k = 0;
                for (; k < len; k++)
                {
                    if (needle[k] != haystack[i + k]) break;
                }
                if (k == len) return i;
            }
            return -1;
        }

        public static bool CompareBytes(this byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

    }
}
