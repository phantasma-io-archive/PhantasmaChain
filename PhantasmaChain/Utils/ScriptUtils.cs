using Phantasma.VM.Types;
using System.Numerics;

namespace Phantasma.Utils
{
    public static class ScriptUtils
    {
        public static byte[] TokenIssueScript(string name, string symbol, BigInteger initialSupply, BigInteger maxSupply)
        {
            // TODO
            /*sb.Emit(1, symbol);
            sb.Emit(2, initialSupply);
            sb.Emit(3, maxSupply);
            sb.Emit(4, (byte)attributes);*/
            return ContractDeployScript(new byte[] { }, new byte[] { });
        }

        public static byte[] ContractDeployScript(byte[] script, byte[] abi)
        {
            var sb = new ScriptBuilder();

            sb.EmitLoad(0, script);
            sb.EmitLoad(1, abi);

            sb.EmitExtCall("Chain.Deploy");
            sb.Emit(VM.Opcode.RET);

            return sb.ToScript();
        }

        public static byte[] TransferScript(Address tokenPublicKey, Address from, Address to, int amount)
        {
            var sb = new ScriptBuilder();
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }
    }
}
