using Phantasma.Core;

namespace Phantasma.Numerics
{
    public static class Base16
    {
        private const string hexAlphabet = "0123456789ABCDEF";

        public static byte[] Decode(this string input)
        {
            Throw.If(input == null || input.Length == 0, "string cant be empty");

            if (input.StartsWith("0x"))
            {
                return input.Substring(2).Decode();
            }

            Throw.If(input.Length % 2 == 1, "string length must be even");

            byte[] result = new byte[input.Length / 2];

            for (int i = 0; i < result.Length; i++)
            {
                var str = input.Substring(i * 2, 2);
                int A = hexAlphabet.IndexOf(str[0]);
                int B = hexAlphabet.IndexOf(str[1]);

                Throw.If(A < 0 || B < 0, "invalid character");

                result[i] = (byte)(A * 16 + B);
            }

            return result;
        }

        // constant time hex conversion
        // see http://stackoverflow.com/a/14333437/445517
        //
        // An explanation of the weird bit fiddling:
        //
        // 1. `bytes[i] >> 4` extracts the high nibble of a byte  
        //   `bytes[i] & 0xF` extracts the low nibble of a byte
        // 2. `b - 10`  
        //    is `< 0` for values `b < 10`, which will become a decimal digit  
        //    is `>= 0` for values `b > 10`, which will become a letter from `A` to `F`.
        // 3. Using `i >> 31` on a signed 32 bit integer extracts the sign, thanks to sign extension.
        //    It will be `-1` for `i < 0` and `0` for `i >= 0`.
        // 4. Combining 2) and 3), shows that `(b-10)>>31` will be `0` for letters and `-1` for digits.
        // 5. Looking at the case for letters, the last summand becomes `0`, and `b` is in the range 10 to 15. We want to map it to `A`(65) to `F`(70), which implies adding 55 (`'A'-10`).
        // 6. Looking at the case for digits, we want to adapt the last summand so it maps `b` from the range 0 to 9 to the range `0`(48) to `9`(57). This means it needs to become -7 (`'0' - 55`).  
        // Now we could just multiply with 7. But since -1 is represented by all bits being 1, we can instead use `& -7` since `(0 & -7) == 0` and `(-1 & -7) == -7`.
        //
        // Some further considerations:
        //
        // * I didn't use a second loop variable to index into `c`, since measurement shows that calculating it from `i` is cheaper. 
        // * Using exactly `i < bytes.Length` as upper bound of the loop allows the JITter to eliminate bounds checks on `bytes[i]`, so I chose that variant.
        // * Making `b` an int avoids unnecessary conversions from and to byte.
        public static string Encode(this byte[] input)
        {
            Throw.IfNull(input, "input");

            char[] c = new char[input.Length * 2];
            int b;

            for (int i = 0; i < input.Length; i++)
            {
                b = input[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = input[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }

            return new string(c);
        }
    }
}
