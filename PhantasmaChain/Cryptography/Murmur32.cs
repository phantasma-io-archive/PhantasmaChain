/*
The MurmurHash3 algorithm was created by Austin Appleby and put into the public domain.  See http://code.google.com/p/smhasher/
This version was modified based on a version written by Elliott B. Edwards
 */

namespace Phantasma.Cryptography
{
    public static class Murmur32 
    {
        public static int Hash(byte[] data, uint seed = 144)
        {
            return Hash(data, 0, (uint)data.Length, seed);
        }

        public static int Hash(byte[] data, uint offset, uint length, uint seed)
        {
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;
            uint streamLength = 0;

            uint end = offset + length;
            while (offset < end)
            {
                var bytesLeft = end - offset;
                if (bytesLeft == 0)
                {
                    break;
                }

                if (bytesLeft > 4)
                {
                    bytesLeft = 4;
                }

                uint k1;
                switch (bytesLeft)
                {
                    case 1:
                        k1 = (uint)(data[offset + 0]);
                        break;

                    case 2:
                        k1 = (uint)(data[offset + 0] | data[offset + 1] << 8);
                        break;

                    case 3:
                        k1 = (uint)(data[offset + 0] | data[offset + 1] << 8 | data[offset + 2] << 16);
                        break;

                    default:
                        /* Get four bytes from the input into an uint */
                        k1 = (uint)(data[offset + 0] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
                        break;
                }
                offset += bytesLeft;

                k1 *= c1;
                k1 = rotl32(k1, 15);
                k1 *= c2;
                h1 ^= k1;

                if (offset >= end) {
                    h1 ^= k1;
                    h1 = rotl32(h1, 13);
                    h1 = h1 * 5 + 0xe6546b64;
                }
            }

            // finalization, magic chants to wrap it all up
            h1 ^= streamLength;
            h1 = fmix(h1);

            unchecked //ignore overflow
            {
                return (int)h1;
            }
        }

        private static uint rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }

        private static uint fmix(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;
            return h;
        }
    }
}