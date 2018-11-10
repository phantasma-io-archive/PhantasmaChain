using Phantasma.Numerics;
using System;
using System.Linq;

namespace Phantasma.Cryptography
{
    public static class Entropy
    {
        private static Random rnd = new Random();

        private static readonly int securityParameter = 64;

        // TODO this should use secure random generation instead of System.Random!!
        public static byte[] GetRandomBytes(int targetLength)
        {
            var bytes = new byte[targetLength + (securityParameter / 8) + 1];
            rnd.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0;

            var maxBytes = new byte[targetLength];
            for (int i = 0; i < maxBytes.Length; i++)
            {
                maxBytes[i] = 255;
            }

            var n = new BigInteger(bytes);
            var max = new BigInteger(maxBytes);

            var q = n % max;

            bytes = q.ToByteArray();

            // TODO this is a fix for a bug that appears sometimes, it appears that the math before has a mistake somewhere
            var diff = targetLength - bytes.Length;
            if (diff > 0)
            {
                var pad = new byte[diff];
                rnd.NextBytes(pad);

                bytes = bytes.Concat(pad).ToArray();
            }

            return bytes;
        }
    }
}
