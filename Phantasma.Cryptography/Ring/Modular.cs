using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Core;

namespace Phantasma.Cryptography.Ring
{
    // Most of the code is ripped off from Bouncy Castle. Multi-exponentiation stuff is new
    public class Modular
    {
        private const long IMASK = 0xffffffffL;
        private const ulong UIMASK = (ulong)IMASK;

        BigInteger modulus;
        protected int[] modulusMagnitude;
        long mDash;

        public Modular(BigInteger modulus)
        {
            Throw.If(modulus <= 2, "Modulus must be greater than 2");

            this.modulus = modulus;
            modulusMagnitude = GetData(modulus);


            Throw.If(modulusMagnitude[modulusMagnitude.Length - 1] % 2 == 0, "Modulus must be odd");

            mDash = GetMDash(modulusMagnitude);
        }

        public BigInteger Modulus { get { return modulus; } }

        protected int[] GetData(BigInteger number)
        {
            var bytes = number.ToByteArray();
            int nBytes = bytes.Length;
            while (nBytes > 0 && bytes[nBytes - 1] == 0)
                --nBytes;

            int length = (nBytes - 1) / 4 + 1;
            var res = new int[length];
            Buffer.BlockCopy(bytes, 0, res, 0, Math.Min(length * 4, nBytes));
            Array.Reverse(res);
            return res;
        }

        protected BigInteger FromData(int[] data)
        {
            var bytes = new byte[data.Length * 4 + 1];
            Buffer.BlockCopy(data.Reverse().ToArray(), 0, bytes, 0, bytes.Length - 1);
            return new BigInteger(bytes);
        }

        private long GetMDash(int[] magnitude)
        {
            long v = (((~magnitude[magnitude.Length - 1]) | 1) & 0xffffffffL);
            var mDash = FastModInverse(v, 0x100000000L);

            return mDash;
        }

        private static long FastModInverse(long v, long m)
        {
            long[] x = new long[2];
            long gcd = FastExtEuclid(v, m, x);

            if (x[0] < 0)
            {
                x[0] += m;
            }

            return x[0];
        }

        private static long FastExtEuclid(long a, long b, long[] uOut)
        {
            long u1 = 1;
            long u3 = a;
            long v1 = 0;
            long v3 = b;

            while (v3 > 0)
            {
                long q, tn;

                q = u3 / v3;

                tn = u1 - (v1 * q);
                u1 = v1;
                v1 = tn;

                tn = u3 - (v3 * q);
                u3 = v3;
                v3 = tn;
            }

            uOut[0] = u1;
            uOut[1] = (u3 - (u1 * a)) / b;

            return u3;
        }

        public BigInteger Pow(BigInteger[] bases, BigInteger[] exponents)
        {
            int[,][] cache = null;
            return Pow(bases, exponents, ref cache, 4);
        }

        public BigInteger Pow(BigInteger[] bases, BigInteger[] exponents, ref int[,][] cache, int windowSize = 8)
        {
            if (bases.Length != exponents.Length)
                throw new ArithmeticException("Same number of bases and exponents expected");

            int[][] exps;
            int maxExpLen;
            ExtractAligned(exponents, out exps, out maxExpLen);

            // those are in Montgomery form
            var gs = new int[exponents.Length][];
            for (int i = 0; i < exponents.Length; ++i)
            {
                BigInteger g = (bases[i] << (32 * modulusMagnitude.Length)) % modulus;
                gs[i] = GetData(g);
                if (gs[i].Length < modulusMagnitude.Length)
                    gs[i] = Extend(gs[i], modulusMagnitude.Length);
            }

            var accum = new int[modulusMagnitude.Length + 1];
            var a = new int[modulusMagnitude.Length];
            var gi = new int[modulusMagnitude.Length];
            bool foundFirst = false;

            int nWindows = (exponents.Length - 1) / windowSize + 1;
            if (cache == null)
                cache = new int[nWindows, 1 << windowSize][];
            else if (cache.GetLength(0) != nWindows || cache.GetLength(1) != (1 << windowSize))
                throw new ArgumentException("Cache has wrong dimentions");

            for (int i = 0; i < maxExpLen; ++i)
            {
                for (int bit = 31; bit >= 0; --bit)
                {
                    bool nonZero = false;
                    var mask = 1 << bit;
                    for (int ew = 0; ew < nWindows; ++ew)
                    {
                        var window = 0;
                        for (int j = 0; j < windowSize && ew * windowSize + j < exponents.Length; ++j)
                            if ((exps[ew * windowSize + j][i] & mask) != 0)
                                window += 1 << j;

                        if (window != 0)
                        {
                            if (cache[ew, window] == null)
                            {
                                int[] w = null;
                                bool copied = false;
                                for (int j = 0; j < windowSize && ew * windowSize + j < exponents.Length; ++j)
                                    if ((exps[ew * windowSize + j][i] & mask) != 0)
                                        if (w == null)
                                            w = gs[ew * windowSize + j];
                                        else
                                        {
                                            if (!copied)
                                            {
                                                w = (int[])w.Clone();
                                                copied = true;
                                            }

                                            MultiplyMonty(accum, w, gs[ew * windowSize + j]);
                                        }

                                cache[ew, window] = w;
                            }

                            if (!nonZero)
                            {
                                Buffer.BlockCopy(cache[ew, window], 0, gi, 0, modulusMagnitude.Length * 4);
                                nonZero = true;
                            }
                            else
                                MultiplyMonty(accum, gi, cache[ew, window]);
                        }
                    }

                    if (foundFirst)
                        MultiplyMonty(accum, a, a);

                    if (nonZero)
                    {
                        if (!foundFirst)
                        {
                            Buffer.BlockCopy(gi, 0, a, 0, modulusMagnitude.Length * 4);
                            foundFirst = true;
                        }
                        else
                            MultiplyMonty(accum, a, gi);
                    }
                }
            }

            Array.Clear(gi, 0, gi.Length);
            gi[gi.Length - 1] = 1;
            MultiplyMonty(accum, a, gi);

            BigInteger result = FromData(a);
            return result;
        }

        protected void ExtractAligned(BigInteger[] exponents, out int[][] exps, out int maxExpLen)
        {
            exps = new int[exponents.Length][];
            maxExpLen = 0;
            for (int i = 0; i < exponents.Length; ++i)
            {
                exps[i] = GetData(exponents[i]);
                maxExpLen = Math.Max(maxExpLen, exps[i].Length);
            }

            for (int i = 0; i < exponents.Length; ++i)
                if (exps[i].Length < maxExpLen)
                    exps[i] = Extend(exps[i], maxExpLen);
        }

        public BigInteger Pow(BigInteger number, BigInteger exponent)
        {
            if (exponent.Sign() == 0)
                return BigInteger.One;

            if (number.Sign() == 0)
                return BigInteger.Zero;

            // zVal = number * R mod m
            BigInteger tmp = (number << (32 * modulusMagnitude.Length)) % modulus;
            var zVal = GetData(tmp);
            if (zVal.Length > modulusMagnitude.Length)
                return BigInteger.ModPow(number, exponent, modulus);

            var yAccum = new int[modulusMagnitude.Length + 1];
            if (zVal.Length < modulusMagnitude.Length)
                zVal = Extend(zVal, modulusMagnitude.Length);

            var yVal = new int[modulusMagnitude.Length];
            Buffer.BlockCopy(zVal, 0, yVal, 0, yVal.Length * 4);

            var exponentMagnitude = GetData(exponent);

            //
            // from LSW to MSW
            //
            for (int i = 0; i < exponentMagnitude.Length; i++)
            {
                int v = exponentMagnitude[i];
                int bits = 0;

                if (i == 0)
                {
                    while (v > 0)
                    {
                        v <<= 1;
                        bits++;
                    }

                    v <<= 1;
                    bits++;
                }

                while (v != 0)
                {
                    // Montgomery square algo doesn't exist, and a normal
                    // square followed by a Montgomery reduction proved to
                    // be almost as heavy as a Montgomery mulitply.
                    MultiplyMonty(yAccum, yVal, yVal);

                    bits++;

                    if (v < 0)
                        MultiplyMonty(yAccum, yVal, zVal);

                    v <<= 1;
                }

                while (bits < 32)
                {
                    MultiplyMonty(yAccum, yVal, yVal);
                    bits++;
                }
            }

            // Return y * R^(-1) mod m by doing y * 1 * R^(-1) mod m
            Array.Clear(zVal, 0, zVal.Length);
            zVal[zVal.Length - 1] = 1;
            MultiplyMonty(yAccum, yVal, zVal);

            BigInteger result = FromData(yVal);

            return exponent.Sign() > 0 ? result : Inverse(result);
        }

        public BigInteger Inverse(BigInteger number)
        {
            BigInteger x, y = BigInteger.Zero;
            BigInteger gcd = ExtEuclid((number % modulus), modulus, out x, ref y, false);

            if (gcd != 1)
                throw new ArithmeticException("Numbers not relatively prime.");

            if (x.Sign() < 0)
            {
                var magnitude = doSubBigLil(modulusMagnitude, GetData(x));
                x = FromData(magnitude);
            }

            return x;
        }

        private static int[] doSubBigLil(int[] bigMag, int[] lilMag)
        {
            int[] res = (int[])bigMag.Clone();
            return Subtract(0, res, 0, lilMag);
        }

        /**
      * Calculate the numbers u1, u2, and u3 such that:
      *
      * u1 * a + u2 * b = u3
      *
      * where u3 is the greatest common divider of a and b.
      * a and b using the extended Euclid algorithm (refer p. 323
      * of The Art of Computer Programming vol 2, 2nd ed).
      * This also seems to have the side effect of calculating
      * some form of multiplicative inverse.
      *
      * @param a First number to calculate gcd for
      * @param b Second number to calculate gcd for
      * @param u1Out the return object for the u1 value
      * @param u2Out the return object for the u2 value
      * @return The greatest common divisor of a and b
      */
        private static BigInteger ExtEuclid(BigInteger a, BigInteger b, out BigInteger u1, ref BigInteger u2, bool needU2)
        {
            u1 = BigInteger.One;
            BigInteger u3 = a;
            BigInteger v1 = BigInteger.Zero;
            BigInteger v3 = b;

            while (v3.Sign() > 0)
            {
                BigInteger remainder = u3 % v3;
                BigInteger quotient = u3/ v3;

                BigInteger tn = u1 - v1 * quotient;
                u1 = v1;
                v1 = tn;

                u3 = v3;
                v3 = remainder;
            }

            if (needU2)
                u2 = (u3 - u1 * a) / b;

            return u3;
        }

        protected static int[] Extend(int[] number, int size)
        {
            int[] longZ = new int[size];
            Buffer.BlockCopy(number, 0, longZ, (size - number.Length) * 4, number.Length * 4);
            return longZ;
        }

        /**
      * Montgomery multiplication: a = x * y * R^(-1) mod m
      * <br/>
      * Based algorithm 14.36 of Handbook of Applied Cryptography.
      * <br/>
      * <li> m, x, y should have length n </li>
      * <li> a should have length (n + 1) </li>
      * <li> b = 2^32, R = b^n </li>
      * <br/>
      * The result is put in x
      * <br/>
      * NOTE: the indices of x, y, m, a different in HAC and in Java
      */
        protected void MultiplyMonty(int[] a, int[] x, int[] y)
        // mQuote = -m^(-1) mod b
        {
            if (modulusMagnitude.Length == 1)
            {
                x[0] = (int)MultiplyMontyNIsOne((uint)x[0], (uint)y[0], (uint)modulusMagnitude[0], (ulong)mDash);
                return;
            }

            int n = modulusMagnitude.Length;
            int nMinus1 = n - 1;
            long y_0 = y[nMinus1] & IMASK;

            // 1. a = 0 (Notation: a = (a_{n} a_{n-1} ... a_{0})_{b} )
            Array.Clear(a, 0, n + 1);

            // 2. for i from 0 to (n - 1) do the following:
            for (int i = n; i > 0; i--)
            {
                long x_i = x[i - 1] & IMASK;

                // 2.1 u = ((a[0] + (x[i] * y[0]) * mQuote) mod b
                long u = ((((a[n] & IMASK) + ((x_i * y_0) & IMASK)) & IMASK) * mDash) & IMASK;

                // 2.2 a = (a + x_i * y + u * m) / b
                long prod1 = x_i * y_0;
                long prod2 = u * (modulusMagnitude[nMinus1] & IMASK);
                long tmp = (a[n] & IMASK) + (prod1 & IMASK) + (prod2 & IMASK);
                long carry = (long)((ulong)prod1 >> 32) + (long)((ulong)prod2 >> 32) + (long)((ulong)tmp >> 32);
                for (int j = nMinus1; j > 0; j--)
                {
                    prod1 = x_i * (y[j - 1] & IMASK);
                    prod2 = u * (modulusMagnitude[j - 1] & IMASK);
                    tmp = (a[j] & IMASK) + (prod1 & IMASK) + (prod2 & IMASK) + (carry & IMASK);
                    carry = (long)((ulong)carry >> 32) + (long)((ulong)prod1 >> 32) +
                    (long)((ulong)prod2 >> 32) + (long)((ulong)tmp >> 32);
                    a[j + 1] = (int)tmp; // division by b
                }
                carry += (a[0] & IMASK);
                a[1] = (int)carry;
                a[0] = (int)((ulong)carry >> 32); // OJO!!!!!
            }

            // 3. if x >= m the x = x - m
            if (CompareTo(0, a, 0, modulusMagnitude) >= 0)
                Subtract(0, a, 0, modulusMagnitude);

            // put the result in x
            Buffer.BlockCopy(a, 4, x, 0, n * 4);
        }

        private static uint MultiplyMontyNIsOne(uint x, uint y, uint m, ulong mQuote)
        {
            ulong um = m;
            ulong prod1 = (ulong)x * (ulong)y;
            ulong u = (prod1 * mQuote) & UIMASK;
            ulong prod2 = u * um;
            ulong tmp = (prod1 & UIMASK) + (prod2 & UIMASK);
            ulong carry = (prod1 >> 32) + (prod2 >> 32) + (tmp >> 32);

            if (carry > um)
                carry -= um;

            return (uint)(carry & UIMASK);
        }

        /**
      * unsigned comparison on two arrays - note the arrays may
      * start with leading zeros.
      */
        private static int CompareTo(int xIndx, int[] x, int yIndx, int[] y)
        {
            while (xIndx != x.Length && x[xIndx] == 0)
                xIndx++;

            while (yIndx != y.Length && y[yIndx] == 0)
                yIndx++;

            return CompareNoLeadingZeroes(xIndx, x, yIndx, y);
        }

        private static int CompareNoLeadingZeroes(int xIndx, int[] x, int yIndx, int[] y)
        {
            int diff = (x.Length - y.Length) - (xIndx - yIndx);

            if (diff != 0)
                return diff < 0 ? -1 : 1;

            // lengths of magnitudes the same, test the magnitude values

            while (xIndx < x.Length)
            {
                uint v1 = (uint)x[xIndx++];
                uint v2 = (uint)y[yIndx++];

                if (v1 != v2)
                    return v1 < v2 ? -1 : 1;
            }

            return 0;
        }

        /**
      * returns x = x - y - we assume x is >= y
      */
        private static int[] Subtract(int xStart, int[] x, int yStart, int[] y)
        {
            int iT = x.Length;
            int iV = y.Length;
            long m;
            int borrow = 0;

            do
            {
                m = (x[--iT] & IMASK) - (y[--iV] & IMASK) + borrow;
                x[iT] = (int)m;

                // borrow = (m < 0) ? -1 : 0;
                borrow = (int)(m >> 63);
            }
            while (iV > yStart);

            if (borrow != 0)
                while (--x[--iT] == -1) ;

            return x;
        }
    }

    public class MultiExponentiation : Modular
    {
        const int MaxChainLength = 4;

        class CacheNode
        {
            public Dictionary<int, CacheNode> next = new Dictionary<int, CacheNode>(MaxChainLength);
            public int[] number = null;
            public int length = 0;
        }

        BigInteger[] origBases;
        int[][] bases;
        CacheNode rootNode = new CacheNode();

        public MultiExponentiation(BigInteger modulus, BigInteger[] bases) : base(modulus)
        {
            origBases = bases;
            this.bases = new int[bases.Length][];
            for (int i = 0; i < bases.Length; ++i)
            {
                BigInteger g = (bases[i] << (32 * modulusMagnitude.Length)) % modulus;
                this.bases[i] = GetData(g);
                if (this.bases[i].Length < modulusMagnitude.Length)
                    this.bases[i] = Extend(this.bases[i], modulusMagnitude.Length);

                rootNode.next[i] = new CacheNode { number = this.bases[i], length = 1 };
            }
        }

        public BigInteger Pow(BigInteger[] exponents)
        {
            if (bases.Length != exponents.Length)
                throw new ArithmeticException("Same number of bases and exponents expected");

            int[][] exps;
            int maxExpLen;
            ExtractAligned(exponents, out exps, out maxExpLen);

            var accum = new int[modulusMagnitude.Length + 1];
            var a = new int[modulusMagnitude.Length];
            var gi = new int[modulusMagnitude.Length];
            bool foundFirst = false;

            for (int i = 0; i < maxExpLen; ++i)
            {
                for (int bit = 31; bit >= 0; --bit)
                {
                    bool nonZero = false;
                    var mask = 1 << bit;

                    var node = rootNode;
                    for (int e = 0; e < exponents.Length; ++e)
                        if ((exps[e][i] & mask) != 0)
                        {
                            CacheNode next;
                            if (!node.next.TryGetValue(e, out next))
                            {
                                next = new CacheNode { length = node.length + 1 };
                                next.number = (int[])node.number.Clone();
                                MultiplyMonty(accum, next.number, bases[e]);
                                node.next[e] = next;
                            }

                            node = next;
                            if (node.length == MaxChainLength)
                            {
                                if (nonZero)
                                    MultiplyMonty(accum, gi, node.number);
                                else
                                {
                                    Buffer.BlockCopy(node.number, 0, gi, 0, modulusMagnitude.Length * 4);
                                    nonZero = true;
                                }

                                node = rootNode;
                            }
                        }

                    if (node != rootNode)
                        if (nonZero)
                            MultiplyMonty(accum, gi, node.number);
                        else
                        {
                            Buffer.BlockCopy(node.number, 0, gi, 0, modulusMagnitude.Length * 4);
                            nonZero = true;
                        }

                    if (foundFirst)
                        MultiplyMonty(accum, a, a);

                    if (nonZero)
                    {
                        if (!foundFirst)
                        {
                            Buffer.BlockCopy(gi, 0, a, 0, modulusMagnitude.Length * 4);
                            foundFirst = true;
                        }
                        else
                            MultiplyMonty(accum, a, gi);
                    }
                }
            }

            Array.Clear(gi, 0, gi.Length);
            gi[gi.Length - 1] = 1;
            MultiplyMonty(accum, a, gi);

            BigInteger result = FromData(a);
            return result;
        }

        public BigInteger[] Bases { get { return origBases; } }
    }
}
