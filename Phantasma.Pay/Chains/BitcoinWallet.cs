using System;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;

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

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = "https://blockchain.info/rawaddr/" + this.Address;
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                var temp = BigInteger.Parse(root.GetString("final_balance"));
                var amount = UnitConversion.ToDecimal(temp, 8); // convert from satoshi to BTC
                _balances.Add(new WalletBalance("BTC", amount));
                callback(true);
            });            
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            ECPoint pKey = ECCurve.Secp256k1.G * keys.PrivateKey;

            var publicKey = pKey.EncodePoint(true);

            var bytes = ByteArrayUtils.ConcatBytes(new byte[] { 0 }, publicKey.SHA256().RIPEMD160());

            return bytes.Base58CheckEncode();
        }
    }
}
