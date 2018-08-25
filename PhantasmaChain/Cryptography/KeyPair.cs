using System;
using System.Linq;
using Phantasma.Cryptography.EdDSA;
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

        private static Random rnd = new Random(); // TODO use crypto RNG

        public static KeyPair Generate()
        {
            var securityParameter = 64;
            var bytes = new byte[PrivateKeyLength + (securityParameter / 8) + 1];
            rnd.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0;

            var maxBytes = new byte[PrivateKeyLength];
            for (int i=0; i<maxBytes.Length; i++)
            {
                maxBytes[i] = 255;
            }

            var n = new BigInteger(bytes);
            var max = new BigInteger(maxBytes);
            
            var q = n % max;

            return new KeyPair(q.ToByteArray());
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

        public Signature Sign(byte[] message)
        {
            var sign = Ed25519.Sign(message, Ed25519.ExpandedPrivateKeyFromSeed(this.PrivateKey));
            return new Ed25519Signature(sign);
        }
    }
}
