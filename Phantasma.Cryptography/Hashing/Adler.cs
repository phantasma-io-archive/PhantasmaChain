using System.Collections.Generic;
using System.Text;

namespace Phantasma.Cryptography.Hashing
{
    public static class Adler
    {
        public static ushort Adler16(this string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            return bytes.Adler16();
        }

        public static uint Adler32(this string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            return bytes.Adler32();
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
    }
}
