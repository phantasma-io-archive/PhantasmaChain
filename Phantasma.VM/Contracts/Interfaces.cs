using System;
using System.Collections.Generic;

using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.VM.Contracts
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
    public class ContractEventAttribute: Attribute
    {

    }

    public interface IBlock
    {
        uint Height { get; }
        Hash Hash { get; }
        Hash PreviousHash { get; }
        Timestamp Timestamp { get; }
        IEnumerable<ITransaction> Transactions { get; }
    }

    public interface ITransaction
    {
        byte[] Script { get; }
        Signature[] Signatures { get; }
        Hash Hash { get; }
    }

    #region CONTRACTS
    public interface IContract
    {
        ContractInterface ABI { get; }
        byte[] Script { get; }
    }

    public interface IMigratable : IContract
    {
        [ContractEvent]
        bool Migrate(byte[] script);
    }

    public interface IDestructible : IContract
    {
        [ContractEvent]
        bool Destroy();
    }
    #endregion

    #region TOKENS
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
        BigInteger ID { get; }
    }

    public interface IToken: IContract
    {
        string Symbol { get; }
    }

    public interface ISwappable : IToken
    {
        IEnumerable<IFund> Funds { get; }

        [ContractEvent]
        bool AddFund(IFund fund);

        [ContractEvent]
        bool RemoveFund(IFund fund);
    }

    public interface IFiniteToken : IToken
    {
        BigInteger MaxSupply { get; }
    }

    public interface IFungibleToken : IToken
    {
        BigInteger CirculatingSupply { get; }

        BigInteger BalanceOf(Address address);

        [ContractEvent]
        bool Send(Address from, Address to, BigInteger amount);
    }

    public interface ILockable : IFungibleToken
    {
        [ContractEvent]
        bool Lock(Address address, DateTime until);
    }

    public interface IMintable : IFungibleToken
    {
        [ContractEvent]
        bool Mint(Address address, BigInteger amount);
    }

    public interface IBurnable : IFungibleToken
    {
        [ContractEvent]
        bool Burn(Address address, BigInteger amount);
    }

    public interface IDivisibleToken : IFungibleToken
    {
        BigInteger Decimals { get; }
    }

    public interface INonFungibleToken : IToken
    {
        [ContractEvent]
        bool Send(Address from, Address to, BigInteger ID);

        //BigInteger Mint(Address address, byte[] data);

        bool IsOwner(Address address, BigInteger ID);
        IEnumerable<BigInteger> AssetsOf(Address address);
        byte[] DataOf(BigInteger ID);
    }

    public interface IPerishable : INonFungibleToken
    {
        [ContractEvent]
        bool Perish(BigInteger ID);
    }
    #endregion

    #region SALES
    public enum SaleState
    {
        Pending,
        Active,
        Completed,
        Cancelled
    }

    public interface ISale : IContract
    {
        IToken Token { get; }
        SaleState State { get; }

        BigInteger GetRate(Timestamp time);

        [ContractEvent]
        bool Contribute();
    }

    public interface ISoftCapped : ISale
    {
        BigInteger SoftCap { get; }
    }

    public interface IHardCapped : ISale
    {
        BigInteger HardCap { get; }
    }
    #endregion

    #region GOVERNANCE
    public interface IVotable: IContract
    {
        [ContractEvent]
        bool BeginPool(BigInteger subject, IEnumerable<byte[]> options, Timestamp duration);

        [ContractEvent]
        bool Vote(BigInteger subject, byte[] secret);
    }
    #endregion

    #region BANK
    public interface IFund: IContract
    {
        // all tokens must either not be divisible or have same decimals 
        IEnumerable<IFungibleToken> Tokens { get; }

        // total amount of all tokens
        BigInteger TotalAmount { get; }

        // get the amount of a certain token in the fund
        BigInteger AmountOf(IFungibleToken token);

        // get the amount of shares than an specific address owns in this fund
        BigInteger SharesOf(Address address);

        // deposits tokens into the fund and gets back a certain amount of shares 
        [ContractEvent]
        bool GetShares(BigInteger amount);

        // redeems shares of this fund into a specific token
        [ContractEvent]
        bool ReedemShares(BigInteger amount, IFungibleToken token);

        // deposit a token and get other token back
        [ContractEvent]
        bool Swap(IFungibleToken target);
    }

    public interface IBank: IContract
    {
        IEnumerable<IFund> Funds { get; }
    }
    #endregion
}
