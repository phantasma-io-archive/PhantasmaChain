using Phantasma.Contracts;
using Phantasma.Core;
using System;
using System.Numerics;

namespace Phantasma.Utils
{
    public static class ScriptUtils
    {
        public static byte[] TokenIssueScript(string name, string symbol, BigInteger initialSupply, BigInteger maxSupply, TokenAttribute attributes)
        {
            var sb = new ScriptBuilder();
            var script = new byte[20];
            var abi = new byte[20];

            sb.EmitLoad(0, script);
            sb.EmitLoad(1, abi);

            /*sb.Emit(1, symbol);
            sb.Emit(2, initialSupply);
            sb.Emit(3, maxSupply);
            sb.Emit(4, (byte)attributes);*/
            sb.EmitCall("Chain.Deploy");
            sb.Emit(VM.Opcode.RET);

            return sb.ToScript();
        }

        public static byte[] TransferScript(string symbol, byte[] fromKey, byte[] toKey, int amount)
        {
            var sb = new ScriptBuilder();
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }
    }
}
