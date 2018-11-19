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
        private uint[] _data;
        private const int _Base = sizeof(uint) * 8;    //number of bits required for shift operations

        private static uint _MaxVal => (uint) Math.Pow(2, _Base) - 1;

        public static readonly LargeInteger Zero = new LargeInteger(0L);

        public static readonly LargeInteger One = new LargeInteger(1L);

        

        public LargeInteger(LargeInteger other)
        {
            this._sign = other._sign;
            this._data = new uint[other._data.Length];
            Array.Copy(other._data, this._data, this._data.Length);
        }

        public LargeInteger(uint[] bytes, int sign = 1)
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
                this._data = new uint[1] { 0 };
                return;
            }

            _sign = val < 0 ? -1 : 1;

            if (val < 0) val = -val;

            
            var bytes = BitConverter.GetBytes(val);

            var uintBytes = new uint[(bytes.Length / 4) + 1];

            for (int i = 0; i < bytes.Length; i++)
            {
                int uintIndex = (i / 4);
                int shiftAmount = (i % 4) * 8;
                uintBytes[uintIndex] += (uint) (bytes[i] << shiftAmount);
            }

            _data = null;
            InitFromArray(uintBytes);
        }

        //TODO: CONVERT TO UINT INSTEAD OF BYTES
        private void InitFromArray(uint[] bytes)
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

            _data = new uint[n];
            Array.Copy(bytes, _data, n);
        }

        public LargeInteger(string value, int radix)
        {
            value = value.ToUpper().Trim();

            if (value == "0")
            {
                this._sign = 0;
                this._data = new uint[1] { 0 };
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
                result += (int) value._data[i] * (1 << bits);
            }

            if (value._sign < 0)
            {
                result *= -1;
            }

            return result;
        }

        public static explicit operator long(LargeInteger value)
        {
            long result = 0;

            int max = value._data.Length;

            if (max > 8) max = 8;

            for (int i = 0; i < max; i++)
            {
                var bits = i * 8;
                result += (uint)((value._data[i]) * (1 << bits));
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

        /*
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
            var sourceBase = 4294967296;
            var targetBase = 10;
            while (true)
            {
                var byteArray = val._data;
                var arrayLength = val._data.Length;
                var newArray = new uint[arrayLength];

                long k = 0;
                for (var j = arrayLength - 1; j >= 0; j--)
                {
                    newArray[j] = (uint)((k * sourceBase + byteArray[j]) / targetBase);
                    k = (k * sourceBase + byteArray[j]) - (newArray[j] * targetBase);
                }

                var c = (char)(48 + k);
                buffer[outLength] = c;
                outLength++;

                if (newArray.Length == 1 && newArray[0] == 0)
                {
                    break;
                }

                val = new LargeInteger(newArray);
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
        */
        public override string ToString()
        {
            int radix = 10;
            Throw.If(radix < 2 || radix > 36, "Radix must be >= 2 and <= 36");

            string text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string text2 = "";
            LargeInteger largeInteger = this;
            bool flag = false;

            var largeInteger2 = new LargeInteger();
            var largeInteger3 = new LargeInteger();
            var bi = new LargeInteger(radix);
            if (largeInteger._data.Length == 0 || (largeInteger._data.Length == 1 && largeInteger._data[0] == 0))
            {
                text2 = "0";
            }
            else
            {
                while (largeInteger._data.Length > 1 || (largeInteger._data.Length == 1 && largeInteger._data[0] != 0))
                {
                    DivMod(largeInteger, bi, out largeInteger2, out largeInteger3);
                    if (largeInteger3._data.Length == 0)
                        text2 = "0" + text2;
                    else
                        text2 = ((largeInteger3._data[0] >= 10) ? (text[(int)(largeInteger3._data[0] - 10)].ToString() + text2) : (largeInteger3._data[0] + text2));
                    largeInteger = largeInteger2;
                }
                if (_sign < 1 && text != "0")
                {
                    text2 = "-" + text2;
                }
            }

            return text2;
        }

        /*
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
        */

        private static void DivMod(LargeInteger dividend, LargeInteger divisor, out LargeInteger quot, out LargeInteger rem)
        {
            if (divisor == 0)
            {
                quot = LargeInteger.Zero;
                rem = LargeInteger.Zero;
                return;
            }

            var dividendArray = dividend._data;
            var divisorArray = divisor._data;
            var quotient = new uint[dividend._data.Length - divisor._data.Length + 1];
            var rest = new uint[divisor._data.Length];

            var b = _Base; // Number base (32 bits).
            //unsigned short* un, *vn;  // Normalized form of u, v.
            int i, j, q;

            var m = dividendArray.Length;
            var n = divisorArray.Length;

            if (m < n)
            {
                quot = new LargeInteger(0);
                rem = new LargeInteger(dividend);
                return;
            }

            var interimDividend = new LargeInteger(0);

            /*
            // single digit divisor
            //if (n == 1)
            {
                for (i = 0, j = m - 1, q = 0; j >= 0; i++, j--)
                {
                    interimDividend.ShiftAndInsertUint(dividendArray[j]); //"down goes one"

                    if (interimDividend < divisor)
                        continue;

                    //how many times does the divisor fit on the current dividend
                    if (interimDividend._data.Length == 1)
                    {
                        quotient[q] = (uint)(((long)interimDividend) / ((long)divisor));
                        interimDividend -= divisor * quotient[q];
                    }
                    else if (interimDividend._data.Length == 2)
                    {

                    }

                    q++;
                }
            }
            else*/
            {
                for (i = 0, j = m - 1, q = 0; j >= 0; i++, j--)
                {
                    interimDividend.ShiftAndInsertUint(dividendArray[j]); //"down goes one"

                    if (interimDividend < divisor)
                        continue;

                    //how many times does the divisor fit on the current dividend
                    while (true)
                    {
                        interimDividend -= divisor;
                        quotient[q]++;

                        if (interimDividend < divisor)  //if it doesn't fit anymore, go to the next iteration
                        {
                            q++;
                            break;
                        }
                    }
                }
            }

            quot = new LargeInteger(quotient);
            rem = new LargeInteger(interimDividend);
        }

        // Left-shifts a byte array in place. Assumes little-endian. Throws on overflow.
        private static void ShiftByteArrayLeft(byte[] array, byte newVal = 0)
        {
            if (array == null || array.Length == 0)
                throw new ArgumentNullException("array");

            // move left-to-right, left-shifting each byte
            for (int i = array.Length - 1; i >= 1; --i)
            {
                array[i] = array[i - 1];
            }

            array[0] = newVal;
        }

        private void ShiftAndInsertUint(uint newVal)
        {
            _sign = 1;

            if (_data.Length == 1 && _data[0] == 0)
            {
                _data[0] = newVal;
                return;
            }
                
            uint[] newData = new uint[_data.Length + 1];

            for (int i = newData.Length - 1; i > 0; i--)
                newData[i] = _data[i - 1];

            newData[0] = newVal;

            _data = newData;
        }

        private static uint[] Add(uint[] X, uint[] Y)
        {
            var longest = Math.Max(X.Length, Y.Length);
            var r = new uint[longest + 1];
            
            uint overflow = 0;
            for (int i = 0; i < longest; i++)
            {
                uint x = i < X.Length ? X[i] : (uint)0;
                uint y = i < Y.Length ? Y[i] : (uint)0;
                long sum = overflow + x + y;

                r[i] = (uint)sum;
                overflow = (uint) (sum >> _Base);
            }

            r[longest] = (byte)overflow;
            return r;
        }

        private static uint[] Subtract(uint[] X, uint[] Y)
        {
            var longest = Math.Max(X.Length, Y.Length);
            var r = new uint[longest];

            uint borrow = 0;
            for (int i = 0; i < longest; i++)
            {
                uint x = i < X.Length ? X[i] : (uint)0;
                uint y = i < Y.Length ? Y[i] : (uint)0;

                //check if borrowing is necessary
                if (x < y)
                {
                    int j = i + 1;

                    //check what is the first non zero column to the left of the current one
                    while (j < X.Length && X[j] == 0)
                        j++;

                    if(j == X.Length)
                        throw new Exception("This subtraction will lead to a negative number. Why do you do this to me senpai");

                    X[j]--; //borrow from the first non zero

                    //now go back, merrily distributing the borrow along the way
                    j--;
                    while (j != i)
                    {
                        X[j] = _MaxVal; //remember that this code is reached only if X[j] is 0
                        j--;
                    }

                    borrow = _MaxVal + 1;
                }

                uint sum = (borrow + x) - y;
                borrow = 0;

                r[i] = sum;
            }

            return r;
        }

        private static uint[] Multiply(uint[] X, uint[] Y)
        {
            var r = new uint[X.Length + Y.Length + 1];

            long sum = 0;
            uint carryOver = 0;
            

            for (int j = 0; j < Y.Length; j++)
            {
                for (int i = 0; i < X.Length; i++)
                {
                    sum = r[i+j] + ((long)X[i] * Y[j]);

                    r[i + j] = (uint) sum;
                    carryOver = (uint) (sum >> _Base);

                    int z = 1;
                    while (carryOver > 0)
                    {
                        sum = r[i + j + z] + carryOver;

                        r[i + j + z] = (uint) sum;
                        carryOver = (uint) (sum >> _Base);

                        z++;
                    }
                    
                }
            }

            return r;
        }

        public static LargeInteger operator +(LargeInteger a, LargeInteger b)
        {
            LargeInteger result;

            //all these if-else's are to make sure we don't attempt operations that would give a negative result,
            //allowing the large int operations to deal only in the scope of unsigned numbers
            if (a._sign < 0 && b._sign < 0)
            {
                result = new LargeInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : -1;
            }
            else
            if (a._sign < 0)
            {
                if (Abs(a) < b)
                {
                    result = new LargeInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : 1;
                }
                else
                {
                    result = new LargeInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : -1;
                }
            }
            else if (b._sign < 0)
            {
                if (a < Abs(b))
                {
                    result = new LargeInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : -1;
                }
                else
                {
                    result = new LargeInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : 1;
                }
            }
            else
            {
                result = new LargeInteger(Add(b._data, a._data));
                result._sign = result == 0 ? 0 : 1;
            }

            return result;
        }

        public static LargeInteger operator -(LargeInteger a, LargeInteger b)
        {
            LargeInteger result;

            //all these if-else's are to make sure we don't attempt operations that would give a negative result,
            //allowing the large int operations to deal only in the scope of unsigned numbers
            if (a._sign < 0 && b._sign < 0)
            {
                if (Abs(a) < Abs(b))
                {
                    result = new LargeInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : 1;
                }
                else
                {
                    result = new LargeInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : -1;
                }
            }
            else
            if (a._sign < 0)
            {
                result = new LargeInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : -1;
            }
            else if (b._sign < 0)
            {
                result = new LargeInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : 1;
            }
            else
            {
                if (a < b)
                {
                    result = new LargeInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : -1;
                }
                else
                {
                    result = new LargeInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : 1;
                }
            }

            return result;
        }

        public static LargeInteger operator *(LargeInteger a, LargeInteger b)
        {
            var result = new LargeInteger(Multiply(a._data, b._data));
            result._sign = a._sign * b._sign;
            return result;
        }

        public static LargeInteger operator /(LargeInteger a, LargeInteger b)
        {
            LargeInteger quot, rem;
            DivMod(Abs(a), Abs(b), out quot, out rem);
            quot._sign = quot._sign == 0 ? 0 : a._sign * b._sign;
            return quot;
        }

        public static LargeInteger operator %(LargeInteger a, LargeInteger b)
        {
            LargeInteger quot, rem;
            DivMod(a, b, out quot, out rem);

            if (rem < 0)    //using the convention that 0 <= rem <= divisor. So if rem < 0, add the divisor to it
                rem += b;

            return rem;
        }

        public static void DivideAndModulus(LargeInteger a, LargeInteger b, out LargeInteger quot, out LargeInteger rem)
        {
            DivMod(a, b, out quot, out rem);
            quot._sign = a._sign * b._sign;
            
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
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : (uint)0;
                uint B = i < b._data.Length ? b._data[i] : (uint)0;
                temp[i] = (byte)(A ^ B);
            }

            return new LargeInteger(temp);
        }

        public static LargeInteger operator |(LargeInteger a, LargeInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : (uint)0;
                uint B = i < b._data.Length ? b._data[i] : (uint)0;
                temp[i] = (byte)(A | B);
            }

            return new LargeInteger(temp);
        }

        public static LargeInteger operator &(LargeInteger a, LargeInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : (uint)0;
                uint B = i < b._data.Length ? b._data[i] : (uint)0;
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

        //TODO: this probably needs looking into..
        public override int GetHashCode()
        {
            long hashCode = -1521134295 * _sign;

            // Rotate by 3 bits and XOR the new value
            for (var i = 0; i < _data.Length; i++)
            {
                hashCode = (int) ((hashCode << 3) | (hashCode >> (29)) ^ _data[i]);
            }

            return (int)hashCode;
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