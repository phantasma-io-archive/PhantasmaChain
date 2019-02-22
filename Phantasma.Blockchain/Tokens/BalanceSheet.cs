using Phantasma.Blockchain.Storage;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Text;

namespace Phantasma.Blockchain.Tokens
{
    public class BalanceSheet
    {
        private byte[] _prefix;
        private StorageContext _storage;

        public BalanceSheet(string symbol, StorageContext storage)
        {
            symbol = symbol + ".";
            this._prefix = Encoding.ASCII.GetBytes(symbol);
            this._storage = storage;
        }

        private byte[] GetKeyForAddress(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefix, address.PublicKey);
        }

        public BigInteger Get(Address address)
        {
            lock (_storage)
            {
                var key = GetKeyForAddress(address);
                var temp = _storage.Get(key); // TODO make utils method GetBigInteger
                if (temp == null || temp.Length == 0)
                {
                    return 0;
                }
                return new BigInteger(temp);
            }
        }

        public bool Add(Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            var balance = Get(address);
            balance += amount;

            var key = GetKeyForAddress(address);

            lock (_storage)
            {
                _storage.Put(key, balance);
            }

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

            var key = GetKeyForAddress(address);

            lock (_storage)
            {
                if (balance == 0)
                {
                    _storage.Delete(key);
                }
                else
                {
                    _storage.Put(key, balance);
                }
            }

            return true;
        }
    }
}
