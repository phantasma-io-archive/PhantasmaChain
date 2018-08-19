using Phantasma.Utils;
using System;

/*
 * Implementation of BigInteger class, byte-compatible with System.Numerics (which we don't use due to lack of compatibility with Bridge.NET)
 * Based on the work of Bazzilic and Chan Keong TAN
 * https://github.com/bazzilic/BigInteger
 */
namespace Phantasma.Mathematics
{
    public class BigInteger
    {
        private const int maxLength = 70;

        public static readonly BigInteger Zero = new BigInteger(0L);

        public static readonly BigInteger One = new BigInteger(1L);

        private uint[] _data;

        public int dataLength;

        public BigInteger()
        {
            _data = new uint[70];
            dataLength = 1;
        }

        public BigInteger(long value)
        {
            _data = new uint[70];
            long num = value;
            dataLength = 0;
            while (value != 0L && dataLength < 70)
            {
                _data[dataLength] = (uint)(value & uint.MaxValue);
                value >>= 32;
                dataLength++;
            }

            if (num > 0)
            {
                Throw.If(value != 0L || ((int)_data[69] & -2147483648) != 0, "Positive overflow in constructor.");
            }
            else
            if (num < 0)
            {
                Throw.If(value != -1 || ((int)_data[dataLength - 1] & -2147483648) == 0, "Negative underflow in constructor.");
            }

            if (dataLength == 0)
            {
                dataLength = 1;
            }
        }

        public BigInteger(ulong value)
        {
            _data = new uint[70];
            dataLength = 0;
            while (value != 0L && dataLength < 70)
            {
                _data[dataLength] = (uint)(value & uint.MaxValue);
                value >>= 32;
                dataLength++;
            }

            Throw.If(value != 0L || ((int)_data[69] & -2147483648) != 0, "Positive overflow in constructor.");

            if (dataLength == 0)
            {
                dataLength = 1;
            }
        }

        public BigInteger(BigInteger bi)
        {
            _data = new uint[70];
            dataLength = bi.dataLength;
            for (int i = 0; i < dataLength; i++)
            {
                _data[i] = bi._data[i];
            }
        }

        public BigInteger(string value, int radix)
        {
            var bi = new BigInteger(1L);
            var bigInteger = new BigInteger();
            value = value.ToUpper().Trim();

            int num = 0;
            if (value[0] == '-')
            {
                num = 1;
            }

            for (int num2 = value.Length - 1; num2 >= num; num2--)
            {
                int num3 = value[num2];
                num3 = ((num3 >= 48 && num3 <= 57) ? (num3 - 48) : ((num3 < 65 || num3 > 90) ? 9999999 : (num3 - 65 + 10)));
                Throw.If(num3 >= radix, "Invalid string in constructor.");

                if (value[0] == '-')
                {
                    num3 = -num3;
                }

                bigInteger += bi * num3;

                if (num2 - 1 >= num)
                {
                    bi *= radix;
                }
            }

            if (value[0] == '-')
            {
                Throw.If(((int)bigInteger._data[69] & -2147483648) == 0, "Negative underflow in constructor.");
            }
            else
            {
                Throw.If(((int)bigInteger._data[69] & -2147483648) != 0, "Positive overflow in constructor.");
            }

            _data = new uint[70];
            for (int i = 0; i < bigInteger.dataLength; i++)
            {
                _data[i] = bigInteger._data[i];
            }
            dataLength = bigInteger.dataLength;
        }

        public BigInteger(byte[] inData)
        {
            int num = inData.Length;
            Array.Reverse(inData);
            dataLength = num >> 2;
            int num2 = num & 3;
            if (num2 != 0)
            {
                dataLength++;
            }

            Throw.If(dataLength > 70, "Byte overflow in constructor.");
            _data = new uint[70];
            int num3 = 0;
            for (int num4 = num - 1; num4 >= 3; num4 -= 4)
            {
                _data[num3++] = (uint)((inData[num4 - 3] << 24) + (inData[num4 - 2] << 16) + (inData[num4 - 1] << 8) + inData[num4]);
            }

            switch (num2)
            {
                case 1:
                    _data[num3] = inData[0];
                    break;
                case 2:
                    _data[num3] = (uint)((inData[0] << 8) + inData[1]);
                    break;
                case 3:
                    _data[num3] = (uint)((inData[0] << 16) + (inData[1] << 8) + inData[2]);
                    break;
            }

            if (dataLength == 0)
            {
                dataLength = 1;
            }

            while (dataLength > 1 && _data[dataLength - 1] == 0)
            {
                dataLength--;
            }
        }

        public BigInteger(uint[] inData)
        {
            dataLength = inData.Length;
            Throw.If(dataLength > 70, "Byte overflow in constructor.");

            _data = new uint[70];
            int num = dataLength - 1;
            int num2 = 0;

            while (num >= 0)
            {
                _data[num2] = inData[num];
                num--;
                num2++;
            }

            while (dataLength > 1 && _data[dataLength - 1] == 0)
            {
                dataLength--;
            }
        }

        public static implicit operator BigInteger(long value)
        {
            return new BigInteger(value);
        }

        public static implicit operator BigInteger(ulong value)
        {
            return new BigInteger(value);
        }

        public static implicit operator BigInteger(int value)
        {
            return new BigInteger(value);
        }

        public static implicit operator BigInteger(uint value)
        {
            return new BigInteger((ulong)value);
        }

        public static BigInteger operator +(BigInteger bi1, BigInteger bi2)
        {
            var bigInteger = new BigInteger
            {
                dataLength = ((bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength)
            };

            long num = 0L;
            for (int i = 0; i < bigInteger.dataLength; i++)
            {
                long num2 = (long)bi1._data[i] + (long)bi2._data[i] + num;
                num = num2 >> 32;
                bigInteger._data[i] = (uint)(num2 & uint.MaxValue);
            }

            if (num != 0L && bigInteger.dataLength < 70)
            {
                bigInteger._data[bigInteger.dataLength] = (uint)num;
                bigInteger.dataLength++;
            }

            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }

            int num3 = 69;
            if (((int)bi1._data[num3] & -2147483648) == ((int)bi2._data[num3] & -2147483648) && ((int)bigInteger._data[num3] & -2147483648) != ((int)bi1._data[num3] & -2147483648))
            {
                throw new ArithmeticException();
            }

            return bigInteger;
        }

        public static BigInteger operator ++(BigInteger bi1)
        {
            BigInteger bigInteger = new BigInteger(bi1);
            long num = 1L;
            int num2 = 0;
            while (num != 0L && num2 < 70)
            {
                long num3 = bigInteger._data[num2];
                num3++;
                bigInteger._data[num2] = (uint)(num3 & uint.MaxValue);
                num = num3 >> 32;
                num2++;
            }
            if (num2 <= bigInteger.dataLength)
            {
                while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
                {
                    bigInteger.dataLength--;
                }
            }
            else
            {
                bigInteger.dataLength = num2;
            }
            int num4 = 69;


            Throw.If(((int)bi1._data[num4] & -2147483648) == 0 && ((int)bigInteger._data[num4] & -2147483648) != ((int)bi1._data[num4] & -2147483648), "Overflow in increment.");

            return bigInteger;
        }

        public static BigInteger operator -(BigInteger bi1, BigInteger bi2)
        {
            var bigInteger = new BigInteger
            {
                dataLength = ((bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength)
            };

            long num = 0L;

            for (int i = 0; i < bigInteger.dataLength; i++)
            {
                long num2 = (long)bi1._data[i] - (long)bi2._data[i] - num;
                bigInteger._data[i] = (uint)(num2 & uint.MaxValue);
                num = ((num2 >= 0) ? 0 : 1);
            }

            if (num != 0)
            {
                for (int j = bigInteger.dataLength; j < 70; j++)
                {
                    bigInteger._data[j] = uint.MaxValue;
                }
                bigInteger.dataLength = 70;
            }

            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }
            int num3 = 69;

            Throw.If(((int)bi1._data[num3] & -2147483648) != ((int)bi2._data[num3] & -2147483648) && ((int)bigInteger._data[num3] & -2147483648) != ((int)bi1._data[num3] & -2147483648), "overflow in subtraction");

            return bigInteger;
        }

        public static BigInteger operator --(BigInteger bi1)
        {
            var bigInteger = new BigInteger(bi1);
            bool flag = true;
            int num = 0;

            while (flag && num < 70)
            {
                long num2 = bigInteger._data[num];
                num2--;
                bigInteger._data[num] = (uint)(num2 & uint.MaxValue);
                if (num2 >= 0)
                {
                    flag = false;
                }
                num++;
            }

            if (num > bigInteger.dataLength)
            {
                bigInteger.dataLength = num;
            }

            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }

            int num3 = 69;
            Throw.If(((int)bi1._data[num3] & -2147483648) != 0 && ((int)bigInteger._data[num3] & -2147483648) != ((int)bi1._data[num3] & -2147483648), "Underflow in decrement.");

            return bigInteger;
        }

        public static BigInteger operator *(BigInteger bi1, BigInteger bi2)
        {
            int num = 69;
            bool flag = false;
            bool flag2 = false;
            try
            {
                if (((int)bi1._data[num] & -2147483648) != 0)
                {
                    flag = true;
                    bi1 = -bi1;
                }
                if (((int)bi2._data[num] & -2147483648) != 0)
                {
                    flag2 = true;
                    bi2 = -bi2;
                }
            }
            catch (Exception)
            {
            }

            var bigInteger = new BigInteger();
            try
            {
                for (int i = 0; i < bi1.dataLength; i++)
                {
                    if (bi1._data[i] != 0)
                    {
                        ulong num2 = 0uL;
                        int num3 = 0;
                        int num4 = i;
                        while (num3 < bi2.dataLength)
                        {
                            ulong num5 = (ulong)((long)bi1._data[i] * (long)bi2._data[num3] + bigInteger._data[num4] + (long)num2);
                            bigInteger._data[num4] = (uint)(num5 & uint.MaxValue);
                            num2 = num5 >> 32;
                            num3++;
                            num4++;
                        }
                        if (num2 != 0)
                        {
                            bigInteger._data[i + bi2.dataLength] = (uint)num2;
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw new ArithmeticException("Multiplication overflow.");
            }

            bigInteger.dataLength = bi1.dataLength + bi2.dataLength;
            if (bigInteger.dataLength > 70)
            {
                bigInteger.dataLength = 70;
            }

            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }

            if (((int)bigInteger._data[num] & -2147483648) != 0)
            {
                if (flag != flag2 && bigInteger._data[num] == 2147483648u)
                {
                    if (bigInteger.dataLength == 1)
                    {
                        return bigInteger;
                    }
                    bool flag3 = true;
                    for (int j = 0; j < bigInteger.dataLength - 1; j++)
                    {
                        if (!flag3)
                        {
                            break;
                        }
                        if (bigInteger._data[j] != 0)
                        {
                            flag3 = false;
                        }
                    }
                    if (flag3)
                    {
                        return bigInteger;
                    }
                }

                throw new ArithmeticException("Multiplication overflow.");
            }

            if (flag != flag2)
            {
                return -bigInteger;
            }
            return bigInteger;
        }

        public static BigInteger operator <<(BigInteger bi1, int shiftVal)
        {
            var bigInteger = new BigInteger(bi1);
            bigInteger.dataLength = ShiftLeft(bigInteger._data, shiftVal);
            return bigInteger;
        }

        private static int ShiftLeft(uint[] buffer, int shiftVal)
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

            return num2;
        }

        public static BigInteger operator >>(BigInteger bi1, int shiftVal)
        {
            var bigInteger = new BigInteger(bi1);
            bigInteger.dataLength = ShiftRight(bigInteger._data, shiftVal);
            if (((int)bi1._data[69] & -2147483648) != 0)
            {
                for (int num = 69; num >= bigInteger.dataLength; num--)
                {
                    bigInteger._data[num] = uint.MaxValue;
                }
                uint num2 = 2147483648u;
                for (int i = 0; i < 32; i++)
                {
                    if ((bigInteger._data[bigInteger.dataLength - 1] & num2) != 0)
                    {
                        break;
                    }
                    bigInteger._data[bigInteger.dataLength - 1] |= num2;
                    num2 >>= 1;
                }
                bigInteger.dataLength = 70;
            }
            return bigInteger;
        }

        private static int ShiftRight(uint[] buffer, int shiftVal)
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
            return num3;
        }

        public static BigInteger operator ~(BigInteger bi1)
        {
            var bigInteger = new BigInteger(bi1);
            for (int i = 0; i < 70; i++)
            {
                bigInteger._data[i] = ~bi1._data[i];
            }

            bigInteger.dataLength = 70;
            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }

            return bigInteger;
        }

        public static BigInteger operator -(BigInteger bi1)
        {
            if (bi1.dataLength == 1 && bi1._data[0] == 0)
            {
                return new BigInteger();
            }

            BigInteger bigInteger = new BigInteger(bi1);
            for (int i = 0; i < 70; i++)
            {
                bigInteger._data[i] = ~bi1._data[i];
            }

            long num = 1L;
            int num2 = 0;
            while (num != 0L && num2 < 70)
            {
                long num3 = bigInteger._data[num2];
                num3++;
                bigInteger._data[num2] = (uint)(num3 & uint.MaxValue);
                num = num3 >> 32;
                num2++;
            }

            Throw.If(((int)bi1._data[69] & -2147483648) == ((int)bigInteger._data[69] & -2147483648), "Overflow in negation.");

            bigInteger.dataLength = 70;
            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }

            return bigInteger;
        }

        public static bool operator ==(BigInteger bi1, BigInteger bi2)
        {
            return bi1.Equals(bi2);
        }

        public static bool operator !=(BigInteger bi1, BigInteger bi2)
        {
            return !bi1.Equals(bi2);
        }

        public override bool Equals(object o)
        {
            var bigInteger = (BigInteger)o;
            if (dataLength != bigInteger.dataLength)
            {
                return false;
            }
            for (int i = 0; i < dataLength; i++)
            {
                if (_data[i] != bigInteger._data[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return _data.GetHashCode();
        }

        public static bool operator >(BigInteger bi1, BigInteger bi2)
        {
            int num = 69;
            if (((int)bi1._data[num] & -2147483648) != 0 && ((int)bi2._data[num] & -2147483648) == 0)
            {
                return false;
            }

            if (((int)bi1._data[num] & -2147483648) == 0 && ((int)bi2._data[num] & -2147483648) != 0)
            {
                return true;
            }

            int num2 = (bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength;
            num = num2 - 1;
            while (num >= 0 && bi1._data[num] == bi2._data[num])
            {
                num--;
            }

            if (num >= 0)
            {
                if (bi1._data[num] > bi2._data[num])
                {
                    return true;
                }
                return false;
            }
            return false;
        }

        public static bool operator <(BigInteger bi1, BigInteger bi2)
        {
            int num = 69;

            if (((int)bi1._data[num] & -2147483648) != 0 && ((int)bi2._data[num] & -2147483648) == 0)
            {
                return true;
            }

            if (((int)bi1._data[num] & -2147483648) == 0 && ((int)bi2._data[num] & -2147483648) != 0)
            {
                return false;
            }

            int num2 = (bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength;
            num = num2 - 1;
            while (num >= 0 && bi1._data[num] == bi2._data[num])
            {
                num--;
            }

            if (num >= 0)
            {
                if (bi1._data[num] < bi2._data[num])
                {
                    return true;
                }
                return false;
            }

            return false;
        }

        public static bool operator >=(BigInteger bi1, BigInteger bi2)
        {
            return bi1 == bi2 || bi1 > bi2;
        }

        public static bool operator <=(BigInteger bi1, BigInteger bi2)
        {
            return bi1 == bi2 || bi1 < bi2;
        }

        private static void MultiByteDivide(BigInteger bi1, BigInteger bi2, BigInteger outQuotient, BigInteger outRemainder)
        {
            uint[] array = new uint[70];
            int num = bi1.dataLength + 1;
            uint[] array2 = new uint[num];
            uint num2 = 2147483648u;
            uint num3 = bi2._data[bi2.dataLength - 1];
            int num4 = 0;
            int num5 = 0;
            while (num2 != 0 && (num3 & num2) == 0)
            {
                num4++;
                num2 >>= 1;
            }
            for (int i = 0; i < bi1.dataLength; i++)
            {
                array2[i] = bi1._data[i];
            }
            ShiftLeft(array2, num4);
            bi2 <<= num4;
            int num6 = num - bi2.dataLength;
            int num7 = num - 1;
            ulong num8 = bi2._data[bi2.dataLength - 1];
            ulong num9 = bi2._data[bi2.dataLength - 2];
            int num10 = bi2.dataLength + 1;
            uint[] array3 = new uint[num10];
            while (num6 > 0)
            {
                ulong num11 = ((ulong)array2[num7] << 32) + array2[num7 - 1];
                ulong num12 = num11 / num8;
                ulong num13 = num11 % num8;
                bool flag = false;
                while (!flag)
                {
                    flag = true;
                    if (num12 == 4294967296L || num12 * num9 > (num13 << 32) + array2[num7 - 2])
                    {
                        num12--;
                        num13 += num8;
                        if (num13 < 4294967296L)
                        {
                            flag = false;
                        }
                    }
                }
                for (int j = 0; j < num10; j++)
                {
                    array3[j] = array2[num7 - j];
                }

                var bigInteger = new BigInteger(array3);
                BigInteger bigInteger2 = bi2 * (long)num12;
                while (bigInteger2 > bigInteger)
                {
                    num12--;
                    bigInteger2 -= bi2;
                }
                BigInteger bigInteger3 = bigInteger - bigInteger2;
                for (int k = 0; k < num10; k++)
                {
                    array2[num7 - k] = bigInteger3._data[bi2.dataLength - k];
                }
                array[num5++] = (uint)num12;
                num7--;
                num6--;
            }
            outQuotient.dataLength = num5;
            int l = 0;
            int num15 = outQuotient.dataLength - 1;
            while (num15 >= 0)
            {
                outQuotient._data[l] = array[num15];
                num15--;
                l++;
            }
            for (; l < 70; l++)
            {
                outQuotient._data[l] = 0u;
            }
            while (outQuotient.dataLength > 1 && outQuotient._data[outQuotient.dataLength - 1] == 0)
            {
                outQuotient.dataLength--;
            }
            if (outQuotient.dataLength == 0)
            {
                outQuotient.dataLength = 1;
            }
            outRemainder.dataLength = ShiftRight(array2, num4);
            for (l = 0; l < outRemainder.dataLength; l++)
            {
                outRemainder._data[l] = array2[l];
            }
            for (; l < 70; l++)
            {
                outRemainder._data[l] = 0u;
            }
        }

        private static void SingleByteDivide(BigInteger bi1, BigInteger bi2, BigInteger outQuotient, BigInteger outRemainder)
        {
            uint[] array = new uint[70];
            int num = 0;
            for (int i = 0; i < 70; i++)
            {
                outRemainder._data[i] = bi1._data[i];
            }
            outRemainder.dataLength = bi1.dataLength;
            while (outRemainder.dataLength > 1 && outRemainder._data[outRemainder.dataLength - 1] == 0)
            {
                outRemainder.dataLength--;
            }
            ulong num2 = bi2._data[0];
            int num3 = outRemainder.dataLength - 1;
            ulong num4 = outRemainder._data[num3];
            if (num4 >= num2)
            {
                ulong num5 = num4 / num2;
                array[num++] = (uint)num5;
                outRemainder._data[num3] = (uint)(num4 % num2);
            }
            num3--;
            while (num3 >= 0)
            {
                num4 = ((ulong)outRemainder._data[num3 + 1] << 32) + outRemainder._data[num3];
                ulong num7 = num4 / num2;
                array[num++] = (uint)num7;
                outRemainder._data[num3 + 1] = 0u;
                outRemainder._data[num3--] = (uint)(num4 % num2);
            }
            outQuotient.dataLength = num;
            int j = 0;
            int num10 = outQuotient.dataLength - 1;
            while (num10 >= 0)
            {
                outQuotient._data[j] = array[num10];
                num10--;
                j++;
            }
            for (; j < 70; j++)
            {
                outQuotient._data[j] = 0u;
            }
            while (outQuotient.dataLength > 1 && outQuotient._data[outQuotient.dataLength - 1] == 0)
            {
                outQuotient.dataLength--;
            }
            if (outQuotient.dataLength == 0)
            {
                outQuotient.dataLength = 1;
            }
            while (outRemainder.dataLength > 1 && outRemainder._data[outRemainder.dataLength - 1] == 0)
            {
                outRemainder.dataLength--;
            }
        }

        public static BigInteger operator /(BigInteger bi1, BigInteger bi2)
        {
            BigInteger bigInteger = new BigInteger();
            BigInteger outRemainder = new BigInteger();
            int num = 69;
            bool flag = false;
            bool flag2 = false;
            if (((int)bi1._data[num] & -2147483648) != 0)
            {
                bi1 = -bi1;
                flag2 = true;
            }
            if (((int)bi2._data[num] & -2147483648) != 0)
            {
                bi2 = -bi2;
                flag = true;
            }
            if (bi1 < bi2)
            {
                return bigInteger;
            }
            if (bi2.dataLength == 1)
            {
                SingleByteDivide(bi1, bi2, bigInteger, outRemainder);
            }
            else
            {
                MultiByteDivide(bi1, bi2, bigInteger, outRemainder);
            }
            if (flag2 != flag)
            {
                return -bigInteger;
            }
            return bigInteger;
        }

        public static BigInteger operator %(BigInteger bi1, BigInteger bi2)
        {
            BigInteger outQuotient = new BigInteger();
            BigInteger bigInteger = new BigInteger(bi1);
            int num = 69;
            bool flag = false;
            if (((int)bi1._data[num] & -2147483648) != 0)
            {
                bi1 = -bi1;
                flag = true;
            }
            if (((int)bi2._data[num] & -2147483648) != 0)
            {
                bi2 = -bi2;
            }
            if (bi1 < bi2)
            {
                return bigInteger;
            }
            if (bi2.dataLength == 1)
            {
                SingleByteDivide(bi1, bi2, outQuotient, bigInteger);
            }
            else
            {
                MultiByteDivide(bi1, bi2, outQuotient, bigInteger);
            }
            if (flag)
            {
                return -bigInteger;
            }
            return bigInteger;
        }

        public static BigInteger operator &(BigInteger bi1, BigInteger bi2)
        {
            BigInteger bigInteger = new BigInteger();
            int num = (bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength;
            for (int i = 0; i < num; i++)
            {
                uint num2 = bi1._data[i] & bi2._data[i];
                bigInteger._data[i] = num2;
            }
            bigInteger.dataLength = 70;
            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }
            return bigInteger;
        }

        public static BigInteger operator |(BigInteger bi1, BigInteger bi2)
        {
            BigInteger bigInteger = new BigInteger();
            int num = (bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength;
            for (int i = 0; i < num; i++)
            {
                uint num2 = bi1._data[i] | bi2._data[i];
                bigInteger._data[i] = num2;
            }
            bigInteger.dataLength = 70;
            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }
            return bigInteger;
        }

        public static BigInteger operator ^(BigInteger bi1, BigInteger bi2)
        {
            BigInteger bigInteger = new BigInteger();
            int num = (bi1.dataLength > bi2.dataLength) ? bi1.dataLength : bi2.dataLength;
            for (int i = 0; i < num; i++)
            {
                uint num2 = bi1._data[i] ^ bi2._data[i];
                bigInteger._data[i] = num2;
            }
            bigInteger.dataLength = 70;
            while (bigInteger.dataLength > 1 && bigInteger._data[bigInteger.dataLength - 1] == 0)
            {
                bigInteger.dataLength--;
            }
            return bigInteger;
        }

        public BigInteger Max(BigInteger bi)
        {
            if (this > bi)
            {
                return new BigInteger(this);
            }
            return new BigInteger(bi);
        }

        public BigInteger Min(BigInteger bi)
        {
            if (this < bi)
            {
                return new BigInteger(this);
            }
            return new BigInteger(bi);
        }

        public BigInteger Abs()
        {
            if (((int)_data[69] & -2147483648) != 0)
            {
                return -this;
            }
            return new BigInteger(this);
        }

        public override string ToString()
        {
            return ToString(10);
        }

        public string ToString(int radix)
        {
            Throw.If(radix < 2 || radix > 36, "Radix must be >= 2 and <= 36");

            string text = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string text2 = "";
            BigInteger bigInteger = this;
            bool flag = false;

            if (((int)bigInteger._data[69] & -2147483648) != 0)
            {
                flag = true;
                try
                {
                    bigInteger = -bigInteger;
                }
                catch (Exception)
                {
                }
            }

            var bigInteger2 = new BigInteger();
            var bigInteger3 = new BigInteger();
            var bi = new BigInteger(radix);
            if (bigInteger.dataLength == 1 && bigInteger._data[0] == 0)
            {
                text2 = "0";
            }
            else
            {
                while (bigInteger.dataLength > 1 || (bigInteger.dataLength == 1 && bigInteger._data[0] != 0))
                {
                    SingleByteDivide(bigInteger, bi, bigInteger2, bigInteger3);
                    text2 = ((bigInteger3._data[0] >= 10) ? (text[(int)(bigInteger3._data[0] - 10)].ToString() + text2) : (bigInteger3._data[0] + text2));
                    bigInteger = bigInteger2;
                }
                if (flag)
                {
                    text2 = "-" + text2;
                }
            }

            return text2;
        }

        public string ToHexString()
        {
            string text = _data[dataLength - 1].ToString("X");
            for (int num = dataLength - 2; num >= 0; num--)
            {
                text += _data[num].ToString("X8");
            }
            return text;
        }

        public BigInteger GCD(BigInteger bi)
        {
            BigInteger bigInteger = (((int)_data[69] & -2147483648) == 0) ? this : (-this);
            BigInteger bigInteger2 = (((int)bi._data[69] & -2147483648) == 0) ? bi : (-bi);
            BigInteger bigInteger3 = bigInteger2;
            while (bigInteger.dataLength > 1 || (bigInteger.dataLength == 1 && bigInteger._data[0] != 0))
            {
                bigInteger3 = bigInteger;
                bigInteger = bigInteger2 % bigInteger;
                bigInteger2 = bigInteger3;
            }
            return bigInteger3;
        }

        public int BitCount()
        {
            while (dataLength > 1 && _data[dataLength - 1] == 0)
            {
                dataLength--;
            }
            uint num = _data[dataLength - 1];
            uint num2 = 2147483648u;
            int num3 = 32;
            while (num3 > 0 && (num & num2) == 0)
            {
                num3--;
                num2 >>= 1;
            }
            num3 += dataLength - 1 << 5;
            return (num3 == 0) ? 1 : num3;
        }

        public static explicit operator int(BigInteger value)
        {
            return (int)value._data[0];
        }

        public static explicit operator long(BigInteger value)
        {
            long num = 0L;
            num = value._data[0];
            try
            {
                num |= (long)((ulong)value._data[1] << 32);
            }
            catch (Exception)
            {
                if (((int)value._data[0] & -2147483648) != 0)
                {
                    num = (int)value._data[0];
                }
            }
            return num;
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
                if (bigInteger.dataLength == 1)
                {
                    SingleByteDivide(bi, bigInteger, bigInteger2, bigInteger3);
                }
                else
                {
                    MultiByteDivide(bi, bigInteger, bigInteger2, bigInteger3);
                }
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
            if (((int)bigInteger5._data[69] & -2147483648) != 0)
            {
                bigInteger5 += modulus;
            }
            return bigInteger5;
        }

        public byte[] ToByteArray()
        {
            bool flag = true;
            int num = BitCount();
            if (flag)
            {
                num++;
            }
            int num2 = num >> 3;
            if ((num & 7) != 0)
            {
                num2++;
            }
            byte[] array = new byte[num2];
            int num3 = 0;
            uint num4 = _data[dataLength - 1];
            if (flag)
            {
                array[num3++] = 0;
            }
            int num6 = num3;
            uint num7;
            if ((num7 = ((num4 >> 24) & 0xFF)) != 0)
            {
                array[num3++] = (byte)num7;
            }
            if ((num7 = ((num4 >> 16) & 0xFF)) != 0)
            {
                array[num3++] = (byte)num7;
            }
            else if (num3 > num6)
            {
                num3++;
            }
            if ((num7 = ((num4 >> 8) & 0xFF)) != 0)
            {
                array[num3++] = (byte)num7;
            }
            else if (num3 > num6)
            {
                num3++;
            }
            if ((num7 = (num4 & 0xFF)) != 0)
            {
                array[num3++] = (byte)num7;
            }
            else if (num3 > num6)
            {
                num3++;
            }
            int num12 = dataLength - 2;
            while (num12 >= 0)
            {
                num4 = _data[num12];
                array[num3 + 3] = (byte)(num4 & 0xFF);
                num4 >>= 8;
                array[num3 + 2] = (byte)(num4 & 0xFF);
                num4 >>= 8;
                array[num3 + 1] = (byte)(num4 & 0xFF);
                num4 >>= 8;
                array[num3] = (byte)(num4 & 0xFF);
                num12--;
                num3 += 4;
            }
            Array.Reverse(array);
            return array;
        }

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

        public BigInteger Sqrt()
        {
            Throw.If(this < 0, "cannot be negative");

            if (this == 0)
            {
                return 0;
            }

            uint num = (uint)BitCount();

            num = (((num & 1) == 0) ? (num >> 1) : ((num >> 1) + 1));
            uint num2 = num >> 5;
            byte b = (byte)(num & 0x1F);
            var bigInteger = new BigInteger();
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
            bigInteger.dataLength = (int)num2;
            for (int num4 = (int)(num2 - 1); num4 >= 0; num4--)
            {
                while (num3 != 0)
                {
                    bigInteger._data[num4] ^= num3;
                    if (bigInteger * bigInteger > this)
                    {
                        bigInteger._data[num4] ^= num3;
                    }
                    num3 >>= 1;
                }
                num3 = 2147483648u;
            }
            return bigInteger;
        }

        public static BigInteger Pow(BigInteger a, BigInteger b)
        {
            BigInteger bigInteger = 1;
            for (int i = 0; i < b; i++)
            {
                bigInteger *= a;
            }
            return bigInteger;
        }
    }
}
