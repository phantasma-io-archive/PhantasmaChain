using Phantasma.Blockchain.Storage;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Tokens
{
    public class OwnershipSheet
    {
        private byte[] _prefixItems;
        private byte[] _prefixOwner;
        private StorageContext _storage;

     //   private Dictionary<Address, HashSet<BigInteger>> _items = new Dictionary<Address, HashSet<BigInteger>>();
     //   private Dictionary<BigInteger, Address> _ownerMap = new Dictionary<BigInteger, Address>();

        public OwnershipSheet(string symbol, StorageContext storage)
        {
            this._prefixItems = Encoding.ASCII.GetBytes(symbol + ".ids.");
            this._prefixOwner = Encoding.ASCII.GetBytes(symbol + ".own.");
            this._storage = storage;
        }

        private byte[] GetKeyForList(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefixItems, address.PublicKey);
        }

        private byte[] GetKeyForOwner(BigInteger tokenID)
        {
            return ByteArrayUtils.ConcatBytes(_prefixOwner, tokenID.ToByteArray());
        }

        public BigInteger[] Get(Address address)
        {
            lock (_storage)
            {
                var listKey = GetKeyForList(address);
                var list = new StorageList(listKey, _storage);
                return list.All<BigInteger>();
            }
        }

        public Address GetOwner(BigInteger tokenID)
        {
            lock (_storage)
            {
                var ownerKey = GetKeyForOwner(tokenID);

                var temp = _storage.Get(ownerKey);
                if (temp == null || temp.Length != Address.PublicKeyLength)
                {
                    return Address.Null;
                }

                return new Address(temp);
            }
        }

        public bool Give(Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(tokenID) != Address.Null)
            {
                return false;
            }

            var listKey = GetKeyForList(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (_storage)
            {
                var list = new StorageList(listKey, _storage);
                list.Add<BigInteger>(tokenID);

                _storage.Put(ownerKey, address);
            } 
            return true;
        }

        public bool Take(Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(tokenID) != address)
            {
                return false;
            }

            var listKey = GetKeyForList(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (_storage)
            {
                var list = new StorageList(listKey, _storage);
                list.Remove(tokenID);

                _storage.Delete(ownerKey);
            }

            return true;
        }
    }
}
