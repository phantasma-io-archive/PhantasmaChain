using Phantasma.Contracts.Types;
using System.Numerics;

namespace Phantasma.Contracts.Interfaces
{
    public interface IToken: IContract
    {
        string Symbol { get; }
        string Name { get; }

        BigInteger BalanceOf(Address address);
    }

    public interface IFungibleToken : IToken
    {
        BigInteger CirculatingSupply { get; }
        BigInteger MaxSupply { get; }
        uint Decimals { get; }

        bool Send(Address destination, BigInteger amount);
    }

    public interface INonFungibleToken : IToken
    {
        byte[] ID { get; }
        byte[] Data { get; }

        bool Send(Address destination, byte[] ID);
    }

}
