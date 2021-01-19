using Phantasma.Cryptography;
using System;
using System.Linq;

namespace Phantasma.Neo.Utils
{
    public static class LuxUtils
    {
        public static bool IsValidAddress(this string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return false;
            }

            if (address.Length != 34)
            {
                return false;
            }

            byte[] buffer;
            try
            {
                buffer = Numerics.Base58.Decode(address);

            }
            catch
            {
                return false;
            }

            if (buffer.Length < 4) return false;

            byte[] checksum = buffer.Sha256(0, (uint)(buffer.Length - 4)).Sha256();
            return buffer.Skip(buffer.Length - 4).SequenceEqual(checksum.Take(4));
        }
    }
}
