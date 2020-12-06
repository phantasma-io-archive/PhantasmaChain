using System;
using System.Numerics;
using System.Collections.Generic;
using Phantasma.VM;
using Phantasma.Numerics;

namespace Phantasma.CodeGen.Assembler
{
    // NOTE since everything here is done as static, its necessary to call ClearAlias() for every new compilation!
    // later I recommend rewriting this as a normal class
    public static class ArgumentUtils
    {
        public const string BYTE_PREFIX = "0x";
        public const string REG_PREFIX = "r";
        public const string TYPE_PREFIX = "#";
        public const string ALIAS_PREFIX = "$";
        public const string LABEL_PREFIX = "@";
        public const string STRING_PREFIX = "\"";

        private static Dictionary<string, byte> _aliasMap = new Dictionary<string, byte>();

        public static void ClearAlias()
        {
            _aliasMap.Clear();
        }

        public static void RegisterAlias(string name, byte register)
        {
            _aliasMap[name] = register;
        }

        public static bool IsString(this string arg)
        {
            return arg.Length >= 2 && arg.StartsWith(STRING_PREFIX) && arg.EndsWith(STRING_PREFIX);
        }

        public static bool IsRegister(this string arg)
        {
            return ((arg.StartsWith(REG_PREFIX) || arg.StartsWith(ALIAS_PREFIX)) && arg.Length > 1);
        }

        public static bool IsAlias(this string arg)
        {
            return (arg.StartsWith(ALIAS_PREFIX) && arg.Length > 1);
        }

        public static bool IsType(this string arg)
        {
            return arg.StartsWith(TYPE_PREFIX) && arg.Length > 1;
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

        // TODO check if valid identifier
        public static string AsAlias(this string arg)
        {
            return arg.Substring(1);
        }

        public static byte AsRegister(this string arg)
        {
            var val = arg.Substring(1);

            if (arg.StartsWith(ALIAS_PREFIX))
            {
                if (_aliasMap.ContainsKey(val))
                {
                    return _aliasMap[val];
                }
                else
                {
                    throw new Exception("Unknown register alias: " + val);
                }
            }

            return byte.Parse(val);
        }

        public static byte AsType(this string arg)
        {
            VMType vmType;

            var val = arg.Substring(1);
            if (Enum.TryParse(val, true, out vmType))
            {
                return (byte)vmType;
            }
            else
            {
                throw new Exception("Invalid asm type: " + arg);
            }
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
