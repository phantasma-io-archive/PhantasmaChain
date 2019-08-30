using Phantasma.Core;
using System;
using System.Text;

namespace Phantasma.Numerics
{
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
            var temp = new byte[input.Length + 1];
            for (int i=0; i<input.Length; i++)
            {
                temp[i] = input[(input.Length - 1) - i];
            }
            temp[input.Length] = 0;

            var value = BigInteger.FromSignedArray(temp);
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
