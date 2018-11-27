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

        private static uint _MaxVal => (uint)Math.Pow(2, _Base) - 1;

        public static readonly LargeInteger Zero = new LargeInteger(0L);

        public static readonly LargeInteger One = new LargeInteger(1L);
        private int dataLength => _data.Length;


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
                uintBytes[uintIndex] += (uint)(bytes[i] << shiftAmount);
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

            var bigInteger = new LargeInteger(0);
            var bi = new LargeInteger(1L);

            if (value == "0")
            {
                this._sign = 0;
                this._data = new uint[1] { 0 };
                return;
            }

            _sign = (value[0] == '-') ? -1 : 1;

            int limit = _sign == -1 ? 1 : 0;

            for (int i = value.Length - 1; i >= limit; i--)
            {
                int val = value[i];
                val = ((val >= 48 && val <= 57) ? (val - 48) : ((val < 65 || val > 90) ? 9999999 : (val - 65 + 10)));
                Throw.If(val >= radix, "Invalid string in constructor.");

                bigInteger += bi * val;

                if (i - 1 >= limit)
                    bi *= radix;
            }

            _data = null;
            InitFromArray(bigInteger._data);
        }

        public int Sign()
        {
            return _sign;
        }

        public static explicit operator int(LargeInteger value)
        {
            int result = (int)value._data[0];

            if (value._sign < 0)
                result *= -1;

            return result;
        }

        public static explicit operator long(LargeInteger value)
        {
            long result = 0;

            int max = value._data.Length;

            if (max > 2) max = 2;

            for (int i = 0; i < max; i++)
            {
                var bits = i * 32;
                result += ((value._data[i]) * (1 << bits));
            }

            if (value._sign < 0)
                result *= -1;

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
            return ToHex();
        }

        public string ToDecimal()
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
                    DivideAndModulus(largeInteger, bi, out largeInteger2, out largeInteger3);
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

        public string ToHex()
        {
            string result = "";

            foreach (var digit in _data)
            {
                result += digit.ToString("X8");
            }

            return result;
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
                ulong sum = (ulong)overflow + x + y;

                r[i] = (uint)sum;
                overflow = (uint)(sum >> _Base);
            }

            r[longest] = (byte)overflow;
            return r;
        }

        private static uint[] Subtract(uint[] X, uint[] Y)
        {
            var longest = X.Length > Y.Length ? X.Length : Y.Length;
            var r = new uint[longest];

            long num = 0L, num2;

            long x, y;

            for (int i = 0; i < r.Length; i++)
            {
                x = i < X.Length ? X[i] : 0;
                y = i < Y.Length ? Y[i] : 0;
                num2 = x - y - num;
                r[i] = (uint)(num2 & uint.MaxValue);
                num = ((num2 >= 0) ? 0 : 1);
            }

            //int num3 = maxLength - 1;
            //Throw.If(((int)X[num3] & -2147483648) != ((int)Y[num3] & -2147483648) && ((int)r[num3] & -2147483648) != ((int)X[num3] & -2147483648), "overflow in subtraction");

            return r;

            /*
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
            */
        }

        private static uint[] Multiply(uint[] X, uint[] Y)
        {
            uint[] output = new uint[X.Length + Y.Length + 1];

            for (int i = 0; i < X.Length; i++)
            {
                if (X[i] != 0)
                {
                    ulong num2 = 0uL;
                    int num3 = 0;
                    int num4 = i;
                    while (num3 < Y.Length)
                    {
                        ulong num5 = (ulong)((long)X[i] * (long)Y[num3] + output[num4] + (long)num2);
                        output[num4] = (uint)(num5 & uint.MaxValue);
                        num2 = num5 >> 32;
                        num3++;
                        num4++;
                    }
                    if (num2 != 0)
                    {
                        output[i + Y.Length] = (uint)num2;
                    }
                }
            }

            return output;

            /*
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
            */
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
            DivideAndModulus(Abs(a), Abs(b), out quot, out rem);
            quot._sign = quot._sign == 0 ? 0 : a._sign * b._sign;
            return quot;
        }

        public static LargeInteger operator %(LargeInteger a, LargeInteger b)
        {
            LargeInteger quot, rem;
            DivideAndModulus(a, b, out quot, out rem);

            if (rem < 0)    //using the convention that 0 <= rem <= denominator. So if rem < 0, add the denominator to it
                rem += b;

            return rem;
        }

        public static void DivideAndModulus(LargeInteger a, LargeInteger b, out LargeInteger quot, out LargeInteger rem)
        {
            if (b == 0)
            {
                quot = LargeInteger.Zero;
                rem = LargeInteger.Zero;
                return;
            }

            if (a._data.Length < b._data.Length)
            {
                quot = new LargeInteger(0);
                rem = new LargeInteger(a);
                return;
            }

            if (b._data.Length == 1)
                SingleDigitDivMod(a, b, out quot, out rem);
            else
                MultiDigitDivMod(a, b, out quot, out rem);

            quot._sign = a._sign * b._sign;

        }

        private static void SingleDigitDivMod(LargeInteger numerator, LargeInteger denominator, out LargeInteger quotient, out LargeInteger remainder)
        {
            uint[] array = new uint[numerator.dataLength - denominator.dataLength + 1];
            uint[] remArray = new uint[numerator.dataLength];
            int num = 0;
            for (int i = 0; i < numerator.dataLength; i++)
            {
                remArray[i] = numerator._data[i];
            }

            ulong num2 = denominator._data[0];
            int num3 = remArray.Length - 1;
            ulong num4 = remArray[num3];

            if (num4 >= num2)
            {
                ulong num5 = num4 / num2;
                array[num++] = (uint)num5;
                remArray[num3] = (uint)(num4 % num2);
            }

            num3--;
            while (num3 >= 0)
            {
                num4 = ((ulong)remArray[num3 + 1] << 32) + remArray[num3];
                ulong num7 = num4 / num2;
                array[num++] = (uint)num7;
                remArray[num3 + 1] = 0u;
                remArray[num3--] = (uint)(num4 % num2);
            }

            uint[] quotArray = new uint[num];
            int j = 0;
            int num10 = quotArray.Length - 1;
            while (num10 >= 0)
            {
                quotArray[j] = array[num10];
                num10--;
                j++;
            }

            quotient = new LargeInteger(quotArray);
            remainder = new LargeInteger(remArray);
        }

        private static void MultiDigitDivMod(LargeInteger numerator, LargeInteger denominator, out LargeInteger quot, out LargeInteger rem)
        {
            uint[] quotArray = new uint[numerator.dataLength - denominator.dataLength + 1];
            uint[] numArrayTmp = new uint[numerator.dataLength + 1];
            uint num2 = 2147483648u;
            uint num3 = denominator._data[denominator.dataLength - 1];
            int num4 = 0;
            int num5 = 0;

            while (num2 != 0 && (num3 & num2) == 0)
            {
                num4++;
                num2 >>= 1;
            }
            for (int i = 0; i < numerator.dataLength; i++)
            {
                numArrayTmp[i] = numerator._data[i];
            }

            ShiftLeft(numArrayTmp, num4);
            denominator <<= num4;

            int j = numerator.dataLength - denominator.dataLength + 1;
            int num7 = numerator.dataLength;
            ulong num8 = denominator._data[denominator.dataLength - 1];
            ulong num9 = denominator._data[denominator.dataLength - 2];
            int num10 = denominator.dataLength + 1;

            uint[] array3 = new uint[num10];

            while (j > 0)
            {
                ulong num11 = ((ulong)numArrayTmp[num7] << 32) + numArrayTmp[num7 - 1];
                ulong num12 = num11 / num8;
                ulong num13 = num11 % num8;
                bool flag = false;
                while (!flag)
                {
                    flag = true;
                    if (num12 == 4294967296L || num12 * num9 > (num13 << 32) + numArrayTmp[num7 - 2])
                    {
                        num12--;
                        num13 += num8;
                        if (num13 < 4294967296L)
                        {
                            flag = false;
                        }
                    }
                }

                for (int k = 0; k < num10; k++)
                {
                    array3[(array3.Length - 1) - k] = numArrayTmp[num7 - k];
                }

                var bigInteger = new LargeInteger(array3);
                LargeInteger bigInteger2 = denominator * (long)num12;
                while (bigInteger2 > bigInteger)
                {
                    num12--;
                    bigInteger2 -= denominator;
                }
                LargeInteger bigInteger3 = bigInteger - bigInteger2;
                for (int k = 0; k < num10; k++)
                {
                    uint tmp = denominator.dataLength - k < bigInteger3._data.Length
                        ? bigInteger3._data[denominator.dataLength - k]
                        : 0;
                    numArrayTmp[num7 - k] = tmp;
                }

                num7--;
                j--;
                quotArray[j] = (uint)num12;
            }

            quot = new LargeInteger(quotArray);

            ShiftRight(numArrayTmp, num4);

            rem = new LargeInteger(numArrayTmp);
        }

        public static LargeInteger operator >>(LargeInteger n, int bits)
        {
            var mult = Pow(2, bits);
            return n / mult;
        }

        private static void ShiftRight(uint[] buffer, int shiftVal)
        {
            int num = 32;
            int num2 = 0;
            int num3 = buffer.Length;
            while (num3 > 1 && buffer[num3 - 1] == 0)
            {
                num3--;
            }

            for (int num4 = shiftVal; num4 > 0; num4 -= num)
            {
                if (num4 < num)
                {
                    num = num4;
                    num2 = 32 - num;
                }

                ulong num5 = 0uL;
                for (int num6 = num3 - 1; num6 >= 0; num6--)
                {
                    ulong num7 = (ulong)buffer[num6] >> num;
                    num7 |= num5;
                    num5 = (((ulong)buffer[num6] << num2) & uint.MaxValue);
                    buffer[num6] = (uint)num7;
                }
            }

            while (num3 > 1 && buffer[num3 - 1] == 0)
            {
                num3--;
            }
        }


        public static LargeInteger operator <<(LargeInteger n, int bits)
        {

            var mult = Pow(2, bits);
            return n * mult;
            /*
            var result = new LargeInteger(n);
            ShiftLeft(result._data, bits);
            return result;
            */
        }

        private static void ShiftLeft(uint[] buffer, int shiftVal)
        {
            int num = 32;
            int num2 = buffer.Length;
            while (num2 > 1 && buffer[num2 - 1] == 0)
            {
                num2--;
            }

            for (int num3 = shiftVal; num3 > 0; num3 -= num)
            {
                if (num3 < num)
                {
                    num = num3;
                }

                ulong num4 = 0uL;

                for (int i = 0; i < num2; i++)
                {
                    ulong num5 = (ulong)buffer[i] << num;
                    num5 |= num4;
                    buffer[i] = (uint)(num5 & uint.MaxValue);
                    num4 = num5 >> 32;
                }

                if (num4 != 0 && num2 + 1 <= buffer.Length)
                {
                    buffer[num2] = (uint)num4;
                    num2++;
                }
            }
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

        public uint[] ToUintArray()
        {
            return (uint[])_data.Clone();
        }

        //TODO: this probably needs looking into..
        public override int GetHashCode()
        {
            long hashCode = -1521134295 * _sign;

            // Rotate by 3 bits and XOR the new value
            for (var i = 0; i < _data.Length; i++)
            {
                hashCode = (int)((hashCode << 3) | (hashCode >> (29)) ^ _data[i]);
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