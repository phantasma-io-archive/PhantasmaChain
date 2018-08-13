using System;
using System.Linq;
using Phantasma.Cryptography;
using Phantasma.Utils;

namespace Phantasma.Cryptography
{
    public sealed class KeyPair 
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        public readonly string Address;

        public const int PublicKeyLength = 36;

        public KeyPair(byte[] privateKey)
        {
            if (privateKey.Length != 32)
                throw new ArgumentException();

            this.PrivateKey = new byte[32];
            Buffer.BlockCopy(privateKey, 0, PrivateKey, 0, 32);            

            this.PublicKey = Ed25519.PublicKeyFromSeed(privateKey);

            this.Address = this.PublicKey.PublicKeyToAddress();
        }

        private static Random rnd = new Random();

        public static KeyPair Generate()
        {
            var bytes = new byte[32];
            rnd.NextBytes(bytes);
            return new KeyPair(bytes);
        }

        public static KeyPair FromWIF(string wif)
        {
            if (wif == null) throw new ArgumentNullException();
            byte[] data = wif.Base58CheckDecode();
            if (data.Length != 34 || data[0] != 0x80 || data[33] != 0x01)
                throw new FormatException();
            byte[] privateKey = new byte[32];
            Buffer.BlockCopy(data, 1, privateKey, 0, privateKey.Length);
            Array.Clear(data, 0, data.Length);
            return new KeyPair(privateKey);
        }

        public string ToWIF()
        {
            byte[] data = new byte[34];
            data[0] = 0x80;
            Buffer.BlockCopy(PrivateKey, 0, data, 1, 32);
            data[33] = 0x01;
            string wif = data.Base58CheckEncode();
            Array.Clear(data, 0, data.Length);
            return wif;
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
