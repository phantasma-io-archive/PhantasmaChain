using Phantasma.Core;
using System;
using System.Numerics;

namespace Phantasma.Utils
{
    public static class ScriptUtils
    {
        public static byte[] TokenIssueScript(string name, string symbol, BigInteger initialSupply, BigInteger maxSupply, Token.Attribute attributes)
        {
            var sb = new ScriptBuilder();
            sb.Emit(0, name);
            sb.Emit(1, symbol);
            sb.Emit(2, initialSupply);
            sb.Emit(3, maxSupply);
            sb.Emit(4, (byte)attributes);
            sb.EmitCall("Asset.Deploy");
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }

        public static byte[] TransferScript(byte[] id, byte[] fromKey, byte[] toKey, int amount)
        {
            var sb = new ScriptBuilder();
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }
    }
}
