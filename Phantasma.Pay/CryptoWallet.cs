using System;
using System.Collections.Generic;
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

        public abstract void SyncBalances();
        public abstract void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback);
    }
}
