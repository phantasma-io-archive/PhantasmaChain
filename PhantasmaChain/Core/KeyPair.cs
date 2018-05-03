using PhantasmaChain.Cryptography;
using PhantasmaChain.Cryptography.ECC;
using System;
using System.Linq;

namespace PhantasmaChain.Core
{
    public class KeyPair 
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        public readonly string address;

        public KeyPair(byte[] privateKey)
        {
            if (privateKey.Length != 32)
                throw new ArgumentException();

            this.PrivateKey = new byte[32];
            Buffer.BlockCopy(privateKey, privateKey.Length - 32, PrivateKey, 0, 32);

            ECPoint pKey;

            pKey = ECCurve.Secp256r1.G * privateKey;

            var bytes = pKey.EncodePoint(true).ToArray();
            this.PublicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            this.address = ChainUtils.PublicKeyToAddress(this.PublicKey);
        }

        public static KeyPair Random()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);
            return new KeyPair(bytes);
        }

        private static byte[] XOR(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) throw new ArgumentException();
            return x.Zip(y, (a, b) => (byte)(a ^ b)).ToArray();
        }

        public static byte[] GetScriptHashFromAddress(string address)
        {
            var temp = address.Base58CheckDecode();
            temp = temp.SubArray(1, 20);
            return temp;
        }
    }
}
