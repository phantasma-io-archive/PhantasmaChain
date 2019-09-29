using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Domain
{
    public interface IPlatform
    {
        string Name { get; }
        string Symbol { get; } // for fuel
        Address InteropAddress { get; }
        string ExternalAddress { get; }
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

        public InteropTransaction(Hash hash, InteropTransfer[] transfers)
        {
            Hash = hash;
            this.Transfers = transfers;
        }
    }

    public struct InteropTransfer
    {
        public readonly Address sourceAddress;
        public readonly Address destinationAddress;
        public readonly Address interopAddress;
        public readonly string Symbol;
        public BigInteger Amount;
        public byte[] Data;

        public InteropTransfer(Address sourceAddress, Address destinationAddress, Address interopAddress, string symbol, BigInteger amount, byte[] data = null)
        {
            this.sourceAddress = sourceAddress;
            this.destinationAddress = destinationAddress;
            this.interopAddress = interopAddress;
            Symbol = symbol;
            Amount = amount;
            Data = data != null ? data : new byte[0];
        }
    }
}
