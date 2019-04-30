using System;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Core.Types;

namespace Phantasma.VM.Utils
{
    public static class ScriptUtils
    {
        public static ScriptBuilder BeginScript()
        {
            var sb = new ScriptBuilder();
            return sb;
        }

        public static byte[] EndScript(this ScriptBuilder sb)
        {
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }

        public static ScriptBuilder AllowGas(this ScriptBuilder sb, Address from, Address to, BigInteger gasPrice, BigInteger gasLimit)
        {
            CallContract(sb, "gas", "AllowGas", from, to, gasPrice, gasLimit);
            return sb;
        }

        public static ScriptBuilder SpendGas(this ScriptBuilder sb, Address address)
        {
            CallContract(sb, "gas", "SpendGas", address);
            return sb;
        }

        // returns register slot that contains the method name
        private static void InsertMethodArgs(ScriptBuilder sb, object[] args)
        {
            byte temp_reg = 0;

            for (int i = args.Length - 1; i >= 0; i--)
            {
                var arg = args[i];

                if (arg is string)
                {
                    sb.EmitLoad(temp_reg, (string)arg);
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is int)
                {
                    sb.EmitLoad(temp_reg, new BigInteger((int)arg));
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is BigInteger)
                {
                    sb.EmitLoad(temp_reg, (BigInteger)arg);
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is bool)
                {
                    sb.EmitLoad(temp_reg, (bool)arg);
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is byte[])
                {
                    sb.EmitLoad(temp_reg, (byte[])arg, VMType.Bytes);
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is Enum)
                {
                    sb.EmitLoad(temp_reg, (Enum)arg);
                    sb.EmitPush(temp_reg);
                }
                else
                if (arg is Address)
                {
                    sb.EmitLoad(temp_reg, ((Address)arg).PublicKey, VMType.Bytes);
                    sb.EmitPush(temp_reg);
                    sb.EmitExtCall("Address()", temp_reg);
                }
                else
                if (arg is Timestamp)
                {
                    sb.EmitLoad(temp_reg, ((Timestamp)arg).Value);
                    sb.EmitPush(temp_reg);
                    sb.EmitExtCall("Timestamp()", temp_reg);
                }
                else
                if (arg is Hash)
                {
                    sb.EmitLoad(temp_reg, ((Hash)arg).ToByteArray(), VMType.Bytes);
                    sb.EmitPush(temp_reg);
                    sb.EmitExtCall("Hash()", temp_reg);
                }
                else
                {
                    throw new System.Exception("invalid type: "+arg.GetType().Name);
                }
            }
        }

        public static ScriptBuilder CallInterop(this ScriptBuilder sb, string method, params object[] args)
        {
            InsertMethodArgs(sb, args);

            byte dest_reg = 0;
            sb.EmitLoad(dest_reg, method);

            sb.Emit(VM.Opcode.EXTCALL, new byte[] { dest_reg });
            return sb;
        }

        public static ScriptBuilder CallContract(this ScriptBuilder sb, string contractName, string method, params object[] args)
        {
            byte src_reg = 0;
            byte dest_reg = 1;
            sb.EmitLoad(src_reg, contractName);
            sb.Emit(VM.Opcode.CTX, new byte[] { src_reg, dest_reg });

            InsertMethodArgs(sb, args);

            byte temp_reg = 0;
            sb.EmitLoad(temp_reg, method);
            sb.EmitPush(temp_reg);

            sb.Emit(VM.Opcode.SWITCH, new byte[] { dest_reg });
            return sb;
        }
    }
}
