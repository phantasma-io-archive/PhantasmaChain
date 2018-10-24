using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Storage;

namespace Phantasma.Blockchain.Tokens
{
    public class Token
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public BigInteger MaxSupply { get; private set; }
        public bool IsCapped => MaxSupply > 0;

        public Address Owner { get; private set; }

        private StorageContext _storage;

        private BigInteger _supply = 0;
        public BigInteger CurrentSupply => _supply;

        public Token(StorageContext storage)
        {
            this._storage = storage;
        }

        public BigInteger Decimals => 8;

        internal Token(Address owner, string symbol, string name, BigInteger maxSupply)
        {
            this.Owner = owner;
            this.Symbol = symbol;
            this.Name = name;
            this.MaxSupply = maxSupply;

            _supply = 0;
        }

        internal bool Mint(BalanceSheet balances, Address target, BigInteger amount)
        {
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

        internal bool Burn(BalanceSheet balances, Address target, BigInteger amount)
        {
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

        internal bool Transfer(BalanceSheet balances, Address source, Address destination, BigInteger amount)
        {
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
    }
}