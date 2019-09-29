using Phantasma.Core.Utils;
using Phantasma.Numerics;
using System;

namespace Phantasma.Cryptography
{
    public static class Entropy
    {
        //private static Random rnd = new Random();
        private static System.Security.Cryptography.RandomNumberGenerator rnd = System.Security.Cryptography.RandomNumberGenerator.Create();

        public static byte[] GetRandomBytes(int targetLength)
        {
            var bytes = new byte[targetLength];
            lock (rnd)
            {
                rnd.GetBytes(bytes);
                //rnd.NextBytes(bytes);
            }

            return bytes;
        }
    }
}
