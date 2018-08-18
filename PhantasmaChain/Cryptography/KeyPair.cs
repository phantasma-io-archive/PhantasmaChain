using System;
using System.Linq;
using Phantasma.Utils;

namespace Phantasma.Cryptography
{
    public enum AddressType
    {
        Personal,
        Stealth,
        Contract
    }

    public sealed class KeyPair 
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        public readonly string Address;

        public const int PrivateKeyLength = 32;
        public const int PublicKeyLength = 32;

        public KeyPair(byte[] privateKey)
        {
            Throw.If(privateKey.Length != PrivateKeyLength, $"privateKey should have length {PrivateKeyLength}");

            this.PrivateKey = new byte[PrivateKeyLength];
            Buffer.BlockCopy(privateKey, 0, PrivateKey, 0, PrivateKeyLength);            

            this.PublicKey = Ed25519.PublicKeyFromSeed(privateKey);

            this.Address = this.PublicKey.PublicKeyToAddress(AddressType.Personal);
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
            Throw.If(wif == null, "WIF required");

            byte[] data = wif.Base58CheckDecode();
            Throw.If(data.Length != 34 || data[0] != 0x80 || data[33] != 0x01, "Invalid WIF format");

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
