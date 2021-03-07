using System;
using System.Linq;
using Neo;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using NeoSmartContract = Neo.SmartContract;
using NeoSmartContractHelper = Neo.SmartContract.Helper;
using NeoWallets = Neo.Wallets;
using NeoWalletsHelper = Neo.Wallets.Helper;

namespace Phantasma.Neo.Core
{
    public class NeoKeys : IKeyPair
    {
        public byte[] PrivateKey { get; private set; }
        public byte[] PublicKey { get; private set; }

        public readonly byte[] UncompressedPublicKey;
        public readonly string Address;
        public readonly string WIF;
        public readonly UInt160 SignatureHash;

        public readonly byte[] signatureScript;

        public NeoKeys(byte[] privateKey)
        {
            var keyPair = new NeoWallets.KeyPair(privateKey);

            this.PrivateKey = keyPair.PrivateKey;
            this.PublicKey = keyPair.PublicKey.EncodePoint(true).ToArray();

            this.signatureScript = NeoSmartContract.Contract.CreateSignatureRedeemScript(keyPair.PublicKey);

            this.UncompressedPublicKey = keyPair.PublicKey.EncodePoint(false).Skip(1).ToArray();

            this.SignatureHash = NeoSmartContractHelper.ToScriptHash(signatureScript);
            this.Address = NeoWalletsHelper.ToAddress(this.SignatureHash);
            this.WIF = GetWIF();
        }

        public static NeoKeys FromWIF(string wif)
        {
            return new NeoKeys(NeoWallets.Wallet.GetPrivateKeyFromWIF(wif));
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

        private string GetWIF()
        {
            var keyPair = new NeoWallets.KeyPair(PrivateKey);
            return keyPair.Export();
        }

        public override string ToString()
        {
            return this.Address;
        }

        public Signature Sign(byte[] msg, Func<byte[], byte[], byte[], byte[]> customSignFunction = null)
        {
            return ECDsaSignature.Generate(this, msg, ECDsaCurve.Secp256r1);
        }
    }
}
