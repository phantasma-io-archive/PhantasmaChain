using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Cryptography;

namespace Phantasma.Pay
{
    [Flags]
    public enum CryptoCurrencyCaps
    {
        None = 0,
        Balance = 0x1,
        Transfer = 0x2,
        Stake = 0x4,
    }

    public struct CryptoCurrencyInfo
    {
        public readonly string Symbol;
        public readonly string Name;
        public readonly int Decimals;
        public readonly string Platform;
        public readonly CryptoCurrencyCaps Caps;

        public CryptoCurrencyInfo(string symbol, string name, int decimals, string platform, CryptoCurrencyCaps caps)
        {
            Symbol = symbol;
            Name = name;
            Decimals = decimals;
            Platform = platform;
            Caps = caps;
        }
    }

    public struct WalletBalance
    {
        public readonly string Symbol;
        public readonly decimal Amount;
        public readonly string Chain;

        public WalletBalance(string symbol, decimal amount, string chain = "main")
        {
            Symbol = symbol;
            Amount = amount;
            Chain = chain;
        }
    }

    public abstract class CryptoWallet
    {
        public abstract string Platform { get; }
        public readonly string Address;
        public string Name { get; protected set; }

        protected List<WalletBalance> _balances = new List<WalletBalance>();
        public IEnumerable<WalletBalance> Balances => _balances;

        public CryptoWallet(PhantasmaKeys keys)
        {
            this.Address = DeriveAddress(keys);
            this.Name = Address;
        }

        protected void FetchURL(string url, Action<string> callback)
        {
            try
            {
                string json;
                using (var wc = new System.Net.WebClient())
                {
                    json = wc.DownloadString(url);
                    callback(json);
                }
            }
            catch (Exception e)
            {
                callback(null);
            }
        }

        protected abstract string DeriveAddress(PhantasmaKeys keys);

        public abstract void SyncBalances(Action<bool> callback);
        public abstract void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback);

        public abstract IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos();

        protected void JSONRequest(string url, Action<DataNode> callback)
        {
            FetchURL(url, (json) =>
            {
                if (string.IsNullOrEmpty(json))
                {
                    callback(null);
                }
                else
                {
                    try
                    {
                        var root = JSONReader.ReadFromString(json);
                        callback(root);
                    }
                    catch
                    {
                        callback(null);
                    }
                }
            });
        }
    }
}
