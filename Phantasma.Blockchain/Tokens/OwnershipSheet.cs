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
        private Dictionary<Address, HashSet<LargeInteger>> _items = new Dictionary<Address, HashSet<LargeInteger>>();
        private Dictionary<LargeInteger, Address> _ownerMap = new Dictionary<LargeInteger, Address>();

        public IEnumerable<LargeInteger> Get(Address address)
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

            return Enumerable.Empty<LargeInteger>();
        }

        public Address GetOwner(LargeInteger tokenID)
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

        internal bool Give(Address address, LargeInteger tokenID)
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
                HashSet<LargeInteger> items;

                if (_items.ContainsKey(address))
                {
                    items = _items[address];
                }
                else
                {
                    items = new HashSet<LargeInteger>();
                    _items[address] = items;
                }

                items.Add(tokenID);
                _ownerMap[tokenID] = address;
            }
            return true;
        }

        internal bool Take(Address address, LargeInteger tokenID)
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

        public void ForEach(Action<Address, IEnumerable<LargeInteger>> visitor)
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
