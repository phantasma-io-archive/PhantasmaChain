using Phantasma.Core;
using System;
using System.Text;

namespace Phantasma.Numerics
{
    public static class Base58New
    {
        private const string DIGITS = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        private static BigInteger salt = 1;

        /// <summary>
        /// Encodes data with a 4-byte checksum
        /// </summary>
        /// <param name="data">Data to be encoded</param>
        /// <returns></returns>
        public static string Encode(byte[] data)
        {
            return EncodePlain(data);
        }

        /// <summary>
        /// Encodes data in plain Base58, without any checksum.
        /// </summary>
        /// <param name="data">The data to be encoded</param>
        /// <returns></returns>
        public static string EncodePlain(byte[] data)
        {
            // Decode byte[] to BigInteger
            var intData = BigInteger.FromUnsignedArray(data, true);

            // Encode BigInteger to Base58 string
            var result = string.Empty;
            while (intData > 0)
            {
                var remainder = (int)(intData % 58);
                intData /= 58;
                result = DIGITS[remainder] + result;
            }

            // Append `1` for each leading 0 byte
            /*for (var i = 0; i < data.Length && data[i] == 0; i++)
            {
                result = '1' + result;
            }*/
            for (var i = data.Length - 1; i >=  0 && data[i] == 0; i--)
            {
                result = '1' + result;
            }

            return result;
        }

        /// <summary>
        /// Decodes data in Base58Check format (with 4 byte checksum)
        /// </summary>
        /// <param name="data">Data to be decoded</param>
        /// <returns>Returns decoded data if valid; throws FormatException if invalid</returns>
        public static byte[] Decode(string data)
        {
            var dataWithoutCheckSum = DecodePlain(data);

            if (dataWithoutCheckSum == null)
            {
                throw new FormatException("Base58 checksum is invalid");
            }

            return dataWithoutCheckSum;
        }

        /// <summary>
        /// Decodes data in plain Base58, without any checksum.
        /// </summary>
        /// <param name="data">Data to be decoded</param>
        /// <returns>Returns decoded data if valid; throws FormatException if invalid</returns>
        public static byte[] DecodePlain(string data)
        {
            // Decode Base58 string to BigInteger 
            BigInteger intData = 0;
            for (var i = 0; i < data.Length; i++)
            {
                var digit = DIGITS.IndexOf(data[i]); //Slow

                if (digit < 0)
                {
                    throw new FormatException(string.Format("Invalid Base58 character `{0}` at position {1}", data[i], i));
                }

                intData = intData * 58 + digit;
            }

            // Encode BigInteger to byte[]
            // Leading zero bytes get encoded as leading `1` characters

            //var leadingZeroCount = data.TakeWhile(c => c == '1').Count();
            int leadingZeroCount = 0;
            foreach (var b in data)
            {
                if (b == '1')
                    leadingZeroCount++;
                else
                    break;
            }

            /*
            byte[] leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
            var bytesWithoutLeadingZeros =
              intData.ToByteArray()
              .Reverse()// to big endian
              .SkipWhile(b => b == 0);//strip sign byte
            */
            //var result = leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();

            var intDataByteArray = intData.ToUnsignedByteArray();
            byte[] result = new byte[leadingZeroCount + intDataByteArray.Length];
            intDataByteArray.CopyTo(result, 0);

            return result;
        }
    }

    public static class Base58
    {
        public const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        public static byte[] Decode(string input)
        {
            Throw.If(input == null || input.Length == 0, "string cant be empty");

            var bi = BigInteger.Zero;
            for (int i = input.Length - 1; i >= 0; i--)
            {
                int index = Alphabet.IndexOf(input[i]);
                Throw.If(index < 0, "invalid character");

                bi += index * BigInteger.Pow(58, input.Length - 1 - i);
            }

            byte[] bytes = bi.ToUnsignedByteArray();
            Array.Reverse(bytes);

            int leadingZeros = 0;
            for (int i = 0; i < input.Length && input[i] == Alphabet[0]; i++)
            {
                leadingZeros++;
            }

            byte[] tmp = new byte[bytes.Length + leadingZeros];
            Array.Copy(bytes, 0, tmp, leadingZeros, tmp.Length - leadingZeros);
            return tmp;
        }

        public static string Encode(byte[] input)
        {
            var temp = new byte[input.Length];
            for (int i=0; i<input.Length; i++)
            {
                temp[i] = input[(input.Length - 1) - i];
            }
            //temp[input.Length] = 0;

            var value = BigInteger.FromUnsignedArray(temp, isPositive: true);
            var sb = new StringBuilder();
            while (value >= 58)
            {
                var mod = value % 58;
                sb.Insert(0, Alphabet[(int)mod]);
                value /= 58;
            }
            sb.Insert(0, Alphabet[(int)value]);

            foreach (byte b in input)
            {
                if (b == 0)
                    sb.Insert(0, Alphabet[0]);
                else
                    break;
            }
            return sb.ToString();
        }

    }
}
