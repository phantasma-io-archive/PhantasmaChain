using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Tokens
{
    public static class TokenUtils
    {
        private static decimal GetMultiplier(int units)
        {
            decimal unitMultiplier = 1m;
            while (units > 0)
            {
                unitMultiplier *= 10;
                units--;
            }

            return unitMultiplier;
        }

        public static decimal ToDecimal(BigInteger value, int units)
        {
            if (value == null || value == 0)
            {
                return 0;
            }

            var n = (long)value;

            var multiplier = GetMultiplier(units);
            return n / multiplier;
        }

        public static decimal ToDecimal(string value, int units)
        {
            if (string.IsNullOrEmpty(value)) return 0;

            BigInteger big = BigInteger.Parse(value);

            return ToDecimal(big, units);
        }

        public static BigInteger ToBigInteger(decimal n, int units)
        {
            var multiplier = GetMultiplier(units);            
            var A = new BigInteger((long)n);
            var B = new BigInteger((long)multiplier);

            var fracPart = n - Math.Truncate(n);
            BigInteger C = 0;

            if (fracPart > 0)
            {
                var l = fracPart * multiplier;
                C = new BigInteger((long)l);
            }

            return A * B + C;
        }
    }
}
