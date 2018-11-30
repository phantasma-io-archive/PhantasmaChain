using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Blockchain.Tokens;
using System;
using Phantasma.VM;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts.Native;
using System.Text;

namespace Phantasma.Blockchain
{
    public static class ScriptUtils
    {
        public static byte[] CallContractScript(string contractName, string method, params object[] args)
        {
            var sb = new ScriptBuilder();
            byte dest_reg = 1;
            var contractNameBytes = Encoding.ASCII.GetBytes(contractName);
            sb.Emit(VM.Opcode.CTX, ByteArrayUtils.ConcatBytes(new byte[] { dest_reg }, contractNameBytes));

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
                if (arg is Hash)
                {
                    sb.EmitLoad(temp_reg, ((Hash)arg).ToByteArray(), VMType.Bytes);
                    sb.EmitPush(temp_reg);
                    sb.EmitExtCall("Hash()", temp_reg);
                }
                else
                {
                    throw new System.Exception("invalid type");
                }
            }

            sb.EmitLoad(temp_reg, method);
            sb.EmitPush(temp_reg);
            sb.Emit(VM.Opcode.SWITCH, new byte[] { dest_reg });
            sb.Emit(VM.Opcode.RET);
            return sb.ToScript();
        }

        public static readonly string NexusContract = "nexus";

        public static byte[] TokenMintScript(string tokenSymbol, Address target, BigInteger amount)
        {
            return CallContractScript(NexusContract, "MintTokens", tokenSymbol, target, amount);
        }

        public static byte[] TokenTransferScript(string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return CallContractScript(NexusContract, "TransferTokens", from, to, tokenSymbol, amount);
        }

        public static byte[] NfTokenTransferScript(string tokenSymbol, Address from, Address to, BigInteger tokenId)//todo check if this is valid
        {
            return CallContractScript(NexusContract, "TransferToken", from, to, tokenSymbol, tokenId);
        }

        public static byte[] CrossTokenTransferScript(Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return CallContractScript(NexusContract, "SendTokens", destinationChain, from, to, tokenSymbol, amount);
        }

        public static byte[] CrossNfTokenTransferScript(Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger tokenId)
        {
            return CallContractScript(NexusContract, "SendToken", destinationChain, from, to, tokenSymbol, tokenId);
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
    }
}
