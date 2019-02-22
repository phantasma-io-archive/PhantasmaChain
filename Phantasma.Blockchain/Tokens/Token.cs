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
        Fuel = 1 << 4,
        Stakable = 1 << 5,
        Stable = 1 << 6,
        External = 1 << 7,
    }

    public class Token
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public TokenFlags Flags { get; private set; }

        public Chain Chain { get; private set; }

        public BigInteger MaxSupply { get; private set; }

        public bool IsFungible => Flags.HasFlag(TokenFlags.Fungible);
        public bool IsCapped => MaxSupply > 0; // equivalent to Flags.HasFlag(TokenFlags.Infinite)

        public Address Owner { get; private set; }

        private BigInteger _lastId = new BigInteger(0);
        internal BigInteger LastID { get ; private set; }

        private StorageContext _storage;

        private BigInteger _supply = 0;
        public BigInteger CurrentSupply => _supply;

        public int Decimals { get; private set; }

        public Token(StorageContext storage)
        {
            this._storage = storage;
        }

        internal Token(Chain chain, Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags)
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
                Throw.If(flags.HasFlag(TokenFlags.Divisible), "non-fungible token must be indivisible");
            }

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Throw.If(decimals <= 0, "divisible token must have decimals");
            }
            else
            {
                Throw.If(decimals > 0, "indivisible token can't have decimals");
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

        internal bool Mint(StorageContext storage, BalanceSheet balances, Address target, BigInteger amount)
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

            if (!balances.Add(storage, target, amount))
            {
                return false;
            }

            this._supply += amount;
            return true;
        }

        // NFT version
        internal bool Mint()
        {
            if (Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            BigInteger amount = 1;

            if (IsCapped && this.CurrentSupply + amount > this.MaxSupply)
            {
                return false;
            }

            this._supply += amount;
            return true;
        }

        internal bool Burn(StorageContext storage, BalanceSheet balances, Address target, BigInteger amount)
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

            if (!balances.Subtract(storage, target, amount))
            {
                return false;
            }

            this._supply -= amount;
            return true;
        }

        // NFT version
        internal bool Burn()
        {
            if (Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            BigInteger amount = 1;

            if (this.CurrentSupply - amount < 0)
            {
                return false;
            }

            this._supply -= amount;
            return true;
        }

        internal bool Transfer(StorageContext storage, BalanceSheet balances, Address source, Address destination, BigInteger amount)
        {
            if (!Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (!Flags.HasFlag(TokenFlags.Fungible))
            {
                throw new Exception("Should be fungible");
            }

            if (amount <= 0)
            {
                return false;
            }

            if (!balances.Subtract(storage, source, amount))
            {
                return false;
            }

            if (!balances.Add(storage, destination, amount))
            {
                return false;
            }

            return true;
        }

        internal bool Transfer(StorageContext storage, OwnershipSheet ownerships, Address source, Address destination, BigInteger ID)
        {
            if (!Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (Flags.HasFlag(TokenFlags.Fungible))
            {
                throw new Exception("Should be non-fungible");
            }

            if (ID <= 0)
            {
                return false;
            }

            if (!ownerships.Take(storage, source, ID))
            {
                return false;
            }

            if (!ownerships.Give(storage, destination, ID))
            {
                return false;
            }

            return true;
        }

        internal BigInteger GenerateID()
        {
            _lastId++;
            return _lastId;
        }
   }
}