using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;
using System;
using System.Linq;

namespace Phantasma.Neo.Core
{
    public class NeoKeys: IKeyPair 
    {
        public byte[] PrivateKey { get; private set; }
        public byte[] PublicKey { get; private set; }
        public readonly byte[] UncompressedPublicKey;
        public readonly UInt160 PublicKeyHash;
        public readonly string address;
        public readonly string WIF;

        public readonly UInt160 signatureHash;
        public readonly byte[] signatureScript;

        public NeoKeys(byte[] privateKey)
        {
            if (privateKey.Length != 32 && privateKey.Length != 96 && privateKey.Length != 104)
                throw new ArgumentException();
            this.PrivateKey = new byte[32];
            Buffer.BlockCopy(privateKey, privateKey.Length - 32, PrivateKey, 0, 32);

            ECPoint pubKey;

            if (privateKey.Length == 32)
            {
                pubKey = ECCurve.Secp256r1.G * privateKey;
            }
            else
            {
                pubKey = ECPoint.FromBytes(privateKey, ECCurve.Secp256r1);
            }

            var bytes = pubKey.EncodePoint(true).ToArray();
            this.PublicKey = bytes.Skip(1).ToArray();

            this.PublicKeyHash = CryptoUtils.ToScriptHash(bytes);

            this.signatureScript = CreateSignatureScript(bytes);
            signatureHash = CryptoUtils.ToScriptHash(signatureScript);

            this.UncompressedPublicKey = pubKey.EncodePoint(false).Skip(1).ToArray();

            this.address = CryptoUtils.ToAddress(signatureHash);
            this.WIF = GetWIF();
        }

        public static NeoKeys FromWIF(string wif)
        {
            if (wif == null) throw new ArgumentNullException();
            byte[] data = wif.Base58CheckDecode();
            if (data.Length != 34 || data[0] != 0x80 || data[33] != 0x01)
                throw new FormatException();
            byte[] privateKey = new byte[32];
            Buffer.BlockCopy(data, 1, privateKey, 0, privateKey.Length);
            Array.Clear(data, 0, data.Length);
            return new NeoKeys(privateKey);
        }

        private static System.Security.Cryptography.RandomNumberGenerator rnd = System.Security.Cryptography.RandomNumberGenerator.Create();

        public static NeoKeys Generate()
        {
            var bytes = new byte[32];
            lock (rnd)
            {
                rnd.GetBytes(bytes);
            }
            return new NeoKeys(bytes);
        }

        public static byte[] CreateSignatureScript(byte[] bytes)
        {
            var script = new byte[bytes.Length + 2];

            script[0] = (byte) OpCode.PUSHBYTES33;
            Array.Copy(bytes, 0, script, 1, bytes.Length);
            script[script.Length - 1] = (byte) OpCode.CHECKSIG;

            return  script;
        }
      
        private string GetWIF()
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

        public override string ToString()
        {
            return this.address;
        }

        public Signature Sign(byte[] msg)
        {
            return ECDsaSignature.Generate(this, msg, ECDsaCurve.Secp256r1);
        }
    }
}
