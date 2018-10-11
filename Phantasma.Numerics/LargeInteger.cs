using Phantasma.Core;
using System;
using System.Linq;
using System.Text;

/*
 * Implementation of LargeInteger class, written for Phantasma project
 * Author: Simão Pavlovich
 */

namespace Phantasma.Numerics
{
    public struct LargeInteger : IEquatable<LargeInteger>, IComparable<LargeInteger>
    {
        private int _sign;
        private byte[] _data;

        public static readonly LargeInteger Zero = new LargeInteger(0L);

        public static readonly LargeInteger One = new LargeInteger(1L);

        public LargeInteger(LargeInteger other)
        {
            this._sign = other._sign;
            this._data = new byte[other._data.Length];
            Array.Copy(other._data, this._data, this._data.Length);
        }

        public LargeInteger(byte[] bytes, int sign = 1)
        {
            this._sign = sign;
            this._data = null;
            InitFromArray(bytes);
        }

        public LargeInteger(int val) : this((long)val)
        {
        }

        public LargeInteger(uint val) : this((long)val)
        {
        }

        public LargeInteger(long val)
        {
            if (val == 0)
            {
                this._sign = 0;
                this._data = new byte[1] { 0 };
                return;
            }

            _sign = val < 0 ? -1 : 1;

            if (val < 0) val = -val;

            var bytes = BitConverter.GetBytes(val);
            _data = null;
            InitFromArray(bytes);
        }

        private void InitFromArray(byte[] bytes)
        {
            int n = bytes.Length;
            for (int i = n - 1; i >= 0; i--)
            {
                if (bytes[i] == 0)
                {
                    n--;
                }
                else
                {
                    break;
                }
            }

            _data = new byte[n];
            Array.Copy(bytes, _data, n);
        }

        public LargeInteger(string value, int radix)
        {
            value = value.ToUpper().Trim();

            if (value == "0")
            {
                this._sign = 0;
                this._data = new byte[1] { 0 };
                return;
            }

            _sign = (value[0] == '-') ? -1 : 1;

            var accum = LargeInteger.Zero;
            var scale = LargeInteger.One;
            for (int i = value.Length - 1; i >= 0; i--)
            {
                int digit = value[i];

                if (digit >= 48 && digit <= 57)
                {
                    digit -= 48;
                }
                else
                if (digit < 65 || digit > 90)
                {
                    digit -= 65;
                    digit += 10;
                }
                else
                {
                    throw new Exception("LOL");
                }

                var n = scale * digit;
                accum = accum + n;
                scale = scale * 10;
            }

            _data = null;
            InitFromArray(accum._data);
        }

        public int Sign()
        {
            return _sign;
        }

        public static explicit operator int(LargeInteger value)
        {
            int result = 0;

            int max = value._data.Length;

            if (max > 4) max = 4;

            for (int i = 0; i < max; i++)
            {
                var bits = i * 8;
                result += value._data[i] * (1 << bits);
            }

            if (value._sign < 0)
            {
                result *= -1;
            }

            return result;
        }

        public static implicit operator LargeInteger(int val)
        {
            return new LargeInteger(val);
        }

        public static implicit operator LargeInteger(long val)
        {
            return new LargeInteger(val);
        }

        public static LargeInteger Abs(LargeInteger x)
        {
            return new LargeInteger(x._data, 1);
        }

        public override string ToString()
        {
            if (_sign == 0)
            {
                return "0";
            }

            var maxExpectedLength = 1 + 3 * _data.Length;
            var buffer = new char[maxExpectedLength];
            int outLength = 0;

            var val = new LargeInteger(this);
            var b = 256;
            var radix = 10;
            while (true)
            {
                var u = val._data;
                var m = val._data.Length;
                var q = new byte[m];

                var k = 0;
                for (var j = m - 1; j >= 0; j--)
                {
                    q[j] = (byte)((k * b + u[j]) / radix);
                    k = (k * b + u[j]) - (q[j] * radix);
                }

                var c = (char)(48 + k);
                buffer[outLength] = c;
                outLength++;

                if (q.Length == 1 && q[0] == 0)
                {
                    break;
                }

                val = new LargeInteger(q);
            }

            if (_sign < 0)
            {
                buffer[outLength] = '-';
                outLength++;
            }

            var temp = new StringBuilder(outLength);
            for (int i = outLength - 1; i >= 0; i--)
            {
                temp.Append(buffer[i]);
            }

            return temp.ToString();
        }

        private static int nlz(int x)
        {
            int n;

            if (x == 0) return (32);
            n = 0;
            if (x <= 0x0000FFFF) { n = n + 16; x = x << 16; }
            if (x <= 0x00FFFFFF) { n = n + 8; x = x << 8; }
            if (x <= 0x0FFFFFFF) { n = n + 4; x = x << 4; }
            if (x <= 0x3FFFFFFF) { n = n + 2; x = x << 2; }
            if (x <= 0x7FFFFFFF) { n = n + 1; }
            return n;
        }

        // implementation of Knuth's Algorithm D, for a binary computer with base b = 2**8.  
        // The caller supplies
        //1. Space q for the quotient, m - n + 1 halfwords(at least one).
        //2. Space r for the remainder(optional), n halfwords.
        //3. The dividend u, m halfwords, m >= 1.   

        private static void DivMod(LargeInteger X, LargeInteger Y, out LargeInteger quot, out LargeInteger rem)
        {
            if (Y == 0)
            {
                quot = LargeInteger.Zero;
                rem = LargeInteger.Zero;
                return;
            }

            var u = X._data;
            var v = Y._data;
            var q = new byte[X._data.Length - Y._data.Length + 1];
            var r = new byte[Y._data.Length];

            var b = 256; // Number base (8 bits).
            //unsigned short* un, *vn;  // Normalized form of u, v.
            byte qhat;            // Estimated quotient digit.
            byte rhat;            // A remainder.
            int p;               // Product of two digits.
            int s, i, j;
            byte t;
            byte k;

            var m = u.Length;
            var n = v.Length;

            /*if (m < n)
                return 1;              // Return if invalid param.
                */

            // single digit divisor
            if (n == 1)
            {
                k = 0;
                for (j = m - 1; j >= 0; j--)
                {
                    q[j] = (byte)((k * b + u[j]) / v[0]);
                    k = (byte)((k * b + u[j]) - q[j] * v[0]);
                }

                r[0] = k;
                quot = new LargeInteger(q);
                rem = new LargeInteger(r);
                return;
            }

            // Normalize by shifting v left just enough so that
            // its high-order bit is on, and shift u left the
            // same amount.  We may have to append a high-order
            // digit on the dividend; we do that unconditionally.

            s = nlz(v[n - 1]) - 8;        // 0 <= s <= 7.
            var vn = new byte[2 * n];
            for (i = n - 1; i > 0; i--)
                vn[i] = (byte)((v[i] << s) | (v[i - 1] >> 16 - s));
            vn[0] = (byte)(v[0] << s);

            var un = new byte[2 * (m + 1)];
            un[m] = (byte)(u[m - 1] >> 16 - s);
            for (i = m - 1; i > 0; i--)
                un[i] = (byte)((u[i] << s) | (u[i - 1] >> 16 - s));
            un[0] = (byte)(u[0] << s);

            for (j = m - n; j >= 0; j--)
            {       // Main loop.
                    // Compute estimate qhat of q[j].
                qhat = (byte)((un[j + n] * b + un[j + n - 1]) / vn[n - 1]);
                rhat = (byte)((un[j + n] * b + un[j + n - 1]) - qhat * vn[n - 1]);
                again:
                if (qhat >= b || qhat * vn[n - 2] > b * rhat + un[j + n - 2])
                {
                    qhat--;
                    rhat += vn[n - 1];
                    if (rhat < b) goto again;
                }

                // Multiply and subtract.
                k = 0;
                for (i = 0; i < n; i++)
                {
                    p = qhat * vn[i];
                    t = (byte)(un[i + j] - k - (p & 0xFF));
                    un[i + j] = t;
                    k = (byte)((p >> 16) - (t >> 16));
                }

                t = (byte)(un[j + n] - k);
                un[j + n] = t;

                q[j] = qhat;              // Store quotient digit.
                if (t < 0)
                {              // If we subtracted too
                    q[j]--;       // much, add back.
                    k = 0;
                    for (i = 0; i < n; i++)
                    {
                        t = (byte)(un[i + j] + vn[i] + k);
                        un[i + j] = t;
                        k = (byte)(t >> 8);
                    }

                    un[j + n] += k;
                }
            } // End j.

            for (i = 0; i < n; i++)
                r[i] = (byte)((un[i] >> s) | (un[i + 1] << 16 - s));

            quot = new LargeInteger(q);
            rem = new LargeInteger(r);
        }

        private static byte[] Add(byte[] X, byte[] Y)
        {
            var longest = Math.Max(X.Length, Y.Length);
            var r = new byte[longest + 1];

            uint overflow = 0;
            for (int i = 0; i < longest; i++)
            {
                byte x = i < X.Length ? X[i] : (byte)0;
                byte y = i < Y.Length ? Y[i] : (byte)0;
                uint sum = overflow + x + y;

                r[i] = (byte)sum;
                overflow = sum >> 8;
            }

            r[longest] = (byte)overflow;
            return r;
        }

        private static byte[] Subtract(byte[] X, byte[] Y)
        {
            var longest = Math.Max(X.Length, Y.Length);
            var r = new byte[longest + 1];

            uint overflow = 0;
            for (int i = 0; i < longest; i++)
            {
                byte x = i < X.Length ? X[i] : (byte)0;
                byte y = i < Y.Length ? Y[i] : (byte)0;
                uint sum = (overflow + x) - y;

                r[i] = (byte)sum;
                overflow = sum >> 8;
            }

            r[longest] = (byte)overflow;
            return r;
        }

        private static byte[] Multiply(byte[] X, byte[] Y)
        {
            int n = 0;

            // BUG how to calculate the proper length of a X times Y when X has N bits and Y has M bits?
            var r = new byte[X.Length + Y.Length];

            uint overflow = 0;
            for (int j = 0; j < Y.Length; j++)
            {
                for (int i = 0; i < X.Length; i++)
                {
                    var sum = (X[i] * Y[j]) + overflow;
                    r[n] = (byte)sum;
                    n++;
                    overflow = (byte)(sum >> 8);
                }
            }

            r[n] = (byte)overflow;

            return r;
        }

        public static LargeInteger operator +(LargeInteger a, LargeInteger b)
        {
            return new LargeInteger(Add(a._data, b._data));
        }

        public static LargeInteger operator -(LargeInteger a, LargeInteger b)
        {
            return new LargeInteger(Subtract(a._data, b._data));
        }

        public static LargeInteger operator *(LargeInteger a, LargeInteger b)
        {
            return new LargeInteger(Multiply(a._data, b._data));
        }

        public static LargeInteger operator /(LargeInteger a, LargeInteger b)
        {
            LargeInteger quot, rem;
            DivMod(a, b, out quot, out rem);
            return quot;
        }

        public static LargeInteger operator %(LargeInteger a, LargeInteger b)
        {
            LargeInteger quot, rem;
            DivMod(a, b, out quot, out rem);
            return rem;
        }

        public static LargeInteger operator >>(LargeInteger n, int bits)
        {
            var mult = Pow(2, bits);
            return n / mult;
        }

        public static LargeInteger operator <<(LargeInteger n, int bits)
        {
            var mult = Pow(2, bits);
            return n * mult;
        }

        // TODO optimize me
        public static LargeInteger operator ++(LargeInteger n)
        {
            return n + 1;
        }

        // TODO optimize me
        public static LargeInteger operator --(LargeInteger n)
        {
            return n - 1;
        }

        public static LargeInteger operator -(LargeInteger n)
        {
            return new LargeInteger(n._data, -n._sign);
        }

        public static bool operator ==(LargeInteger a, LargeInteger b)
        {
            return a._data.Length == b._data.Length && a._sign == b._sign && a._data.SequenceEqual(b._data);
        }

        public static bool operator !=(LargeInteger a, LargeInteger b)
        {
            return a._data.Length != b._data.Length || a._sign != b._sign || !a._data.SequenceEqual(b._data);
        }

        private static bool LogicalCompare(LargeInteger a, LargeInteger b, bool op)
        {
            if (a._sign < b._sign)
            {
                return op;
            }
            else
            if (a._sign > b._sign)
            {
                return !op;
            }

            if (a._data.Length < b._data.Length)
            {
                return op;
            }
            else
            if (a._data.Length > b._data.Length)
            {
                return !op;
            }

            var A = a._data;
            var B = b._data;
            for (int i = A.Length - 1; i >= 0; i--)
            {
                var x = A[i];
                var y = B[i];
                if (x < y)
                {
                    return op;
                }
                else
                if (x > y)
                {
                    return !op;
                }
            }

            return false;
        }

        public static bool operator <(LargeInteger a, LargeInteger b)
        {
            return LogicalCompare(a, b, true);
        }

        public static bool operator >(LargeInteger a, LargeInteger b)
        {
            return LogicalCompare(a, b, false);
        }

        public static bool operator <=(LargeInteger a, LargeInteger b)
        {
            return (a == b || a < b);
        }

        public static bool operator >=(LargeInteger a, LargeInteger b)
        {
            return (a == b || a > b);
        }

        public static LargeInteger operator ^(LargeInteger a, LargeInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new byte[len];


            for (int i = 0; i < len; i++)
            {
                byte A = i < a._data.Length ? a._data[i] : (byte)0;
                byte B = i < b._data.Length ? b._data[i] : (byte)0;
                temp[i] = (byte)(A ^ B);
            }

            return new LargeInteger(temp);
        }

        public static LargeInteger operator |(LargeInteger a, LargeInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new byte[len];


            for (int i = 0; i < len; i++)
            {
                byte A = i < a._data.Length ? a._data[i] : (byte)0;
                byte B = i < b._data.Length ? b._data[i] : (byte)0;
                temp[i] = (byte)(A | B);
            }

            return new LargeInteger(temp);
        }

        public static LargeInteger operator &(LargeInteger a, LargeInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new byte[len];


            for (int i = 0; i < len; i++)
            {
                byte A = i < a._data.Length ? a._data[i] : (byte)0;
                byte B = i < b._data.Length ? b._data[i] : (byte)0;
                temp[i] = (byte)(A & B);
            }

            return new LargeInteger(temp);
        }

        public bool Equals(LargeInteger other)
        {
            if (other._data.Length != this._data.Length)
            {
                return false;
            }

            return _data.SequenceEqual(other._data);
        }

        public int CompareTo(LargeInteger other)
        {
            if (other.Equals(this))
            {
                return 0;
            }

            if (other < this)
            {
                return -1;
            }

            return 1;
        }

        public static LargeInteger Pow(LargeInteger a, LargeInteger b)
        {
            var val = LargeInteger.One;
            var i = LargeInteger.Zero;

            while (i < b)
            {
                val *= a;
                i = i + LargeInteger.One;
            }
            return val;
        }

        /// <summary>
        /// Modulo Exponentiation
        /// </summary>
        /// <param name="exp">Exponential</param>
        /// <param name="n">Modulo</param>
        /// <returns>LargeInteger result of raising this to the power of exp and then modulo n </returns>
        public LargeInteger ModPow(LargeInteger exp, LargeInteger n)
        {
            Throw.If(exp._sign < 0, "Positive exponents only.");
            var temp = this * exp;
            return temp % n;
        }

        public static LargeInteger Parse(string input, int radix = 10)
        {
            return new LargeInteger(input, radix);
        }

        public static bool TryParse(string input, out LargeInteger output)
        {
            try
            {
                output = new LargeInteger(input, 10);
                return true;
            }
            catch
            {
                output = LargeInteger.Zero;
                return false;
            }
        }

        // TODO check if correct
        public int GetBitLength()
        {
            return _data.Length * 8;
        }

        public byte[] ToByteArray()
        {
            return (byte[])_data.Clone();
        }

        public override int GetHashCode()
        {
            var hashCode = -1521134295 * _sign;

            // Rotate by 3 bits and XOR the new value
            for (var i = 0; i < _data.Length; i++)
            {
                hashCode = (hashCode << 3) | (hashCode >> (29)) ^ _data[i];
            }

            return hashCode;
        }

        public override bool Equals(object obj)
        {
            if (obj is LargeInteger)
            {
                var temp = (LargeInteger)obj;
                return temp._sign == this._sign && temp._data.SequenceEqual(this._data);
            }

            return false;
        }
    }
}