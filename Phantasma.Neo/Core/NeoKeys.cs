using System;
using System.Linq;
using System.Text;

using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Utils;

namespace Phantasma.Neo.Core
{
    public class NeoKeys : IKeyPair
    {
        public byte[] PrivateKey { get; private set; }
        public byte[] PublicKey { get; private set; }

        public readonly byte[] UncompressedPublicKey;
        public readonly UInt160 PublicKeyHash;
        public readonly string Address;
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

            this.PublicKey = pubKey.EncodePoint(true).ToArray();
            this.PublicKeyHash = CryptoUtils.ToScriptHash(PublicKey);

            this.signatureScript = CreateSignatureScript(PublicKey);

            this.UncompressedPublicKey = pubKey.EncodePoint(false).Skip(1).ToArray();

            this.signatureHash = CryptoUtils.ToScriptHash(signatureScript);
            this.Address = CryptoUtils.ToAddress(signatureHash);
            this.WIF = GetWIF();
        }

        public static string PublicKeyToAddress(byte[] publicKey)
        {
            var signatureScript = CreateSignatureScript(publicKey);
            var signatureHash = CryptoUtils.ToScriptHash(signatureScript);
            return CryptoUtils.ToAddress(signatureHash);
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

        public static NeoKeys FromNEP2(string nep2, string passphrase, int N = 16384, int r = 8, int p = 8)
        {
            Throw.IfNull(nep2, nameof(nep2));
            Throw.IfNull(passphrase, nameof(passphrase));

            byte[] data = nep2.Base58CheckDecode();
            if (data.Length != 39 || data[0] != 0x01 || data[1] != 0x42 || data[2] != 0xe0)
                throw new FormatException();

            byte[] addressHash = new byte[4];
            Buffer.BlockCopy(data, 3, addressHash, 0, 4);
            byte[] datapassphrase = Encoding.UTF8.GetBytes(passphrase);
            byte[] derivedkey = SCrypt.DeriveKey(datapassphrase, addressHash, N, r, p, 64);
            Array.Clear(datapassphrase, 0, datapassphrase.Length);

            byte[] derivedhalf1 = derivedkey.Take(32).ToArray();
            byte[] derivedhalf2 = derivedkey.Skip(32).ToArray();
            Array.Clear(derivedkey, 0, derivedkey.Length);

            byte[] encryptedkey = new byte[32];
            Buffer.BlockCopy(data, 7, encryptedkey, 0, 32);
            Array.Clear(data, 0, data.Length);

            byte[] prikey = XOR(encryptedkey.AES256Decrypt(derivedhalf2), derivedhalf1);
            Array.Clear(derivedhalf1, 0, derivedhalf1.Length);
            Array.Clear(derivedhalf2, 0, derivedhalf2.Length);

            ECPoint pubkey = ECCurve.Secp256r1.G * prikey;
            var keys = new NeoKeys(prikey);
            var temp = Encoding.ASCII.GetBytes(keys.Address).Sha256().Sha256().Take(4).ToArray();
            if (!temp.SequenceEqual(addressHash))
            {
                throw new FormatException("invalid passphrase when decrypting NEP2");
            }
            return keys;
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

            script[0] = (byte)OpCode.PUSHBYTES33;
            Array.Copy(bytes, 0, script, 1, bytes.Length);
            script[script.Length - 1] = (byte)OpCode.CHECKSIG;

            return script;
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
            return this.Address;
        }

        public Signature Sign(byte[] msg)
        {
            return ECDsaSignature.Generate(this, msg, ECDsaCurve.Secp256r1);
        }
    }
}
