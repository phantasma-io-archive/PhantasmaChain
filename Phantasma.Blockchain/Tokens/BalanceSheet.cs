using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class BalanceSheet
    {
        private Dictionary<Address, BigInteger> _balances = new Dictionary<Address, BigInteger>();

        public BigInteger Get(Address address)
        {
            if (_balances.ContainsKey(address))
            {
                return _balances[address];
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
            _balances[address] = balance;
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

            if (balance == 0)
            {
                _balances.Remove(address);
            }
            else
            {
                _balances[address] = balance;
            }

            return true;
        }
    }
}
