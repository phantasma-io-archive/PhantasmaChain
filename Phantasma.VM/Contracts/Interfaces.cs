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

    #region CONTRACTS
    public interface IContract
    {
        ContractInterface ABI { get; }
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
    /*public interface FungibleTransferTransaction : ITransaction
    {
        IFungibleToken Token { get; }
        Address Destination { get; }
        LargeInteger Amount { get; }
    }

    public interface NonFungibleTransferTransaction : ITransaction
    {
        INonFungibleToken Token { get; }
        Address Destination { get; }
        LargeInteger ID { get; }
    }*/

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
        LargeInteger MaxSupply { get; }
    }

    public interface IFungibleToken : IToken
    {
        LargeInteger CirculatingSupply { get; }

        LargeInteger BalanceOf(Address address);

        [ContractEvent]
        bool Send(Address from, Address to, LargeInteger amount);
    }

    public interface ILockable : IFungibleToken
    {
        [ContractEvent]
        bool Lock(Address address, DateTime until);
    }

    public interface IMintable : IFungibleToken
    {
        [ContractEvent]
        bool Mint(Address address, LargeInteger amount);
    }

    public interface IBurnable : IFungibleToken
    {
        [ContractEvent]
        bool Burn(Address address, LargeInteger amount);
    }

    public interface IDivisibleToken : IFungibleToken
    {
        LargeInteger Decimals { get; }
    }

    public interface INonFungibleToken : IToken
    {
        [ContractEvent]
        bool Send(Address from, Address to, LargeInteger ID);

        //LargeInteger Mint(Address address, byte[] data);

        bool IsOwner(Address address, LargeInteger ID);
        IEnumerable<LargeInteger> AssetsOf(Address address);
        byte[] DataOf(LargeInteger ID);
    }

    public interface IPerishable : INonFungibleToken
    {
        [ContractEvent]
        bool Perish(LargeInteger ID);
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

        LargeInteger GetRate(Timestamp time);

        [ContractEvent]
        bool Contribute();
    }

    public interface ISoftCapped : ISale
    {
        LargeInteger SoftCap { get; }
    }

    public interface IHardCapped : ISale
    {
        LargeInteger HardCap { get; }
    }
    #endregion

    #region GOVERNANCE
    public interface IVotable: IContract
    {
        [ContractEvent]
        bool BeginPool(LargeInteger subject, IEnumerable<byte[]> options, Timestamp duration);

        [ContractEvent]
        bool Vote(LargeInteger subject, byte[] secret);
    }
    #endregion

    #region BANK
    public interface IFund: IContract
    {
        // all tokens must either not be divisible or have same decimals 
        IEnumerable<IFungibleToken> Tokens { get; }

        // total amount of all tokens
        LargeInteger TotalAmount { get; }

        // get the amount of a certain token in the fund
        LargeInteger AmountOf(IFungibleToken token);

        // get the amount of shares than an specific address owns in this fund
        LargeInteger SharesOf(Address address);

        // deposits tokens into the fund and gets back a certain amount of shares 
        [ContractEvent]
        bool GetShares(LargeInteger amount);

        // redeems shares of this fund into a specific token
        [ContractEvent]
        bool ReedemShares(LargeInteger amount, IFungibleToken token);

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
