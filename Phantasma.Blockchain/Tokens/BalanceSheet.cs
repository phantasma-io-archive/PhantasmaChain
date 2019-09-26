using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System.Text;

namespace Phantasma.Blockchain.Tokens
{
    public struct BalanceSheet
    {
        private byte[] _prefix;

        public BalanceSheet(string symbol)
        {
            var key = $".balances.{symbol}";
            this._prefix = Encoding.ASCII.GetBytes(key);
        }

        private byte[] GetKeyForAddress(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefix, address.PublicKey);
        }

        public BigInteger Get(StorageContext storage, Address address)
        {
            lock (storage)
            {
                var key = GetKeyForAddress(address);
                var temp = storage.Get(key); // TODO make utils method GetBigInteger
                if (temp == null || temp.Length == 0)
                {
                    return 0;
                }
                return BigInteger.FromSignedArray(temp);
            }
        }

        public bool Add(StorageContext storage, Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var balance = Get(storage, address);
            balance += amount;

            var key = GetKeyForAddress(address);

            lock (storage)
            {
                storage.Put(key, balance);
            }

            return true;
        }

        public bool Subtract(StorageContext storage, Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var balance = Get(storage, address);

            if (balance < amount)
            {
                return false;
            }

            balance -= amount;

            var key = GetKeyForAddress(address);

            lock (storage)
            {
                if (balance == 0)
                {
                    storage.Delete(key);
                }
                else
                {
                    storage.Put(key, balance);
                }
            }

            return true;
        }
    }
}
