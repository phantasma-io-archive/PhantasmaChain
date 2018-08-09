using System;

namespace Phantasma.Contracts
{
    public static class ContractExtensions
    {
        public static void Expect(this IContract contract, bool assertion)
        {
            throw new NotImplementedException();
        }

        public static Address GetSource(this ITransaction tx)
        {
            return new Address(tx.PublicKey);
        }

        public static Address GetAddress(this IContract contract)
        {
            return new Address(contract.PublicKey);
        }

        public static Timestamp ToTimestamp(this DateTime value)
        {
            long val = (value.Ticks - 621355968000000000) / 10000000;
            return new Timestamp(val);
        }

        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);

        // Unix timestamp is seconds past epoch
        public static DateTime ToDateTime(this long unixTimeStamp)
        {
            return epoch.AddSeconds(unixTimeStamp).ToUniversalTime();
        }
    }
}
