using System;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Utils;

namespace Phantasma.Core
{
    public sealed class KeyPair 
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        public readonly string address;

        public const int PublicKeyLength = 36;

        public KeyPair(byte[] privateKey)
        {
            if (privateKey.Length != 32)
                throw new ArgumentException();

            this.PrivateKey = new byte[32];
            Buffer.BlockCopy(privateKey, 0, PrivateKey, 0, 32);            

            this.PublicKey = Ed25519.PublicKeyFromSeed(privateKey);

            var addressBytes = EncodePublicKey(this.PublicKey);
            this.address = addressBytes.PublicKeyToAddress();
        }

        internal static byte[] EncodePublicKey(byte[] pubKey)
        {
            if (pubKey == null || pubKey.Length != 33)
            {
                throw new ArgumentException("Invalid public key format");
            }

            var checkSum = pubKey.Adler16();
            var checkbytes = BitConverter.GetBytes(checkSum);

            var addressBytes = new byte[1 + checkbytes.Length + pubKey.Length];
            addressBytes[0] = 50;
            int ofs = 1;
            Array.Copy(checkbytes, 0, addressBytes, ofs, checkbytes.Length);
            ofs += checkbytes.Length;

            Array.Copy(pubKey, 0, addressBytes, ofs, pubKey.Length);
            ofs += pubKey.Length;

            return addressBytes;
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
