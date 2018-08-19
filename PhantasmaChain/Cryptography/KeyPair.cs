using System;
using System.Linq;
using Phantasma.Mathematics;
using Phantasma.Utils;
using Phantasma.VM.Types;

namespace Phantasma.Cryptography
{
    public sealed class KeyPair 
    {
        public readonly byte[] PrivateKey;
        public readonly Address Address;

        public const int PrivateKeyLength = 32;

        public KeyPair(byte[] privateKey)
        {
            Throw.If(privateKey.Length != PrivateKeyLength, $"privateKey should have length {PrivateKeyLength}");

            this.PrivateKey = new byte[PrivateKeyLength];
            Buffer.BlockCopy(privateKey, 0, PrivateKey, 0, PrivateKeyLength);            

            var publicKey = Ed25519.PublicKeyFromSeed(privateKey);

            this.Address = new Address(publicKey);
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

        public byte[] Sign(byte[] message)
        {
            return Ed25519.Sign(message, this.PrivateKey);
        }

        public static bool VerifySignature(byte[] message, byte[] signature, Address address)
        {
            return Ed25519.Verify(message, signature, address.PublicKey);
        }
    }
}
