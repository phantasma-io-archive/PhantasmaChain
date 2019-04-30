using System;
using System.Collections.Generic;
using System.Text;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Pay.Chains
{
    public class PhantasmaWallet : CryptoWallet
    {
        public PhantasmaWallet(KeyPair keys, Action<string, Action<string>> urlFetcher) : base(keys, urlFetcher)
        {
        }

        public override WalletKind Kind => WalletKind.Phantasma;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = "http://localhost:7078/api/getAccount/" + this.Address; // TODO change this later
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                root = root.GetNode("balances");
                foreach (var child in root.Children)
                {
                    var symbol = child.GetString("symbol");
                    var decimals = child.GetInt32("decimals");

                    var temp = child.GetString("amount");
                    var n = BigInteger.Parse(temp);
                    var amount = UnitConversion.ToDecimal(n, decimals);

                    _balances.Add(new WalletBalance(symbol, amount));
                }

                callback(true);
            });
            callback(false);
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            return keys.Address.Text; 
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, WalletKind.Phantasma);
            yield return new CryptoCurrencyInfo("KCAL", "Phantasma Energy", 10, WalletKind.Phantasma);
            yield break;
        }
    }
}
