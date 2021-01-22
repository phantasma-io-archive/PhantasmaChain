using Org.BouncyCastle.Crypto.Digests;

namespace Phantasma.Cryptography.Hashing
{
    public static class SHA3Keccak
    {
        public static byte[] CalculateHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }
    }

}
