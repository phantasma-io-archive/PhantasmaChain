using System;
using System.Collections.Generic;
using Phantasma.Pay.Chains;
using Phantasma.Cryptography;

namespace Phantasma.Pay
{
    public enum WalletKind
    {
        Phantasma,
        Bitcoin,
        Ethereum,
        Neo,
        EOS,
    }

    public struct WalletBalance
    {
        public readonly string Symbol;
        public readonly decimal Amount;

        public WalletBalance(string symbol, decimal amount)
        {
            Symbol = symbol;
            Amount = amount;
        }
    }

    public abstract class WalletManager
    {
        private Dictionary<WalletKind, CryptoWallet> _wallets = new Dictionary<WalletKind, CryptoWallet>();
        private KeyPair keys;

        public IEnumerable<CryptoWallet> Wallets => _wallets.Values;

        public WalletManager(KeyPair keys, params WalletKind[] kinds)
        {
            this.keys = keys;

            if (kinds != null)
            {
                foreach (var kind in kinds)
                {
                    GetAddress(kind);
                }
            }
        }

        public abstract void FetchURL(string url, Action<string> callback);

        public string GetAddress(WalletKind kind)
        {
            if (_wallets.ContainsKey(kind))
            {
                return _wallets[kind].Address;
            }

            CryptoWallet wallet;
            switch (kind)
            {
                case WalletKind.Phantasma: wallet = new PhantasmaWallet(keys, FetchURL); break;
                case WalletKind.Neo: wallet = new NeoWallet(keys, FetchURL); break;
                case WalletKind.Bitcoin: wallet = new BitcoinWallet(keys, FetchURL); break;
                case WalletKind.Ethereum: wallet = new EthereumWallet(keys, FetchURL); break;
                case WalletKind.EOS: wallet = new EOSWallet(keys, FetchURL); break;
                default: throw new Exception("Unsupported wallet kind: " + kind);
            }

            _wallets[kind] = wallet;
            return wallet.Address;
        }

        public void SyncBalances(Action<WalletKind, bool> callback)
        {
            foreach (var wallet in _wallets.Values)
            {
                var kind = wallet.Kind;
                wallet.SyncBalances((result) => callback(kind, result));
            }
        }
    }
}
