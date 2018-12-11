using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class SupplySheet
    {
        public LargeInteger ParentBalance { get; private set; }
        public LargeInteger LocalBalance { get; private set; }

        private LargeInteger _maxBalance;

        private Dictionary<Address, LargeInteger> _childBalances = new Dictionary<Address, LargeInteger>();

        public SupplySheet(LargeInteger parentBalance, LargeInteger localBalance, LargeInteger maxBalance)
        {
            this.ParentBalance = parentBalance;
            this.LocalBalance = localBalance;
            this._maxBalance = maxBalance;
        }

        public LargeInteger GetChildBalance(Chain chain)
        {
            Throw.IfNull(chain, nameof(chain));

            if (_childBalances.ContainsKey(chain.Address))
            {
                return _childBalances[chain.Address];
            }

            return 0;
        }

        public bool MoveToParent(LargeInteger amount)
        {
            if (LocalBalance < amount)
            {
                return false;
            }

            LocalBalance -= amount;
            ParentBalance += amount;
            return true;
        }

        public bool MoveFromParent(LargeInteger amount)
        {
            if (ParentBalance < amount)
            {
                return false;
            }

            LocalBalance += amount;
            ParentBalance -= amount;
            return true;
        }

        public bool MoveToChild(Chain child, LargeInteger amount)
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

        public bool MoveFromChild(Chain child, LargeInteger amount)
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

        public bool Burn(LargeInteger amount)
        {
            if (LocalBalance < amount)
            {
                return false;
            }

            LocalBalance -= amount;
            return true;
        }

        public bool Mint(LargeInteger amount)
        {
            LargeInteger existingSupply = ParentBalance + LocalBalance;

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
