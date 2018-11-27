using System;
using System.Linq;
using Phantasma.Core;

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
            _sign = other._sign;
            _data = new uint[other._data.Length];
            Array.Copy(other._data, _data, _data.Length);
        }

        public LargeInteger(uint[] bytes, int sign = 1)
        {
            _sign = sign;
            _data = null;

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
                _sign = 0;
                _data = new uint[1] { 0 };
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
                _sign = 0;
                _data = new uint[1] { 0 };
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
                        text2 = ((largeInteger3._data[0] >= 10) ? (text[(int)(largeInteger3._data[0] - 10)] + text2) : (largeInteger3._data[0] + text2));
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
                uint x = i < X.Length ? X[i] : 0;
                uint y = i < Y.Length ? Y[i] : 0;
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

            long carry = 0;

            for (int i = 0; i < r.Length; i++)
            {
                long x = i < X.Length ? X[i] : 0;
                long y = i < Y.Length ? Y[i] : 0;
                var tmpSub = x - y - carry;
                r[i] = (uint)(tmpSub & uint.MaxValue);
                carry = ((tmpSub >= 0) ? 0 : 1);
            }

            return r;
        }

        private static uint[] Multiply(uint[] X, uint[] Y)
        {
            uint[] output = new uint[X.Length + Y.Length + 1];

            for (int i = 0; i < X.Length; i++)
            {
                if (X[i] == 0)
                    continue;

                ulong carry = 0uL;
                int k = i;

                for (int j = 0; j < Y.Length; j++, k++)
                {
                    ulong tmp = (ulong)(X[i] * (long)Y[j] + output[k] + (long)carry);
                    output[k] = (uint) (tmp);
                    carry = tmp >> 32;
                }

                output[i + Y.Length] = (uint)carry;
            }

            return output;
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
                quot = Zero;
                rem = Zero;
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
            uint[] tmpQuotArray = new uint[numerator.dataLength - denominator.dataLength + 1];
            uint[] remArray = new uint[numerator.dataLength];
            int quotIter = 0;   //quotient array iterator index
            for (int i = 0; i < numerator.dataLength; i++)
            {
                remArray[i] = numerator._data[i];
            }

            ulong quickDen = denominator._data[0];  //quick denominator
            int remIter = remArray.Length - 1;  //remainder array iterator index
            ulong tmpRem = remArray[remIter];   //temporary remainder digit

            if (tmpRem >= quickDen)
            {
                ulong tmpQuot = tmpRem / quickDen;
                tmpQuotArray[quotIter++] = (uint)tmpQuot;
                remArray[remIter] = (uint)(tmpRem % quickDen);
            }

            remIter--;
            while (remIter >= 0)
            {
                tmpRem = ((ulong)remArray[remIter + 1] << 32) + remArray[remIter];
                ulong tmpQuot = tmpRem / quickDen;
                tmpQuotArray[quotIter++] = (uint)tmpQuot;
                remArray[remIter + 1] = 0u;
                remArray[remIter--] = (uint)(tmpRem % quickDen);
            }

            uint[] quotArray = new uint[quotIter];
            for(int i = quotArray.Length - 1, j = 0; i >= 0; i--, j++)
            {
                quotArray[j] = tmpQuotArray[i];
            }

            quotient = new LargeInteger(quotArray);
            remainder = new LargeInteger(remArray);
        }

        private static void MultiDigitDivMod(LargeInteger numerator, LargeInteger denominator, out LargeInteger quot, out LargeInteger rem)
        {
            uint[] quotArray = new uint[numerator.dataLength - denominator.dataLength + 1];
            uint[] remArray = new uint[numerator.dataLength + 1];

            uint tmp = 2147483648u;
            uint tmp2 = denominator._data[denominator.dataLength - 1];    //denominator most significant digit
            int shiftCount = 0;

            while (tmp != 0 && (tmp2 & tmp) == 0)
            {
                shiftCount++;
                tmp >>= 1;
            }
            for (int i = 0; i < numerator.dataLength; i++)
            {
                remArray[i] = numerator._data[i];
            }

            ShiftLeft(ref remArray, shiftCount);
            denominator <<= shiftCount;

            int j = numerator.dataLength - denominator.dataLength + 1;
            int remIter = numerator.dataLength; //yes, numerator, not remArray
            ulong denMsd = denominator._data[denominator.dataLength - 1];       //denominator most significant digit
            ulong denSubMsd = denominator._data[denominator.dataLength - 2];    //denominator second most significant digit
            int denSize = denominator.dataLength + 1;

            uint[] tmpRemSubArray = new uint[denSize];

            while (j > 0)
            {
                ulong quickDenominator = ((ulong)remArray[remIter] << 32) + remArray[remIter - 1];
                ulong tmpQuot = quickDenominator / denMsd;
                ulong tmpRem = quickDenominator % denMsd;
                bool flag = false;
                while (!flag)
                {
                    flag = true;
                    if (tmpQuot == 4294967296L || tmpQuot * denSubMsd > (tmpRem << 32) + remArray[remIter - 2])
                    {
                        tmpQuot--;
                        tmpRem += denMsd;
                        if (tmpRem < 4294967296L)
                        {
                            flag = false;
                        }
                    }
                }

                for (int k = 0; k < denSize; k++)
                {
                    tmpRemSubArray[(tmpRemSubArray.Length - 1) - k] = remArray[remIter - k];
                }

                var tmpRemBigInt = new LargeInteger(tmpRemSubArray);
                LargeInteger estimNumBigInt = denominator * (long)tmpQuot;  //current numerator estimate
                while (estimNumBigInt > tmpRemBigInt)
                {
                    tmpQuot--;
                    estimNumBigInt -= denominator;
                }
                LargeInteger estimRemBigInt = tmpRemBigInt - estimNumBigInt;    //current remainder estimate
                for (int k = 0; k < denSize; k++)
                {
                    tmp = denominator.dataLength - k < estimRemBigInt._data.Length
                        ? estimRemBigInt._data[denominator.dataLength - k]
                        : 0;
                    remArray[remIter - k] = tmp;
                }

                remIter--;
                j--;
                quotArray[j] = (uint)tmpQuot;
            }

            quot = new LargeInteger(quotArray);

            ShiftRight(ref remArray, shiftCount);

            rem = new LargeInteger(remArray);
        }

        public static LargeInteger operator >>(LargeInteger n, int bits)
        {
            var mult = Pow(2, bits);
            return n / mult;
        }

        private static void ShiftRight(ref uint[] buffer, int shiftVal)
        {
            int bitCount = 32;
            int shiftCount = 0;
            int length = buffer.Length;

            for (int i = shiftVal; i > 0; i -= bitCount)
            {
                if (i < bitCount)
                {
                    bitCount = i;
                    shiftCount = 32 - bitCount;
                }

                ulong carry = 0uL;
                for (int j = length - 1; j >= 0; j--)
                {
                    ulong tmp = (ulong)buffer[j] >> bitCount;
                    tmp |= carry;
                    carry = (((ulong)buffer[j] << shiftCount) & uint.MaxValue);
                    buffer[j] = (uint)tmp;
                }
            }
        }


        public static LargeInteger operator <<(LargeInteger n, int bits)
        {
            /*
            var mult = Pow(2, bits);
            return n * mult;
            */
            ShiftLeft(ref n._data, bits);

            return n;
        }

        private static void ShiftLeft(ref uint[] buffer, int shiftVal)
        {
            int bitCount = 32;
            int length = buffer.Length;

            int amountOfZeros = shiftVal / 32;  //amount of least significant digit zero padding we need
            int quickShiftAmount = shiftVal % 32;

            long msd = ((long)buffer[length - 1]) << quickShiftAmount;  //shifts the most significant digit
            bool needsExtra = msd != (uint) msd;    //if it goes above the uint range, we need to add
                                                    //a new position for the new MSD

            int newLength = buffer.Length + amountOfZeros + (needsExtra ? 1 : 0);
            uint[] newBuffer = new uint[newLength];

            uint lowerShifted = 0, upperShifted = 0;

            for (int i = 0, j = amountOfZeros; i < length; i++, j++)
            {
                ulong shiftedVal = ((ulong)buffer[i]) << quickShiftAmount;
                
                lowerShifted = (uint) shiftedVal;
                upperShifted = (uint)(shiftedVal >> 32);

                newBuffer[j] |= lowerShifted;

                if(upperShifted > 0)
                    newBuffer[j + 1] |= upperShifted;

                var debugString = newBuffer[j+1].ToString("X8") + newBuffer[j].ToString("X8");
            }

            buffer = newBuffer;

            /*
            Array.Copy(buffer, newBuffer, length);

            for (int i = shiftVal; i > 0; i -= bitCount)
            {
                if (i < bitCount)
                    bitCount = i;

                ulong num4 = 0uL;

                for (int j = 0; j < length; j++)
                {
                    ulong num5 = (ulong)newBuffer[j] << bitCount;
                    num5 |= num4;
                    newBuffer[j] = (uint)(num5 & uint.MaxValue);
                    num4 = num5 >> 32;
                }

                if (num4 != 0 && length + 1 <= newBuffer.Length)
                {
                    newBuffer[length] = (uint)num4;
                    length++;
                }
            }

            buffer = newBuffer;
            */
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

            if (a._sign > b._sign)
            {
                return !op;
            }

            if (a._data.Length < b._data.Length)
            {
                return op;
            }

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
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
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
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
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
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
                temp[i] = (byte)(A & B);
            }

            return new LargeInteger(temp);
        }

        public bool Equals(LargeInteger other)
        {
            if (other._data.Length != _data.Length)
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
            var val = One;
            var i = Zero;

            while (i < b)
            {
                val *= a;
                i = i + One;
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
                output = Zero;
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
                return temp._sign == _sign && temp._data.SequenceEqual(_data);
            }

            return false;
        }
    }
}