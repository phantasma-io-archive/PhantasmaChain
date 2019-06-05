using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using System.Text;

namespace Phantasma.Blockchain.Tokens
{
    public class SupplySheet
    {
        private byte[] _prefix;

        //private Dictionary<Address, BigInteger> _childBalances = new Dictionary<Address, BigInteger>();

        private string _localName;
        private string _parentName;

        public SupplySheet(string symbol, Chain chain, Nexus nexus)
        {
            var parentName = nexus.GetParentChainByName(chain.Name);
            this._parentName = parentName;
            this._localName = chain.Name;
            this._prefix = Encoding.ASCII.GetBytes(symbol);
        }

        private byte[] GetKeyForChain(string name)
        {
            return ByteArrayUtils.ConcatBytes(_prefix, Encoding.UTF8.GetBytes(name));
        }

        private StorageList GetChildList(StorageContext storage)
        {
            var key = ByteArrayUtils.ConcatBytes(_prefix, Encoding.UTF8.GetBytes(".children"));
            return new StorageList(key, storage);
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
            Throw.If(childChainName == _localName, "invalid child, received local instead");
            Throw.If(childChainName == _parentName, "invalid child, received parent instead");

            var localBalance = Get(storage, _localName);
            if (localBalance < amount)
            {
                return false;
            }

            localBalance -= amount;
            Set(storage, _localName, localBalance);

            var childList = GetChildList(storage);
            int childIndex = -1;
            var childCount = childList.Count();
            for (int i=0; i<childCount; i++)
            {
                var temp = childList.Get<string>(i);
                if (temp == childChainName)
                {
                    childIndex = i;
                    break;
                }
            }

            if (childIndex == -1)
            {
                childList.Add(childChainName);
            }

            var childBalance = GetChildBalance(storage, childChainName);
            childBalance += amount;
            Set(storage, childChainName, childBalance);

            return true;
        }

        public bool MoveFromChild(StorageContext storage, string childChainName, BigInteger amount)
        {
            Throw.IfNull(childChainName, nameof(childChainName));
            Throw.If(childChainName == _localName, "invalid child, received local instead");
            Throw.If(childChainName == _parentName, "invalid child, received parent instead");

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

        public BigInteger GetTotal(StorageContext storage)
        {
            var localBalance = Get(storage, _localName);

            BigInteger existingSupply = localBalance;

            var childList = GetChildList(storage);
            var childCount = childList.Count();
            for (int i = 0; i < childCount; i++)
            {
                var childName = childList.Get<string>(i);
                var childBalance = Get(storage, childName);
                existingSupply += childBalance;
            }

            return existingSupply;
        }

        // NOTE if not capped supply, then pass 0 or negative number to maxBalance
        public bool Mint(StorageContext storage, BigInteger amount, BigInteger maxBalance)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (maxBalance > 0)
            {
                var existingSupply = GetTotal(storage);
                var expectedSupply = existingSupply + amount;
                if (expectedSupply > maxBalance)
                {
                    return false;
                }
            }

            var localBalance = Get(storage, _localName);
            localBalance += amount;
            Set(storage, _localName, localBalance);

            return true;
        }

        public bool Burn(StorageContext storage, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var localBalance = Get(storage, _localName);
            if (localBalance < amount)
            {
                return false;
            }

            localBalance -= amount;
            Set(storage, _localName, localBalance);

            return true;
        }

        internal void Init(StorageContext localStorage, StorageContext parentStorage, SupplySheet parentSupply)
        {
            var parentBalance = parentSupply.Get(parentStorage, parentSupply._localName);
            Set(localStorage, _parentName, parentBalance);
        }

        internal void Synch(StorageContext storage, string chainName, BigInteger amountChanged)
        {
            var chainBalance = this.Get(storage, chainName);
            chainBalance += amountChanged;
            Throw.If(chainBalance < 0, "something went wrong, invalid balance found");
            Set(storage, chainName, chainBalance);
        }
    }
}
