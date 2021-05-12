using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using System.Text;

namespace Phantasma.Blockchain.Tokens
{
    public class BalanceException : Exception
    {
        public static IToken token = null;
        public static Address address;
        public static BigInteger amount;

        public BalanceException(IToken token, Address address, BigInteger amount) : base($"Address {address} lacks {UnitConversion.ToDecimal(amount, token.Decimals)} {token.Symbol}")
        {
            if (BalanceException.token != null)
            {
                BalanceException.token = null; // should never enter here...
            }

            BalanceException.token = token;
            BalanceException.address = address;
            BalanceException.amount = amount;
        }
    }

    public struct BalanceSheet
    {
        private byte[] _prefix;
        private IToken _token;

        public BalanceSheet(IToken token)
        {
            this._token = token;
            this._prefix = MakePrefix(token.Symbol);
        }

        public static byte[] MakePrefix(string symbol)
        {
            var key = $".balances.{symbol}";
            return Encoding.UTF8.GetBytes(key);
        }

        private byte[] GetKeyForAddress(Address address)
        {
            return ByteArrayUtils.ConcatBytes(_prefix, address.ToByteArray());
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

            var diff = balance - amount;
            if (diff < 0)
            {
                throw new BalanceException(_token, address, -diff);
                //return false;
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
