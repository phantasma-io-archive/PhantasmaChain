using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Cryptography;

namespace Phantasma.Pay
{
    public struct CryptoCurrencyInfo
    {
        public readonly string Symbol;
        public readonly string Name;
        public readonly int Decimals;
        public readonly WalletKind Kind;

        public CryptoCurrencyInfo(string symbol, string name, int decimals, WalletKind kind)
        {
            Symbol = symbol;
            Name = name;
            Decimals = decimals;
            Kind = kind;
        }
    }

    public abstract class CryptoWallet
    {
        public abstract WalletKind Kind { get; }
        public readonly string Address;

        protected List<WalletBalance> _balances = new List<WalletBalance>();
        public IEnumerable<WalletBalance> Balances => _balances;

        private Action<string, Action<string>> _urlFetcher;

        public CryptoWallet(KeyPair keys, Action<string, Action<string>> urlFetcher)
        {
            this.Address = DeriveAddress(keys);
            this._urlFetcher = urlFetcher;
        }

        protected void FetchURL(string url, Action<string> callback)
        {
            _urlFetcher(url, callback);
        }

        protected abstract string DeriveAddress(KeyPair keys);

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
