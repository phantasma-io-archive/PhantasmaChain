using System;
using Phantasma.Numerics;

namespace Phantasma.CodeGen
{
    public static class ArgumentUtils
    {
        public const string BYTE_PREFIX = "0x";
        public const string REG_PREFIX = "r";
        public const string LABEL_PREFIX = "@";
        public const string STRING_PREFIX = "\"";

        public static bool IsString(this string arg)
        {
            return arg.Length >= 2 && arg.StartsWith(STRING_PREFIX) && arg.EndsWith(STRING_PREFIX);
        }

        public static bool IsRegister(this string arg)
        {
            return arg.StartsWith(REG_PREFIX) && arg.Length > 1;
        }

        public static bool IsLabel(this string arg)
        {
            return arg.StartsWith(LABEL_PREFIX) && arg.Length > 1;
        }

        public static bool IsNumber(this string arg)
        {
            return !string.IsNullOrEmpty(arg) && BigInteger.TryParse(arg, out BigInteger temp);
        }

        public static bool IsBytes(this string arg)
        {
            return !string.IsNullOrEmpty(arg) && arg.Length >= 3 && arg.StartsWith(BYTE_PREFIX) && (arg.Length % 2 == 0);
        }

        public static bool IsBool(this string arg)
        {
            return !string.IsNullOrEmpty(arg) && (arg.Equals("false", StringComparison.OrdinalIgnoreCase) || arg.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        public static string AsString(this string arg)
        {
            return arg.Substring(1, arg.Length - 2);
        }

        public static byte AsRegister(this string arg)
        {
            return byte.Parse(arg.Substring(1));
        }

        public static string AsLabel(this string arg)
        {
            return arg.Substring(1);
        }

        public static bool AsBool(this string arg)
        {
            return arg.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static BigInteger AsNumber(this string arg)
        {
            var result = BigInteger.Parse(arg);
            return result;
        }

        public static byte[] AsBytes(this string arg)
        {
            return Base16.Decode(arg);
        }
    }
}
