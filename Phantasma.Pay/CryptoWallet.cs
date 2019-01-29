using System;
using System.Collections.Generic;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Phantasma.Cryptography;

namespace Phantasma.Pay
{
    public abstract class CryptoWallet
    {
        public abstract WalletKind Kind { get; }
        public readonly string Address;

        protected List<WalletBalance> _balances = new List<WalletBalance>();
        public IEnumerable<WalletBalance> Balances => _balances;
        
        public CryptoWallet(KeyPair keys)
        {
            this.Address = DeriveAddress(keys);
        }

        protected abstract string DeriveAddress(KeyPair keys);

        public abstract void SyncBalances(Action<bool> callback);
        public abstract void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback);

        protected void JSONRequest(string url, Action<DataNode> callback)
        {
            string contents;
            try
            {
                using (var wc = new System.Net.WebClient())
                {
                    contents = wc.DownloadString(url);
                    var root = JSONReader.ReadFromString(contents);
                    callback(root);
                    return;
                }
            }
            catch (Exception e)
            {                
                callback(null);
            }
        }
    }
}
