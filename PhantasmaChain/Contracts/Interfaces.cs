using System.Numerics;
using System;

namespace Phantasma.Contracts
{
    public interface IContractABI
    {
        byte[] PublicKey { get; }
        byte[] Script { get; }
        byte[] ABI { get; }
        string Name { get; }
    }

    public interface ITransaction
    {
        BigInteger Fee { get; }
        BigInteger Order { get; }
        byte[] Script { get; }
        byte[] Signature { get; }
        byte[] Hash { get; }
        byte[] PublicKey { get; }
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

    public interface IBlock
    {
        BigInteger Height { get; }
        byte[] Hash { get; }
        byte[] PreviousHash { get; }
        Timestamp Time { get; }
    }

    public interface IToken: IContractABI
    {
        string Symbol { get; }

        BigInteger BalanceOf(Address address);
    }

    [Flags]
    public enum TokenAttribute
    {
        None = 0x0,
        Burnable = 0x1,
        Mintable = 0x2,
        Tradable = 0x4,
        Infinite = 0x8,
    }

    public interface IFungibleToken : IToken
    {
        BigInteger CirculatingSupply { get; }
        BigInteger MaxSupply { get; }
        BigInteger Decimals { get; }
        TokenAttribute Attributes { get; }

        bool Send(Address destination, BigInteger amount);
    }

    public interface INonFungibleToken : IToken
    {
        byte[] ID { get; }
        byte[] Data { get; }

        bool Send(Address destination, byte[] ID);
    }

    public enum SaleState
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    public interface ISale : IContractABI
    {
        IFungibleToken GetToken();
        SaleState GetState();

        BigInteger GetSoftCap();
        BigInteger GetHardCap();

        BigInteger GetRate();

        [Payable]
        bool Contribute();
    }

}
