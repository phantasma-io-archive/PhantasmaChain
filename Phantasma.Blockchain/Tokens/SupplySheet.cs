using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class SupplySheet
    {
        public BigInteger ParentBalance { get; private set; }
        public BigInteger LocalBalance { get; private set; }

        private BigInteger _maxBalance;

        private Dictionary<Address, BigInteger> _childBalances = new Dictionary<Address, BigInteger>();

        public SupplySheet(BigInteger parentBalance, BigInteger localBalance, BigInteger maxBalance)
        {
            this.ParentBalance = parentBalance;
            this.LocalBalance = localBalance;
            this._maxBalance = maxBalance;
        }

        public BigInteger GetChildBalance(Chain chain)
        {
            Throw.IfNull(chain, nameof(chain));

            if (_childBalances.ContainsKey(chain.Address))
            {
                return _childBalances[chain.Address];
            }

            return 0;
        }

        public bool MoveToParent(BigInteger amount)
        {
            if (LocalBalance < amount)
            {
                return false;
            }

            LocalBalance -= amount;
            ParentBalance += amount;
            return true;
        }

        public bool MoveFromParent(BigInteger amount)
        {
            if (ParentBalance < amount)
            {
                return false;
            }

            LocalBalance += amount;
            ParentBalance -= amount;
            return true;
        }

        public bool MoveToChild(Chain child, BigInteger amount)
        {
            Throw.IfNull(child, nameof(child));

            if (LocalBalance < amount)
            {
                return false;
            }

            LocalBalance -= amount;

            var childBalance = GetChildBalance(child);
            childBalance += amount;
            _childBalances[child.Address] = childBalance;

            return true;
        }

        public bool MoveFromChild(Chain child, BigInteger amount)
        {
            Throw.IfNull(child, nameof(child));

            var childBalance = GetChildBalance(child);

            if (childBalance < amount)
            {
                return false;
            }

            LocalBalance += amount;

            childBalance -= amount;
            _childBalances[child.Address] = childBalance;

            return true;
        }

        public bool Burn(BigInteger amount)
        {
            if (LocalBalance < amount)
            {
                return false;
            }

            LocalBalance -= amount;
            return true;
        }

        public bool Mint(BigInteger amount)
        {
            BigInteger existingSupply = ParentBalance + LocalBalance;

            foreach (var childBalance in _childBalances.Values)
            {
                existingSupply += childBalance;
            }

            var expectedSupply = existingSupply + amount;

            if (expectedSupply > _maxBalance)
            {
                return false;
            }

            LocalBalance += amount;
            return true;
        }

    }
}
