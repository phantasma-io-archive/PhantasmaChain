using System;
using System.Linq;
using System.Reflection;
using Phantasma.Core;
using static Phantasma.Numerics.BigInteger;

/*
 * Implementation of BigInteger class, written for Phantasma project
 * Author: Simão Pavlovich
 */

namespace Phantasma.Numerics
{
    public struct BigInteger : IEquatable<BigInteger>, IComparable<BigInteger>
    {
        private int _sign;
        private uint[] _data;
        private const int _Base = sizeof(uint) * 8;    //number of bits required for shift operations

        private static uint _MaxVal => (uint)Math.Pow(2, _Base) - 1;

        public static readonly BigInteger Zero = new BigInteger(0L);

        public static readonly BigInteger One = new BigInteger(1L);
        private int dataLength => _data.Length;

        public BigInteger(BigInteger other)
        {
            _sign = other._sign;
            _data = new uint[other._data.Length];
            Array.Copy(other._data, _data, _data.Length);
        }

        public BigInteger(uint[] bytes, int sign = 1)
        {
            _sign = sign;
            _data = null;

            InitFromArray(bytes);
        }

        public BigInteger(int val) : this((long)val)
        {
        }

        public BigInteger(uint val) : this((long)val)
        {
        }

        public BigInteger(byte[] bytes, bool twosComplementFormatFlag)
        {
            int sign;

            if (twosComplementFormatFlag)
            {
                var msb = bytes[bytes.Length - 1] >> 7;

                sign = msb == 0 ? 1 : -1;
            }
            else
                sign = 1;

            byte[] buffer;

            if (sign == -1)
                buffer = ApplyTwosComplement(bytes);
            else
                buffer = bytes;

            this = new BigInteger(buffer, sign);
        }

        public BigInteger(byte[] bytes, int sign = 1)
        {
            _sign = sign;
            _data = null;

            uint[] uintArray = new uint[(bytes.Length / 4) + (bytes.Length % 4 > 0 ? 1 : 0)];

            int bytePosition = 0;
            for (int i = 0, j = -1; i < bytes.Length; i++)
            {
                bytePosition = i % 4;

                if (bytePosition == 0)
                    j++;

                uintArray[j] |= (uint)(bytes[i] << (bytePosition * 8));
            }

            InitFromArray(uintArray);
        }

        public BigInteger(long val)
        {
            if (val == 0)
            {
                _sign = 0;
                _data = new uint[1];
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

        private void InitFromArray(uint[] digits)
        {
            int n = digits.Length;
            for (int i = n - 1; i >= 0; i--)
            {
                if (digits[i] == 0)
                {
                    n--;
                }
                else
                {
                    break;
                }
            }

            if (n <= 0)
            {
                _data = new uint[1];
                _sign = 0;
            }
            else
            {
                _data = new uint[n];
                Array.Copy(digits, _data, n);
            }
        }

        public BigInteger(string value, int radix)
        {
            value = value.ToUpper().Trim().Replace("\r","").Replace(" ","").Replace("\n","");

            var BigInteger = new BigInteger(0);
            var bi = new BigInteger(1L);

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

                BigInteger += bi * val;

                if (i - 1 >= limit)
                    bi *= radix;
            }

            _data = null;
            InitFromArray(BigInteger._data);
        }

        public static BigInteger FromHex(string p0)
        {
            return new BigInteger(p0, 16);
        }

        public int Sign()
        {
            return _sign;
        }

        public static explicit operator int(BigInteger value)
        {
            if (value._data.Length == 0)
                return 0;

            int result = (int)value._data[0];

            if (value._sign < 0)
                result *= -1;

            return result;
        }

        public static explicit operator long(BigInteger value)
        {
            long result = 0;

            result = value._data[0];

            if(value.dataLength > 1)
                result |= (long)((ulong)value._data[1] << 32);

            if (value._sign < 0)
                result *= -1;

            return result;
        }

        public static implicit operator BigInteger(int val)
        {
            return new BigInteger(val);
        }

        public static implicit operator BigInteger(long val)
        {
            return new BigInteger(val);
        }

        public static BigInteger Abs(BigInteger x)
        {
            return new BigInteger(x._data, 1);
        }

        public override string ToString()
        {
            return ToDecimal();
        }

        public string ToDecimal()
        {
            int radix = 10;
            Throw.If(radix < 2 || radix > 36, "Radix must be >= 2 and <= 36");

            string text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string text2 = "";

            BigInteger largeInteger = new BigInteger(this);
            largeInteger._sign = 1;

            bool flag = false;

            var largeInteger2 = new BigInteger();
            var largeInteger3 = new BigInteger();
            var bi = new BigInteger(radix);
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
                result += digit.ToString("x8");
            }

            result = result.TrimStart('0');

            return result;
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

        public static BigInteger operator +(BigInteger a, BigInteger b)
        {
            BigInteger result;

            //all these if-else's are to make sure we don't attempt operations that would give a negative result,
            //allowing the large int operations to deal only in the scope of unsigned numbers
            if (a._sign < 0 && b._sign < 0)
            {
                result = new BigInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : -1;
            }
            else
            if (a._sign < 0)
            {
                if (Abs(a) < b)
                {
                    result = new BigInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : 1;
                }
                else
                {
                    result = new BigInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : -1;
                }
            }
            else if (b._sign < 0)
            {
                if (a < Abs(b))
                {
                    result = new BigInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : -1;
                }
                else
                {
                    result = new BigInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : 1;
                }
            }
            else
            {
                result = new BigInteger(Add(b._data, a._data));
                result._sign = result == 0 ? 0 : 1;
            }

            return result;
        }

        public static BigInteger operator -(BigInteger a, BigInteger b)
        {
            BigInteger result;

            //all these if-else's are to make sure we don't attempt operations that would give a negative result,
            //allowing the large int operations to deal only in the scope of unsigned numbers
            if (a._sign < 0 && b._sign < 0)
            {
                if (Abs(a) < Abs(b))
                {
                    result = new BigInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : 1;
                }
                else
                {
                    result = new BigInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : -1;
                }
            }
            else
            if (a._sign < 0)
            {
                result = new BigInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : -1;
            }
            else if (b._sign < 0)
            {
                result = new BigInteger(Add(a._data, b._data));
                result._sign = result == 0 ? 0 : 1;
            }
            else
            {
                if (a < b)
                {
                    result = new BigInteger(Subtract(b._data, a._data));
                    result._sign = result == 0 ? 0 : -1;
                }
                else
                {
                    result = new BigInteger(Subtract(a._data, b._data));
                    result._sign = result == 0 ? 0 : 1;
                }
            }

            return result;
        }

        public static BigInteger operator *(BigInteger a, BigInteger b)
        {
            var result = new BigInteger(Multiply(a._data, b._data))
            {
                _sign = a._sign * b._sign
            };
            return result;
        }

        public static BigInteger operator /(BigInteger a, BigInteger b)
        {
            BigInteger quot, rem;
            DivideAndModulus(Abs(a), Abs(b), out quot, out rem);
            quot._sign = quot._sign == 0 ? 0 : a._sign * b._sign;
            return quot;
        }

        public static BigInteger operator %(BigInteger a, BigInteger b)
        {
            BigInteger quot, rem;
            DivideAndModulus(a, b, out quot, out rem);
            return rem;
        }

        public static void DivideAndModulus(BigInteger a, BigInteger b, out BigInteger quot, out BigInteger rem)
        {
            if (b == 0)
            {
                quot = Zero;
                rem = Zero;
                return;
            }

            if (a._data.Length < b._data.Length)
            {
                quot = new BigInteger(0);
                rem = new BigInteger(a);
                return;
            }

            if (b._data.Length == 1)
                SingleDigitDivMod(a, b, out quot, out rem);
            else
                MultiDigitDivMod(a, b, out quot, out rem);

            
            rem._sign = a._sign;
            rem = a >= 0 ? rem : b + rem;

            quot._sign = quot.GetBitLength() == 0 ? 0 : a._sign * b._sign;
            rem._sign = rem.GetBitLength() == 0 ? 0 : rem._sign;
        }

        //do not access this function directly under any circumstances, always go through DivideAndModulus
        private static void SingleDigitDivMod(BigInteger numerator, BigInteger denominator, out BigInteger quotient, out BigInteger remainder)
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

            quotient = new BigInteger(quotArray);
            remainder = new BigInteger(remArray);
        }

        //do not access this function directly under any circumstances, always go through DivideAndModulus
        private static void MultiDigitDivMod(BigInteger numerator, BigInteger denominator, out BigInteger quot, out BigInteger rem)
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

                var tmpRemBigInt = new BigInteger(tmpRemSubArray);
                BigInteger estimNumBigInt = denominator * (long)tmpQuot;  //current numerator estimate
                while (estimNumBigInt > tmpRemBigInt)
                {
                    tmpQuot--;
                    estimNumBigInt -= denominator;
                }
                BigInteger estimRemBigInt = tmpRemBigInt - estimNumBigInt;    //current remainder estimate
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

            quot = new BigInteger(quotArray);

            ShiftRight(ref remArray, shiftCount);

            rem = new BigInteger(remArray);
        }

        public static BigInteger operator >>(BigInteger n, int bits)
        {
            bits = bits < 0 ? -bits : bits;
            ShiftRight(ref n._data, bits);

            if (n._data[0] == 0 && n.dataLength == 1)
                n._sign = 0;

            return n;
        }

        private static void ShiftRight(ref uint[] buffer, int shiftBitCount)
        {
            int length = buffer.Length;

            int shrinkage = shiftBitCount / 32;  //amount of digits we need to cut from the buffer

            int quickShiftAmount = shiftBitCount % 32;


            uint msd = buffer[length - 1] >> quickShiftAmount;  //shifts the most significant digit
            int extraShrinkage = (msd == 0) ? 1 : 0;    //if that shift goes to 0, it means we need to cut
                                                        //an extra position of the array to account for an MSD == 0

            int newLength = buffer.Length - shrinkage - extraShrinkage;

            if(newLength <= 0)
            {
                buffer = new uint[1];
                return;
            }

            uint[] newBuffer = new uint[newLength];

            quickShiftAmount = 32 - quickShiftAmount;   //we'll use this new shift amount to pre-left shift the applicable digits
                                                        //so we have easy access to the potential underflow of any given digit's right shift operation

            if (extraShrinkage == 1)
                newBuffer[newLength - 1] = buffer[length - 1] << quickShiftAmount;

            for (int i = length - (1 + extraShrinkage) , j = newLength - 1; j >= 1; i--, j--)
            {
                ulong upshiftedVal = (ulong) buffer[i] << quickShiftAmount;

                uint shiftMsd = (uint)(upshiftedVal >> 32);
                uint shiftLsd = (uint) upshiftedVal;

                newBuffer[j] |= shiftMsd;
                newBuffer[j - 1] |= shiftLsd;
            }

            newBuffer[0] |= buffer[shrinkage] >> shiftBitCount;

            buffer = newBuffer;
        }


        public static BigInteger operator <<(BigInteger n, int bits)
        {
            bits = bits < 0 ? -bits : bits;
            ShiftLeft(ref n._data, bits);
            return n;
        }

        private static void ShiftLeft(ref uint[] buffer, int shiftBitCount)
        {
            int length = buffer.Length;

            int amountOfZeros = shiftBitCount / 32;  //amount of least significant digit zero padding we need
            int quickShiftAmount = shiftBitCount % 32;

            long msd = ((long)buffer[length - 1]) << quickShiftAmount;  //shifts the most significant digit

            int extraDigit = (msd != (uint) msd) ? 1 : 0;  //if it goes above the uint range, we need to add
                                                            //a new position for the new MSD

            int newLength = buffer.Length + amountOfZeros + extraDigit;
            uint[] newBuffer = new uint[newLength];

            for (int i = 0, j = amountOfZeros; i < length; i++, j++)
            {
                ulong shiftedVal = ((ulong)buffer[i]) << quickShiftAmount;
                
                var shiftLsd = (uint) shiftedVal;
                var shiftMsd = (uint)(shiftedVal >> 32);

                newBuffer[j] |= shiftLsd;

                if(shiftMsd > 0)
                    newBuffer[j + 1] |= shiftMsd;
            }

            buffer = newBuffer;
        }

        public static BigInteger operator ++(BigInteger n)
        {
            return n + 1;
        }

        public static BigInteger operator --(BigInteger n)
        {
            return n - 1;
        }

        public static BigInteger operator -(BigInteger n)
        {
            n._sign = -n._sign;
            return n;
        }

        public static bool operator ==(BigInteger a, BigInteger b)
        {
            return a._data.Length == b._data.Length && a._sign == b._sign && a._data.SequenceEqual(b._data);
        }

        public static bool operator !=(BigInteger a, BigInteger b)
        {
            return a._data.Length != b._data.Length || a._sign != b._sign || !a._data.SequenceEqual(b._data);
        }

        private static bool LogicalCompare(BigInteger a, BigInteger b, bool op)
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

        public static bool operator <(BigInteger a, BigInteger b)
        {
            return LogicalCompare(a, b, true);
        }

        public static bool operator >(BigInteger a, BigInteger b)
        {
            return LogicalCompare(a, b, false);
        }

        public static bool operator <=(BigInteger a, BigInteger b)
        {
            return (a == b || a < b);
        }

        public static bool operator >=(BigInteger a, BigInteger b)
        {
            return (a == b || a > b);
        }

        public static BigInteger operator ^(BigInteger a, BigInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
                temp[i] = (A ^ B);
            }

            return new BigInteger(temp);
        }

        public static BigInteger operator |(BigInteger a, BigInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
                temp[i] = A | B;
            }

            return new BigInteger(temp);
        }

        public static BigInteger operator ~(BigInteger a)
        {
            var buffer = new uint[a._data.Length];

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = ~a._data[i];
            }

            return new BigInteger(buffer);
        }

        public static BigInteger operator &(BigInteger a, BigInteger b)
        {
            var len = a._data.Length > b._data.Length ? a._data.Length : b._data.Length;
            var temp = new uint[len];


            for (int i = 0; i < len; i++)
            {
                uint A = i < a._data.Length ? a._data[i] : 0;
                uint B = i < b._data.Length ? b._data[i] : 0;
                temp[i] = A & B;
            }

            return new BigInteger(temp);
        }

        public bool Equals(BigInteger other)
        {
            if (other._data.Length != _data.Length)
            {
                return false;
            }

            return _data.SequenceEqual(other._data);
        }

        public int CompareTo(BigInteger other)
        {
            if (other.Equals(this))
            {
                return 0;
            }

            if (this < other)
            {
                return -1;
            }

            return 1;
        }

        public static BigInteger Pow(BigInteger a, BigInteger b)
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

        public static BigInteger ModPow(BigInteger b, BigInteger exp, BigInteger mod)
        {
            return b.ModPow(exp, mod);
        }

        /// <summary>
        /// Modulo Exponentiation
        /// Ported from http://developer.classpath.org/doc/java/math/BigInteger-source.html
        /// </summary>
        /// <param name="exp">Exponential</param>
        /// <param name="mod">Modulo</param>
        /// <returns>BigInteger result of raising this to the power of exp and then modulo n </returns>
        public BigInteger ModPow(BigInteger exp, BigInteger mod)
        {
            Throw.If(mod._sign == -1 || mod == 0, "Non-positive modulo");

            if (exp._sign < 0)
                return ModInverse(mod).ModPow(-exp, mod);

            if (exp == 1)
                return this % mod;

            BigInteger s = new BigInteger(1);
            BigInteger t = new BigInteger(this);

            while (exp != Zero)
            {
                if ((exp & One) == One)
                    s = (s * t) % mod;

                exp = exp >> 1;
                t = (t * t) % mod;
            }

            return s;
        }

        public BigInteger ModInverse(BigInteger modulus)
        {
            BigInteger[] array = new BigInteger[2]
            {
            0,
            1
            };
            BigInteger[] array2 = new BigInteger[2];
            BigInteger[] array3 = new BigInteger[2]
            {
            0,
            0
            };
            int num = 0;
            BigInteger bi = modulus;
            BigInteger bigInteger = this;
            while (bigInteger.dataLength > 1 || (bigInteger.dataLength == 1 && bigInteger._data[0] != 0))
            {
                BigInteger bigInteger2 = new BigInteger();
                BigInteger bigInteger3 = new BigInteger();
                if (num > 1)
                {
                    BigInteger bigInteger4 = (array[0] - array[1] * array2[0]) % modulus;
                    array[0] = array[1];
                    array[1] = bigInteger4;
                }
                
                DivideAndModulus(bi, bigInteger, out bigInteger2, out bigInteger3);
                
                
                array2[0] = array2[1];
                array3[0] = array3[1];
                array2[1] = bigInteger2;
                array3[1] = bigInteger3;
                bi = bigInteger;
                bigInteger = bigInteger3;
                num++;
            }

            Throw.If(array3[0].dataLength > 1 || (array3[0].dataLength == 1 && array3[0]._data[0] != 1), "No inverse!");

            BigInteger bigInteger5 = (array[0] - array[1] * array2[0]) % modulus;
            if (bigInteger5._sign < 0)
            {
                bigInteger5 += modulus;
            }
            return bigInteger5;
        }

        public bool TestBit(int index)
        {
            return (this & (One << index)) > Zero;
        }

        public int GetLowestSetBit()
        {
            if (this.Sign() == 0)
                return -1;

            byte[] b = this.ToByteArray();
            int w = 0;
            while (b[w] == 0)
                w++;
            for (int x = 0; x < 8; x++)
                if ((b[w] & 1 << x) > 0)
                    return x + w * 8;
            throw new Exception();
        }

        public static BigInteger Parse(string input, int radix = 10)
        {
            return new BigInteger(input, radix);
        }

        public static bool TryParse(string input, out BigInteger output)
        {
            try
            {
                output = new BigInteger(input, 10);
                return true;
            }
            catch
            {
                output = Zero;
                return false;
            }
        }

        public int GetBitLength()
        {
            if (Object.Equals(this, null) || (_data.Length == 1 && _data[0] == 0) || _data.Length == 0)
                return 0;

            var result = (_data.Length - 1) * 32;

            result += (int) Math.Log(_data[_data.Length - 1], 2) + 1;

            return result;
        }

        public uint[] ToUintArray()
        {
            return (uint[])_data.Clone();
        }


        /*
public void SetBit(uint bitNum)
        {
            uint num = bitNum >> 5;
            byte b = (byte)(bitNum & 0x1F);
            uint num2 = (uint)(1 << (int)b);
            _data[num] |= num2;
            if (num >= dataLength)
            {
                dataLength = (int)(num + 1);
            }
        }

        public void UnsetBit(uint bitNum)
        {
            uint num = bitNum >> 5;
            if (num < dataLength)
            {
                byte b = (byte)(bitNum & 0x1F);
                uint num2 = (uint)(1 << (int)b);
                uint num3 = (uint)(-1 ^ (int)num2);
                _data[num] &= num3;
                if (dataLength > 1 && _data[dataLength - 1] == 0)
                {
                    dataLength--;
                }
            }
        }
        */

        public bool IsEven => CalcIsEven();

        public bool CalcIsEven()
        {
            var tmp = this % 2;
            var tmp2 = tmp == 0;
            return tmp2;
        }

        public BigInteger Sqrt()
        {
            Throw.If(this < 0, "cannot be negative");

            if (this == 0)
            {
                return 0;
            }

            uint bitLength = (uint)GetBitLength();

            bitLength = (((bitLength & 1) == 0) ? (bitLength >> 1) : ((bitLength >> 1) + 1));
            uint num2 = bitLength >> 5;
            byte b = (byte)(bitLength & 0x1F);
            
            uint num3;

            if (b == 0)
            {
                num3 = 2147483648u;
            }
            else
            {
                num3 = (uint)(1 << (int)b);
                num2++;
            }

            var sqrtArray = new uint[(int) num2];
            for (int num4 = (int)(num2 - 1); num4 >= 0; num4--)
            {
                while (num3 != 0)
                {
                    sqrtArray[num4] ^= num3;
                    var tmp = new BigInteger(sqrtArray);
                    if (tmp * tmp > this)
                    {
                        sqrtArray[num4] ^= num3;
                    }
                    num3 >>= 1;
                }
                num3 = 2147483648u;
            }
            return new BigInteger(sqrtArray);
        }


        public byte[] ToByteArray(bool includeSignInArray = false)
        {
            var bitLength = GetBitLength();
            var byteArraySize = (bitLength / 8) + (uint)((bitLength % 8 > 0) ? 1 : 0) + (includeSignInArray ? 1 : 0);
            byte[] result = new byte[byteArraySize];

            bool applyTwosComplement = includeSignInArray && (_sign == -1);    //only apply two's complement if this number is negative

            for (int i = 0, j = 0; i < _data.Length; i++, j += 4)
            {
                byte[] bytes = BitConverter.GetBytes(_data[i]);
                for (int k = 0; k < 4; k++)
                {
                    if (!applyTwosComplement && bytes[k] == 0)
                        continue;
                    if (applyTwosComplement && j + k >= byteArraySize)
                        continue;

                    if(applyTwosComplement)
                        result[j + k] = (byte) (bytes[k] ^ 0xFF);
                    else
                        result[j + k] = bytes[k];
                }
            }

            //this could be optimized if needed, but likely not worth it for now
            if (applyTwosComplement)
            {
                
                var tmp = (new BigInteger(result, sign: 1) + 1); //create a biginteger with the inverted bits but with positive sign, and add 1.

                result = tmp.ToByteArray(true);     //when we call the ToByteArray asking to include sign, we will get an extra byte on the array to keep sign information while in byte[] format
                                                    //but the twos complement logic won't get applied again given the bigint has positive sign.

                result[result.Length - 1] = 0xFF;      //force the MSB to 1's, as this array represents a negative number.
            }

            return result;
        }

        public static byte[] ApplyTwosComplement(byte[] bytes)
        {
            var buffer = new byte[bytes.Length];

            for (int i = 0; i < bytes.Length; i++)
            {
                buffer[i] = (byte)~bytes[i];
            }

            var tmp = (new BigInteger(buffer, sign: 1) + 1); //create a biginteger with the inverted bits but with positive sign, and add 1. result will remain with positive sign
            
            buffer = tmp.ToByteArray(true); //when we call the ToByteArray asking to include sign, we will get an extra byte on the array to make sure sign is correct 
            //but the twos complement logic won't get applied again given the bigint has positive sign.

            return buffer;
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
            if (obj is BigInteger temp)
            {
                return temp._sign == _sign && temp._data.SequenceEqual(this._data);
            }

            return false;
        }

        public BigInteger Mod(BigInteger b)
        {
            return this % b;
        }

        public BigInteger FlipBit(int bit)
        {
            return this ^ (One << bit);
        }
    }
}
