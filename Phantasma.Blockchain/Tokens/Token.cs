using Phantasma.Numerics;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System;

namespace Phantasma.Blockchain.Tokens
{
    public class Token
    {
        public string Symbol { get; private set; }
        public string Name { get; private set; }

        public BigInteger MaxSupply { get; private set; } // = 93000000;

        public Address Owner { get; private set; }

        private StorageContext _storage;

        private BigInteger _supply = 0;
        public BigInteger CurrentSupply => _supply;

        private Dictionary<Address, BigInteger> _balances = new Dictionary<Address, BigInteger>();

        public Token(StorageContext storage)
        {
            this._storage = storage;
        }

        public string GetName() => Name;
        public string GetSymbol() => Symbol;
        public BigInteger GetSupply() => MaxSupply;
        public BigInteger GetDecimals() => 8;

        internal Token(Address owner, string symbol, string name, BigInteger maxSupply)
        {
            this.Owner = owner;
            this.Symbol = symbol;
            this.Name = name;
            this.MaxSupply = maxSupply;

            _supply = 0;
        }

        internal bool Mint(Address target, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (this.CurrentSupply + amount > this.MaxSupply)
            {
                return false;
            }

            var balance = _balances.ContainsKey(target) ? _balances[target] : 0;

            balance += amount;
            _balances[target] = balance;

            this._supply += amount;
            return true;
        }

        internal bool Burn(Address target, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (this.CurrentSupply - amount < 0)
            {
                return false;
            }

            var balance = _balances.ContainsKey(target) ? _balances[target] : 0;

            if (balance < amount)
            {
                return false;
            }

            balance -= amount;
            if (balance == 0)
            {
                _balances.Remove(target);
            }
            else
            {
                _balances[target] = balance;
            }

            this._supply -= amount;
            return true;
        }

        internal bool Transfer(Address source, Address destination, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (!_balances.ContainsKey(source))
            {
                return false;
            }

            var srcBalance = _balances[source];

            if (srcBalance < amount)
            {
                return false;
            }

            srcBalance -= amount;
            if (srcBalance == 0)
            {
                _balances.Remove(source);
            }
            else
            {
                _balances[source] = srcBalance;
            }

            var destBalance = _balances.ContainsKey(destination) ? _balances[destination] : 0;

            destBalance += amount;
            _balances[destination] = destBalance;

            return true;
        }

        internal BigInteger GetBalance(Address address)
        {
            return _balances.ContainsKey(address) ? _balances[address] : 0;
        }
    }
}