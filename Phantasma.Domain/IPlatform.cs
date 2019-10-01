using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Domain
{
    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        string ExternalAddress { get; }
        Address ChainAddress { get; }
        Address[] InteropAddresses { get; }
    }

    public struct InteropBlock
    {
        public readonly string Platform;
        public readonly string Chain;
        public readonly Hash Hash;
        public readonly Hash[] Transactions;

        public InteropBlock(string platform, string chain, Hash hash, Hash[] transactions)
        {
            Platform = platform;
            Chain = chain;
            Hash = hash;
            Transactions = transactions;
        }
    }

    public struct InteropTransaction
    {
        public readonly Hash Hash;        
        public readonly InteropTransfer[] Transfers;

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
}
