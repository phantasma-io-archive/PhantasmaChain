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
    }

    public class WalletManager
    {
        private Dictionary<WalletKind, CryptoWallet> _wallets = new Dictionary<WalletKind, CryptoWallet>();
        private KeyPair keys;

        public IEnumerable<CryptoWallet> Wallets => _wallets.Values;

        public WalletManager(KeyPair keys)
        {
            this.keys = keys;
        }

        public string GetAddress(WalletKind kind)
        {
            if (_wallets.ContainsKey(kind))
            {
                return _wallets[kind].Address;
            }

            CryptoWallet wallet;
            switch (kind)
            {
                case WalletKind.Phantasma: wallet = new PhantasmaWallet(keys); break;
                case WalletKind.Neo: wallet = new NeoWallet(keys); break;
                case WalletKind.Bitcoin: wallet = new BitcoinWallet(keys); break;
                case WalletKind.Ethereum: wallet = new EthereumWallet(keys); break;
                case WalletKind.EOS: wallet = new EOSWallet(keys); break;
                default: throw new Exception("Unsupported wallet kind: " + kind);
            }

            _wallets[kind] = wallet;
            return wallet.Address;
        }

        public void SyncBalances()
        {
            foreach (var wallet in _wallets.Values)
            {
                wallet.SyncBalances();
            }
        }
    }
}
