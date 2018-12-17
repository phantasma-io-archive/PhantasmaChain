using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Tokens
{
    public class OwnershipSheet
    {
        private Dictionary<Address, HashSet<BigInteger>> _items = new Dictionary<Address, HashSet<BigInteger>>();
        private Dictionary<BigInteger, Address> _ownerMap = new Dictionary<BigInteger, Address>();

        public IEnumerable<BigInteger> Get(Address address)
        {
            lock (_items)
            {
                if (_items.ContainsKey(address))
                {
                    var items = _items[address];
                    if (items != null)
                    {
                        return items;
                    }
                }
            }

            return Enumerable.Empty<BigInteger>();
        }

        public Address GetOwner(BigInteger tokenID)
        {
            lock (_items)
            {
                if (_ownerMap.ContainsKey(tokenID))
                {
                    return _ownerMap[tokenID];
                }
            }

            return Address.Null;
        }

        internal bool Give(Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(tokenID) != Address.Null)
            {
                return false;
            }

            lock (_items)
            {
                HashSet<BigInteger> items;

                if (_items.ContainsKey(address))
                {
                    items = _items[address];
                }
                else
                {
                    items = new HashSet<BigInteger>();
                    _items[address] = items;
                }

                items.Add(tokenID);
                _ownerMap[tokenID] = address;
            }
            return true;
        }

        internal bool Take(Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(tokenID) != address)
            {
                return false;
            }

            lock (_items)
            {
                if (_items.ContainsKey(address))
                {
                    var items = _items[address];
                    items.Remove(tokenID);
                }

                _ownerMap.Remove(tokenID);
            }

            return true;
        }

        public void ForEach(Action<Address, IEnumerable<BigInteger>> visitor)
        {
            Throw.IfNull(visitor, nameof(visitor));

            lock (_items)
            {
                foreach (var entry in _items)
                {
                    visitor(entry.Key, entry.Value);
                }
            }
        }
    }
}
