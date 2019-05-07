using Phantasma.Blockchain.Storage;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Text;

namespace Phantasma.Blockchain.Tokens
{
    public class SupplySheet
    {
        private BigInteger _maxBalance;

        private byte[] _prefix;

        //private Dictionary<Address, BigInteger> _childBalances = new Dictionary<Address, BigInteger>();

        private string _localName;
        private string _parentName;

        public SupplySheet(string symbol, string parentName, string localName, BigInteger maxBalance)
        {
            this._parentName = parentName;
            this._localName = localName;
            this._maxBalance = maxBalance;
            this._prefix = Encoding.ASCII.GetBytes(symbol);
        }

        private byte[] GetKeyForChain(string name)
        {
            return ByteArrayUtils.ConcatBytes(_prefix, Encoding.UTF8.GetBytes(name));
        }

        private BigInteger Get(StorageContext storage, string name)
        {
            lock (storage)
            {
                var key = GetKeyForChain(name);
                var temp = storage.Get(key); // TODO make utils method GetBigInteger
                if (temp == null || temp.Length == 0)
                {
                    return 0;
                }
                return new BigInteger(temp);
            }
        }

        private void Set(StorageContext storage, string name, BigInteger value)
        {
            lock (storage)
            {
                var key = GetKeyForChain(name);
                storage.Put(key, value);
            }
        }

        public BigInteger GetChildBalance(StorageContext storage, string childChainName)
        {
            Throw.IfNull(childChainName, nameof(childChainName));

            return Get(storage, childChainName);
        }

        public bool MoveToParent(StorageContext storage, BigInteger amount)
        {
            var localBalance = Get(storage, _localName);
            if (localBalance < amount)
            {
                return false;
            }

            localBalance -= amount;
            Set(storage, _localName, localBalance);

            var parentBalance = Get(storage, _parentName);
            parentBalance += amount;
            Set(storage, _parentName, parentBalance);

            return true;
        }

        public bool MoveFromParent(StorageContext storage, BigInteger amount)
        {
            var parentBalance = Get(storage, _parentName);
            if (parentBalance < amount)
            {
                return false;
            }

            var localBalance = Get(storage, _localName);
            localBalance += amount;
            Set(storage, _localName, localBalance);

            parentBalance -= amount;
            Set(storage, _parentName, parentBalance);

            return true;
        }

        public bool MoveToChild(StorageContext storage, string childChainName, BigInteger amount)
        {
            Throw.IfNull(childChainName, nameof(childChainName));

            var localBalance = Get(storage, _localName);
            if (localBalance < amount)
            {
                return false;
            }

            localBalance -= amount;
            Set(storage, _localName, localBalance);

            var childBalance = GetChildBalance(storage, childChainName);
            childBalance += amount;
            Set(storage, childChainName, childBalance);

            return true;
        }

        public bool MoveFromChild(StorageContext storage, string childChainName, BigInteger amount)
        {
            Throw.IfNull(childChainName, nameof(childChainName));

            var childBalance = GetChildBalance(storage, childChainName);

            if (childBalance < amount)
            {
                return false;
            }

            var localBalance = Get(storage, _localName);
            localBalance += amount;
            Set(storage, _localName, localBalance);

            childBalance -= amount;
            Set(storage, childChainName, childBalance);

            return true;
        }

        // TODO only can be done in rootchain
        public bool Burn(StorageContext storage, BigInteger amount)
        {
            var localBalance = Get(storage, _localName);
            if (localBalance < amount)
            {
                return false;
            }

            localBalance -= amount;
            Set(storage, _localName, localBalance);

            return true;
        }

        public bool Mint(StorageContext storage, BigInteger amount)
        {
            throw new System.NotImplementedException();
            /*
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
            return true;*/
        }

    }
}
