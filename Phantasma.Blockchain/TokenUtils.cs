using Phantasma.Numerics;

namespace Phantasma.Blockchain
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
            return n / GetMultiplier(units);
        }

        public static BigInteger ToBigInteger(decimal n, int units)
        {
            var l = (long)(n * GetMultiplier(units));
            return new BigInteger(l);
        }
    }
}
