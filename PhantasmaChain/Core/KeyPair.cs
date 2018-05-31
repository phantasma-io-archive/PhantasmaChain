using PhantasmaChain.Utils;
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

            var pubbytes = pKey.EncodePoint(true).ToArray();

            var checkSum = pubbytes.Adler16();
            var checkbytes = BitConverter.GetBytes(checkSum);

            var addressBytes = new byte[1 + checkbytes.Length + pubbytes.Length];
            addressBytes[0] = 50;
            int ofs = 1;
            Array.Copy(checkbytes, 0, addressBytes, ofs, checkbytes.Length);
            ofs += checkbytes.Length;

            Array.Copy(pubbytes, 0, addressBytes, ofs, pubbytes.Length);
            ofs += pubbytes.Length;

            this.address = addressBytes.PublicKeyToAddress();
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
