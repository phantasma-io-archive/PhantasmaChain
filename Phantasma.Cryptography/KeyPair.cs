using System;
using System.Linq;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Core;
using Phantasma.Core.Utils;

namespace Phantasma.Cryptography
{
    public interface IKeyPair
    {
        byte[] PrivateKey { get; }
        byte[] PublicKey { get; }

        // byte[] customSignFunction(byte[] message, byte[] prikey, byte[] pubkey)
        // allows singning with custom crypto libs.
        Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null);
    }

    public sealed class PhantasmaKeys : IKeyPair
    {
        public byte[] PrivateKey { get; private set; }
        public byte[] PublicKey { get; private set; }

        public readonly Address Address;

        public const int PrivateKeyLength = 32;

        public PhantasmaKeys(byte[] privateKey)
        {
            Throw.If(privateKey.Length != PrivateKeyLength, $"privateKey should have length {PrivateKeyLength}");

            this.PrivateKey = new byte[PrivateKeyLength];
            ByteArrayUtils.CopyBytes(privateKey, 0, PrivateKey, 0, PrivateKeyLength); 

            this.PublicKey = Ed25519.PublicKeyFromSeed(privateKey);
            this.Address = Address.FromKey(this);
        }

        public override string ToString()
        {
            return Address.Text;
        }

        public static PhantasmaKeys Generate()
        {
            var privateKey = Entropy.GetRandomBytes(PrivateKeyLength);
            var pair = new PhantasmaKeys(privateKey);
            return pair;
        }

        public static PhantasmaKeys FromWIF(string wif)
        {
            Throw.If(wif == null, "WIF required");

            byte[] data = wif.Base58CheckDecode();
            Throw.If(data.Length != 34 || data[0] != 0x80 || data[33] != 0x01, "Invalid WIF format");

            byte[] privateKey = new byte[32];
            ByteArrayUtils.CopyBytes(data, 1, privateKey, 0, privateKey.Length); 
            Array.Clear(data, 0, data.Length);
            return new PhantasmaKeys(privateKey);
        }

        public string ToWIF()
        {
            byte[] data = new byte[34];
            data[0] = 0x80;
            ByteArrayUtils.CopyBytes(PrivateKey, 0, data, 1, 32); 
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

        public Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
        {
            return Ed25519Signature.Generate(this, msg);
        }
    }
}
