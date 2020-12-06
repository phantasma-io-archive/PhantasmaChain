using System;
using System.Numerics;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

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

        // NOTE when arg is an array, this method will corrupt the two registers adjacent to target_reg
        // "corrupt" in the sense of any value being there previousvly will be lost
        // this should be taken into account by methods that call this method
        // also this was not tested with nested arrays, might work, might not work yet
        private static void LoadIntoReg(ScriptBuilder sb, byte target_reg, object arg)
        {
            if (arg is string)
            {
                sb.EmitLoad(target_reg, (string)arg);
            }
            else
            if (arg is int)
            {
                sb.EmitLoad(target_reg, new BigInteger((int)arg));
            }
            else 
            if (arg is long)
            {
                sb.EmitLoad(target_reg, new BigInteger((long)arg));
            }
            else
            if (arg is BigInteger)
            {
                sb.EmitLoad(target_reg, (BigInteger)arg);
            }
            else
            if (arg is bool)
            {
                sb.EmitLoad(target_reg, (bool)arg);
            }
            else
            if (arg is byte[])
            {
                sb.EmitLoad(target_reg, (byte[])arg, VMType.Bytes);
            }
            else
            if (arg is Enum)
            {
                sb.EmitLoad(target_reg, (Enum)arg);
            }
            else
            if (arg is Timestamp)
            {
                sb.EmitLoad(target_reg, (Timestamp)arg);
            }
            else
            if (arg is ISerializable)
            {
                sb.EmitLoad(target_reg, (ISerializable)arg);
            }
            else
            {
                var srcType = arg.GetType();

                if (srcType.IsArray)
                {
                    // this cast is required to clear any previous value that might be stored at target_reg
                    sb.Emit(Opcode.CAST, new byte[] { target_reg, target_reg, (byte)VMType.None });
                    var array = (Array)arg;
                    for (int j = 0; j < array.Length; j++)
                    {
                        var element = array.GetValue(j);
                        byte temp_regVal = (byte)(target_reg + 1);
                        byte temp_regKey = (byte)(target_reg + 2);
                        LoadIntoReg(sb, temp_regVal, element);
                        LoadIntoReg(sb, temp_regKey, j);
                        sb.Emit(Opcode.PUT, new byte[] { temp_regVal, target_reg , temp_regKey });
                    }
                }
                else
                {
                    throw new System.Exception("invalid type: " + srcType.Name);
                }
            }

        }

        // returns register slot that contains the method name
        private static void InsertMethodArgs(ScriptBuilder sb, object[] args)
        {
            byte temp_reg = 0;

            for (int i = args.Length - 1; i >= 0; i--)
            {
                var arg = args[i];
                LoadIntoReg(sb, temp_reg, arg);
                sb.EmitPush(temp_reg);
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
            InsertMethodArgs(sb, args);

            byte temp_reg = 0;
            sb.EmitLoad(temp_reg, method);
            sb.EmitPush(temp_reg);

            byte src_reg = 0;
            byte dest_reg = 1;
            sb.EmitLoad(src_reg, contractName);
            sb.Emit(VM.Opcode.CTX, new byte[] { src_reg, dest_reg });

            sb.Emit(VM.Opcode.SWITCH, new byte[] { dest_reg });
            return sb;
        }
    }
}
