using System;
using System.Linq;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Numerics;
using Phantasma.Core;

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
            Array.Copy(privateKey, 0, PrivateKey, 0, PrivateKeyLength); // TODO Buffer.BlockCopy

            var publicKey = Ed25519.PublicKeyFromSeed(privateKey);

            this.Address = new Address(publicKey);
        }

        private static Random rnd = new Random(); // TODO use crypto RNG

        public static KeyPair Generate()
        {
            var privateKey = Entropy.GetRandomBytes(PrivateKeyLength);
            return new KeyPair(privateKey);
        }

        public static KeyPair FromWIF(string wif)
        {
            Throw.If(wif == null, "WIF required");

            byte[] data = wif.Base58CheckDecode();
            Throw.If(data.Length != 34 || data[0] != 0x80 || data[33] != 0x01, "Invalid WIF format");

            byte[] privateKey = new byte[32];
            Array.Copy(data, 1, privateKey, 0, privateKey.Length); // TODO Buffer.BlockCopy
            Array.Clear(data, 0, data.Length);
            return new KeyPair(privateKey);
        }

        public string ToWIF()
        {
            byte[] data = new byte[34];
            data[0] = 0x80;
            Array.Copy(PrivateKey, 0, data, 1, 32); // Buffer.BlockCopy
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

        public Signature Sign(byte[] message)
        {
            var sign = Ed25519.Sign(message, Ed25519.ExpandedPrivateKeyFromSeed(this.PrivateKey));
            return new Ed25519Signature(sign);
        }
    }
}
