using System;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Pay.Chains
{
    public class PhantasmaWallet : CryptoWallet
    {
        private string rpcURL;

        public PhantasmaWallet(PhantasmaKeys keys, string rpcURL) : base(keys)
        {
            if (!rpcURL.EndsWith("/"))
            {
                rpcURL += "/";
            }
            this.rpcURL = rpcURL;
        }

        public const string PhantasmaPlatform = "phantasma";

        public override string Platform => PhantasmaPlatform;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = $"{rpcURL}getAccount/{Address}"; 
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                this.Name = root.GetString("name");

                var balanceNode = root.GetNode("balances");
                if (balanceNode != null)
                {
                    foreach (var child in balanceNode.Children)
                    {
                        var symbol = child.GetString("symbol");
                        var decimals = child.GetInt32("decimals");
                        var chain = child.GetString("chain");

                        var temp = child.GetString("amount");
                        var n = BigInteger.Parse(temp);
                        var amount = UnitConversion.ToDecimal(n, decimals);

                        _balances.Add(new WalletBalance(symbol, amount, chain));
                    }
                }

                callback(true);
            });
            callback(false);
        }

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            return keys.Address.Text; 
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, PhantasmaWallet.PhantasmaPlatform, CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer | CryptoCurrencyCaps.Stake);
            yield return new CryptoCurrencyInfo("KCAL", "Phantasma Energy", 10, PhantasmaWallet.PhantasmaPlatform, CryptoCurrencyCaps.Balance | CryptoCurrencyCaps.Transfer);
            yield break;
        }
    }
}
