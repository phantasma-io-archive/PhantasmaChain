using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Storage;
using System;
using Phantasma.Core;

namespace Phantasma.Blockchain.Tokens
{
    [Flags]
    public enum TokenFlags
    {
        None = 0,
        Transferable = 1 << 0,
        Fungible = 1 << 1,
        Finite = 1 << 2,
        Divisible = 1 << 3,
    }

    public class Token
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public TokenFlags Flags { get; private set; }

        public Chain Chain { get; private set; }

        public LargeInteger MaxSupply { get; private set; }

        public bool IsFungible => Flags.HasFlag(TokenFlags.Fungible);
        public bool IsCapped => MaxSupply > 0; // equivalent to Flags.HasFlag(TokenFlags.Infinite)

        public Address Owner { get; private set; }

        private LargeInteger _lastId = new LargeInteger(0);
        internal LargeInteger LastID { get ; private set; }

        private StorageContext _storage;

        private LargeInteger _supply = 0;
        public LargeInteger CurrentSupply => _supply;

        public int Decimals { get; private set; }

        public Token(StorageContext storage)
        {
            this._storage = storage;
        }

        internal Token(Chain chain, Address owner, string symbol, string name, LargeInteger maxSupply, int decimals, TokenFlags flags)
        {
            Throw.If(maxSupply < 0, "negative supply");
            Throw.If(maxSupply == 0 && flags.HasFlag(TokenFlags.Finite), "finite requires a supply");
            Throw.If(maxSupply > 0 && !flags.HasFlag(TokenFlags.Finite), "infinite requires no supply");


            if (flags.HasFlag(TokenFlags.Fungible))
            {
                Throw.If(!chain.IsRoot, "root chain required");
            }
            else
            {
                Throw.If(flags.HasFlag(TokenFlags.Divisible), "non-fungible must be indivisible");
                Throw.If(decimals != 0, "non-fungible must be indivisible");
            }

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Throw.If(decimals <= 0, "divisible must have decimals");
            }

            this.Owner = owner;
            this.Symbol = symbol;
            this.Name = name;
            this.MaxSupply = maxSupply;
            this.Decimals = decimals;
            this.Flags = flags;

            _supply = 0;
        }

        public override string ToString()
        {
            return $"{Name} ({Symbol})";
        }

        internal bool Mint(BalanceSheet balances, Address target, LargeInteger amount)
        {
            if (!Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            if (IsCapped && this.CurrentSupply + amount > this.MaxSupply)
            {
                return false;
            }

            if (!balances.Add(target, amount))
            {
                return false;
            }

            this._supply += amount;
            return true;
        }

        internal bool Burn(BalanceSheet balances, Address target, LargeInteger amount)
        {
            if (!Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            if (this.CurrentSupply - amount < 0)
            {
                return false;
            }

            if (!balances.Subtract(target, amount))
            {
                return false;
            }

            this._supply -= amount;
            return true;
        }

        internal bool Transfer(BalanceSheet balances, Address source, Address destination, LargeInteger amount)
        {
            if (!Flags.HasFlag(TokenFlags.Transferable))
            {
                return false;
            }

            if (!Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            if (!balances.Subtract(source, amount))
            {
                return false;
            }

            if (!balances.Add(destination, amount))
            {
                return false;
            }

            return true;
        }

        internal LargeInteger GenerateID()
        {
            _lastId++;
            return _lastId;
        }
   }
}