using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.IO;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct MarketAuction
    {
        public readonly Address Creator;
        public readonly Timestamp StartDate;
        public readonly Timestamp EndDate;
        public readonly string Symbol;
        public readonly BigInteger TokenID;
        public readonly BigInteger Price;

        public MarketAuction(Address creator, Timestamp startDate, Timestamp endDate, string symbol, BigInteger tokenID, BigInteger price)
        {
            Creator = creator;
            StartDate = startDate;
            EndDate = endDate;
            this.Symbol = symbol;
            this.TokenID = tokenID;
            Price = price;
        }
    }

    public sealed class MarketContract : SmartContract
    {
        public override string Name => "exchange";

        internal StorageMap _auctionMap; //<string, Collection<MarketAuction>>
        internal StorageList _auctionIDs;
        internal StorageValue<BigInteger> _lastAuctionID;

        public MarketContract() : base()
        {
        }

        public void SellToken(Address from, string symbol, BigInteger tokenID, BigInteger price)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid base token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(token);
            var owner = ownerships.GetOwner(tokenID);
            Runtime.Expect(owner == from, "invalid owner");

            _lastAuctionID.Value++;
            var auctionID = _lastAuctionID.Value;

            Runtime.Expect(token.Transfer(ownerships, from, Runtime.Chain.Address, tokenID), "transfer failed");

            var auction = new MarketAuction(from, Timestamp.Now, Timestamp.Now + TimeSpan.FromDays(5), symbol, tokenID, price);
            _auctionMap.Set(auctionID, auction);
            _auctionIDs.Add(auctionID);
        }

        public void BuyToken(Address from, BigInteger auctionID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(_auctionMap.ContainsKey<BigInteger>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<BigInteger, MarketAuction>(auctionID);

            var token = Runtime.Nexus.FindTokenBySymbol(auction.Symbol);
            Runtime.Expect(token != null, "invalid base token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(token);
            var owner = ownerships.GetOwner(auction.TokenID);
            Runtime.Expect(owner == Runtime.Chain.Address, "invalid owner");

            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(from);
            Runtime.Expect(balance >= auction.Price, "not enough balance");

            Runtime.Expect(Runtime.Nexus.NativeToken.Transfer(balances, from, auction.Creator, auction.Price), "payment failed");
            Runtime.Expect(token.Transfer(ownerships, Runtime.Chain.Address, from, auction.TokenID), "transfer failed");

            _auctionMap.Remove<BigInteger>(auctionID);
            _auctionIDs.Remove(auctionID);
        }
            
        public BigInteger[] GetAuctionIDs()
        {
            return _auctionIDs.All<BigInteger>();
        }

        public MarketAuction GetAuctionInfo(BigInteger auctionID)
        {
            Runtime.Expect(_auctionMap.ContainsKey<BigInteger>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<BigInteger, MarketAuction>(auctionID);
            return auction;
        }
    }
}
