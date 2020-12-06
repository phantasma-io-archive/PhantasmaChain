using System.Numerics;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Domain
{
    public struct PlatformSwapAddress
    {
        public string ExternalAddress;
        public Address LocalAddress;
    }

    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        PlatformSwapAddress[] InteropAddresses { get; }
    }

    public class InteropBlock
    {
        public readonly string Platform;
        public readonly string Chain;
        public readonly Hash Hash;
        public readonly Hash[] Transactions;

        public InteropBlock()
        {

        }

        public InteropBlock(string platform, string chain, Hash hash, Hash[] transactions)
        {
            Platform = platform;
            Chain = chain;
            Hash = hash;
            Transactions = transactions;
        }
    }

    public class InteropTransaction
    {
        public readonly Hash Hash;        
        public readonly InteropTransfer[] Transfers;

        public InteropTransaction()
        {

        }

        public InteropTransaction(Hash hash, IEnumerable<InteropTransfer> transfers)
        {
            Hash = hash;
            this.Transfers = transfers.ToArray();
        }
    }

    public struct InteropTransfer
    {
        public readonly string sourceChain;
        public readonly Address sourceAddress;
        public readonly string destinationChain;
        public readonly Address destinationAddress;
        public readonly Address interopAddress;
        public readonly string Symbol;
        public BigInteger Value;
        public byte[] Data;

        public InteropTransfer(string sourceChain, Address sourceAddress, string destinationChain, Address destinationAddress, Address interopAddress, string symbol, BigInteger value, byte[] data = null)
        {
            this.sourceChain = sourceChain;
            this.sourceAddress = sourceAddress;
            this.destinationChain = destinationChain;
            this.destinationAddress = destinationAddress;
            this.interopAddress = interopAddress;
            Symbol = symbol;
            Value = value;
            Data = data != null ? data : new byte[0];
        }
    }

    public struct InteropNFT
    {
        public readonly string Name;
        public readonly string Description;
        public readonly string ImageURL;

        public InteropNFT(string name, string description, string imageURL)
        {
            Name = name;
            Description = description;
            ImageURL = imageURL;
        }
    }
}
