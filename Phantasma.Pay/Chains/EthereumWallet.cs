using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Linq;

namespace Phantasma.Pay.Chains
{
    public class EthereumWallet: CryptoWallet
    {
        public EthereumWallet(KeyPair keys) : base(keys)
        {
        }

        public override WalletKind Kind => WalletKind.Ethereum;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances()
        {
            throw new NotImplementedException();
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * keys.PrivateKey;

            var publicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            var kak = SHA3Keccak.CalculateHash(publicKey);
            return "0x" + Base16.Encode(kak.Skip(12).ToArray());
        }
    }
}
