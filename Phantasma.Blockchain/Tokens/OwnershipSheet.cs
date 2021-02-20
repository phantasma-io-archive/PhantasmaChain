using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Text;
using System.Linq;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Tokens
{
    public struct OwnershipSheet
    {
        private byte[] _prefixItems;
        private byte[] _prefixOwner;

        public OwnershipSheet(string symbol)
        {
            this._prefixItems = MakePrefix(symbol);
            this._prefixOwner = Encoding.ASCII.GetBytes($".ownership.{symbol}");
        }

        public static byte[] MakePrefix(string symbol)
        {
            return Encoding.ASCII.GetBytes($".ids.{symbol}");
        }

        private byte[] GetKeyForMap(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefixItems, address.ToByteArray());
        }

        private byte[] GetKeyForOwner(BigInteger tokenID)
        {
            return ByteArrayUtils.ConcatBytes(_prefixOwner, tokenID.ToSignedByteArray());
        }

        public BigInteger[] Get(StorageContext storage, Address address)
        {
            lock (storage)
            {
                var mapKey = GetKeyForMap(address);
                var map = new StorageMap(mapKey, storage);
                return map.AllValues<BigInteger>();
            }
        }

        public Address GetOwner(StorageContext storage, BigInteger tokenID)
        {
            lock (storage)
            {
                var ownerKey = GetKeyForOwner(tokenID);

                if (storage.Has(ownerKey))
                {
                    return storage.Get<Address>(ownerKey);
                }

                return Address.Null;
            }
        }

        public bool Add(StorageContext storage, Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (!GetOwner(storage, tokenID).IsNull)
            {
                return false;
            }

            var mapKey = GetKeyForMap(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (storage)
            {
                var map = new StorageMap(mapKey, storage);
                map.Set<BigInteger, BigInteger>(tokenID, tokenID);

                storage.Put(ownerKey, address);
            }
            return true;
        }

        public bool Remove(StorageContext storage, Address address, BigInteger tokenID)
        {
            if (tokenID <= 0)
            {
                return false;
            }

            if (GetOwner(storage, tokenID) != address)
            {
                return false;
            }

            var mapKey = GetKeyForMap(address);
            var ownerKey = GetKeyForOwner(tokenID);

            lock (storage)
            {
                var map = new StorageMap(mapKey, storage);
                map.Remove<BigInteger>(tokenID);

                storage.Delete(ownerKey);
            }

            return true;
        }
    }
}
