using Phantasma.Contracts.Interfaces;
using System.Numerics;

namespace Phantasma.Contracts.Types
{
    public interface ITransaction
    {
        Address Source { get; }
        Block Block { get; }
    }

    public interface FungibleTransferTransaction : ITransaction
    {
        IFungibleToken Token { get; }
        Address Destination { get; }
        BigInteger Amount { get; }
    }

    public interface NonFungibleTransferTransaction : ITransaction
    {
        INonFungibleToken Token { get; }
        Address Destination { get; }
        byte[] ID { get; }
    }
}
