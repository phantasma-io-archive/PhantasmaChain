using System;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;

namespace Phantasma.Pay.Chains
{
    public class BitcoinWallet: CryptoWallet
    {
        public BitcoinWallet(KeyPair keys) : base(keys)
        {
        }

        public override WalletKind Kind => WalletKind.Bitcoin;

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

            var publicKey = pKey.EncodePoint(true);

            var bytes = ByteArrayUtils.ConcatBytes(new byte[] { 0 }, publicKey.Sha256().RIPEMD160());

            return bytes.Base58CheckEncode();
        }
    }
}
