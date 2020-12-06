using System.Globalization;

namespace System.Numerics
{
    public static class BigIntegerExtension
    {
        public static BigInteger Mod(this BigInteger a, BigInteger b)
        {
            return a % b;
        }

        public static bool TestBit(this BigInteger a, int index)
        {
            return (a & (BigInteger.One << index)) > BigInteger.Zero;
        }

        public static int GetLowestSetBit(this BigInteger a)
        {
            if (a.Sign == 0)
                return -1;

            byte[] b = a.ToByteArray();

            int w = 0;
            while (b[w] == 0)
                w++;
            for (int x = 0; x < 8; x++)
                if ((b[w] & 1 << x) > 0)
                    return x + w * 8;

            throw new Exception();
        }

        private static int GetBitLen(int w)
        {
            return (w < 1 << 15 ? (w < 1 << 7
                ? (w < 1 << 3 ? (w < 1 << 1
                ? (w < 1 << 0 ? (w < 0 ? 32 : 0) : 1)
                : (w < 1 << 2 ? 2 : 3)) : (w < 1 << 5
                ? (w < 1 << 4 ? 4 : 5)
                : (w < 1 << 6 ? 6 : 7)))
                : (w < 1 << 11
                ? (w < 1 << 9 ? (w < 1 << 8 ? 8 : 9) : (w < 1 << 10 ? 10 : 11))
                : (w < 1 << 13 ? (w < 1 << 12 ? 12 : 13) : (w < 1 << 14 ? 14 : 15)))) : (w < 1 << 23 ? (w < 1 << 19
                ? (w < 1 << 17 ? (w < 1 << 16 ? 16 : 17) : (w < 1 << 18 ? 18 : 19))
                : (w < 1 << 21 ? (w < 1 << 20 ? 20 : 21) : (w < 1 << 22 ? 22 : 23))) : (w < 1 << 27
                ? (w < 1 << 25 ? (w < 1 << 24 ? 24 : 25) : (w < 1 << 26 ? 26 : 27))
                : (w < 1 << 29 ? (w < 1 << 28 ? 28 : 29) : (w < 1 << 30 ? 30 : 31)))));
        }

        public static int GetBitLength(this BigInteger a)
        {
            byte[] b = a.ToByteArray();
            return (b.Length - 1) * 8 + GetBitLen(a.Sign > 0 ? b[b.Length - 1] : 255 - b[b.Length - 1]);
        }

        public static BigInteger HexToBigInteger(this string a)
        {
            return BigInteger.Parse("0" + a, NumberStyles.AllowHexSpecifier);
        }

        public static BigInteger ModInverse(this BigInteger a, BigInteger n)
        {
            BigInteger i = n, v = 0, d = 1;
            while (a > 0)
            {
                BigInteger t = i / a, x = a;
                a = i % x;
                i = x;
                x = d;
                d = v - t * x;
                v = x;
            }
            v %= n;
            if (v < 0) v = (v + n) % n;
            return v;
        }

        public static bool IsParsable(this string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                return false;
            }

            foreach (var ch in val)
            {
                if (ch >='0'  && ch <= '9')
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        public static string ToDecimal(this BigInteger a)
        {
            //TODO 
            return "NYD!!";
        }
    }
}
