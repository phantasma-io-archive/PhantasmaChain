using System;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Contracts
{
    public static class ScriptBuilderExtensions
    {
        public static ScriptBuilder LoanGas(this ScriptBuilder sb, Address from, BigInteger gasPrice, BigInteger gasLimit)
        {
            return sb.CallContract(Nexus.GasContractName, "LoanGas", from, gasPrice, gasLimit);
        }

        public static ScriptBuilder AllowGas(this ScriptBuilder sb, Address from, Address to, BigInteger gasPrice, BigInteger gasLimit)
        {
            return sb.CallContract(Nexus.GasContractName, "AllowGas", from, to, gasPrice, gasLimit);
        }

        public static ScriptBuilder SpendGas(this ScriptBuilder sb, Address address)
        {
            return sb.CallContract(Nexus.GasContractName, "SpendGas", address);
        }

        public static ScriptBuilder MintTokens(this ScriptBuilder sb, string tokenSymbol, Address target, BigInteger amount)
        {
            return sb.CallInterop("Runtime.MintTokens", target, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, string to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferNFT(this ScriptBuilder sb, string tokenSymbol, Address from, Address to, BigInteger tokenId)//todo check if this is valid
        {
            return sb.CallInterop("Runtime.TransferToken", from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder TransferNFT(this ScriptBuilder sb, string tokenSymbol, Address from, string to, BigInteger tokenId)//todo check if this is valid
        {
            return sb.CallInterop("Runtime.TransferToken", from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder CrossTransferToken(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.SendTokens", destinationChain, from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder CrossTransferToken(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, string to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.SendTokens", destinationChain, from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder CrossTransferNFT(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger tokenId)
        {
            return sb.CallInterop("Runtime.SendToken", destinationChain, from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder CrossTransferNFT(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, string to, BigInteger tokenId)
        {
            return sb.CallInterop("Runtime.SendToken", destinationChain, from, to, tokenSymbol, tokenId);
        }
    }
}
