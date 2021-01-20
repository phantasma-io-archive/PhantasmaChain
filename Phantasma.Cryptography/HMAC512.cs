using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Phantasma.Cryptography
{
    public static class HMAC512
    {
        public static byte[] ComputeHash(byte[] key, byte[] msg)
        {
            var hmac = new HMac(new Sha512Digest());
            hmac.Init(new KeyParameter(key));
            byte[] result = new byte[hmac.GetMacSize()];

            hmac.BlockUpdate(msg, 0, msg.Length);
            hmac.DoFinal(result, 0);

            return result;
        }
    }
}
