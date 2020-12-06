using System.Numerics;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;
using Phantasma.Domain;
using Phantasma.Blockchain.Contracts;

namespace Phantasma.Blockchain
{
    public static class ScriptBuilderExtensions
    {
        public static ScriptBuilder AllowGas(this ScriptBuilder sb, Address from, Address to, BigInteger gasPrice, BigInteger gasLimit)
        {
            return sb.CallContract(NativeContractKind.Gas, nameof(GasContract.AllowGas), from, to, gasPrice, gasLimit);
        }

        public static ScriptBuilder SpendGas(this ScriptBuilder sb, Address address)
        {
            return sb.CallContract(NativeContractKind.Gas, nameof(GasContract.SpendGas), address);
        }

        public static ScriptBuilder MintTokens(this ScriptBuilder sb, string tokenSymbol, Address from, Address target, BigInteger amount)
        {
            return sb.CallInterop("Runtime.MintTokens", from, target, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, string to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return sb.CallInterop("Runtime.TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferBalance(this ScriptBuilder sb, string tokenSymbol, Address from, Address to)
        {
            return sb.CallInterop("Runtime.TransferBalance", from, to, tokenSymbol);
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

        public static ScriptBuilder CallContract(this ScriptBuilder sb, NativeContractKind contractKind, string method, params object[] args)
        {
            return sb.CallContract(contractKind.ToString().ToLower(), method, args);
        }

        public static ScriptBuilder CallNFT(this ScriptBuilder sb, string symbol, BigInteger seriesID, string method, params object[] args)
        {
            var contractName = $"{symbol}#{seriesID}";
            return sb.CallContract(contractName, method, args);
        }
    }
}
