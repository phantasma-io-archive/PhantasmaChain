using System;
using System.Collections.Generic;
using System.Text;
using Phantasma.Cryptography;

namespace Phantasma.Pay.Chains
{
    public class PhantasmaWallet : CryptoWallet
    {
        public PhantasmaWallet(KeyPair keys) : base(keys)
        {
        }

        public override WalletKind Kind => WalletKind.Phantasma;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            callback(false);
        }

        protected override string DeriveAddress(KeyPair keys)
        {
            return keys.Address.Text; 
        }
    }
}
