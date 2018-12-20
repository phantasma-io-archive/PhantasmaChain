using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class BalanceSheet
    {
        private Dictionary<Address, BigInteger> _balances = new Dictionary<Address, BigInteger>();

        public BigInteger Get(Address address)
        {
            lock (_balances)
            {
                if (_balances.ContainsKey(address))
                {
                    return _balances[address];
                }
            }

            return 0;
        }

        public bool Add(Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var balance = Get(address);
            balance += amount;

            lock (_balances)
            {
                _balances[address] = balance;
            }

            return true;
        }

        public bool Subtract(Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var balance = Get(address);

            if (balance < amount)
            {
                return false;
            }

            balance -= amount;

            lock (_balances)
            {
                if (balance == 0)
                {
                    _balances.Remove(address);
                }
                else
                {
                    _balances[address] = balance;
                }
            }

            return true;
        }

        public void ForEach(Action<Address, BigInteger> visitor)
        {
            Throw.IfNull(visitor, nameof(visitor));

            lock (_balances)
            {
                foreach (var entry in _balances)
                {
                    visitor(entry.Key, entry.Value);
                }
            }
        }
    }
}
