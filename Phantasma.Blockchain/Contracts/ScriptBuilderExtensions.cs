using System;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain.Contracts
{
    public static class ScriptBuilderExtensions
    {
        public static readonly string NexusContract = "nexus";
        public static readonly string TokenContract = "token";
        public static readonly string EnergyContract = "energy";


        public static ScriptBuilder MintTokens(this ScriptBuilder sb, string tokenSymbol, Address target, BigInteger amount)
        {
            return sb.CallContract(TokenContract, "MintTokens", tokenSymbol, target, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, string to, BigInteger amount)
        {
            return sb.CallContract(TokenContract, "TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferTokens(this ScriptBuilder sb, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return sb.CallContract(TokenContract, "TransferTokens", from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder TransferNFT(this ScriptBuilder sb, string tokenSymbol, Address from, Address to, BigInteger tokenId)//todo check if this is valid
        {
            return sb.CallContract(TokenContract, "TransferToken", from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder TransferNFT(this ScriptBuilder sb, string tokenSymbol, Address from, string to, BigInteger tokenId)//todo check if this is valid
        {
            return sb.CallContract(TokenContract, "TransferToken", from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder CrossTransferToken(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger amount)
        {
            return sb.CallContract(TokenContract, "SendTokens", destinationChain, from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder CrossTransferToken(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, string to, BigInteger amount)
        {
            return sb.CallContract(TokenContract, "SendTokens", destinationChain, from, to, tokenSymbol, amount);
        }

        public static ScriptBuilder CrossTransferNFT(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, Address to, BigInteger tokenId)
        {
            return sb.CallContract(TokenContract, "SendToken", destinationChain, from, to, tokenSymbol, tokenId);
        }

        public static ScriptBuilder CrossTransferNFT(this ScriptBuilder sb, Address destinationChain, string tokenSymbol, Address from, string to, BigInteger tokenId)
        {
            return sb.CallContract(TokenContract, "SendToken", destinationChain, from, to, tokenSymbol, tokenId);
        }
    }
}
