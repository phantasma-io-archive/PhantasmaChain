using Phantasma.Blockchain.Tokens;
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

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = $"https://api.blockcypher.com/v1/eth/main/addrs/{this.Address}/balance";
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                var temp = root.GetString("balance");
                var n = BigInteger.Parse(temp);
                var amount = TokenUtils.ToDecimal(n, 18);
                _balances.Add(new WalletBalance("ETH", amount));
                callback(true);
            });
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
