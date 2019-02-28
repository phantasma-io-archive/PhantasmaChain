using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct MarketEventData
    {
        public string BaseSymbol;
        public string QuoteSymbol;
        public BigInteger ID;
        public BigInteger Price;
    }

    public struct MarketAuction
    {
        public readonly Address Creator;
        public readonly Timestamp StartDate;
        public readonly Timestamp EndDate;
        public readonly string BaseSymbol;
        public readonly string QuoteSymbol;
        public readonly BigInteger TokenID;
        public readonly BigInteger Price;

        public MarketAuction(Address creator, Timestamp startDate, Timestamp endDate, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price)
        {
            Creator = creator;
            StartDate = startDate;
            EndDate = endDate;
            BaseSymbol = baseSymbol;
            QuoteSymbol = quoteSymbol;
            TokenID = tokenID;
            Price = price;
        }
    }

    public sealed class MarketContract : SmartContract
    {
        public override string Name => "market";

        internal StorageMap _auctionMap; //<string, Collection<MarketAuction>>
        internal StorageList _auctionIDs;

        public MarketContract() : base()
        {
        }

        public void SellToken(Address from, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, Timestamp endDate)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(endDate > Runtime.Time, "invalid end date");

            var maxAllowedDate = Runtime.Time + TimeSpan.FromDays(30);
            Runtime.Expect(endDate <= maxAllowedDate, "end date is too distant");

            var quoteToken = Runtime.Nexus.FindTokenBySymbol(quoteSymbol);
            Runtime.Expect(quoteToken != null, "invalid quote token");
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

            var baseToken = Runtime.Nexus.FindTokenBySymbol(baseSymbol);
            Runtime.Expect(baseToken != null, "invalid base token");
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "base token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(baseToken);
            var owner = ownerships.GetOwner(this.Storage, tokenID);
            Runtime.Expect(owner == from, "invalid owner");

            Runtime.Expect(baseToken.Transfer(this.Storage, ownerships, from, Runtime.Chain.Address, tokenID), "transfer failed");

            var auction = new MarketAuction(from, Runtime.Time, endDate, baseSymbol, quoteSymbol, tokenID, price);
            var auctionID = baseSymbol + "." + tokenID;
            _auctionMap.Set(auctionID, auction);
            _auctionIDs.Add(auctionID);

            var nft = this.Runtime.Nexus.GetNFT(baseToken, tokenID);
            nft.CurrentChain = Runtime.Chain.Address;
            nft.CurrentOwner = Runtime.Chain.Address;

            Runtime.Notify(EventKind.AuctionCreated, from, new MarketEventData() { ID = tokenID, BaseSymbol = baseSymbol, QuoteSymbol = quoteSymbol, Price = price });
        }

        public void BuyToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            var baseToken = Runtime.Nexus.FindTokenBySymbol(auction.BaseSymbol);
            Runtime.Expect(baseToken != null, "invalid base token");
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(baseToken);
            var owner = ownerships.GetOwner(this.Storage, auction.TokenID);
            Runtime.Expect(owner == Runtime.Chain.Address, "invalid owner");

            if (auction.Creator != from)
            {
                var quoteToken = Runtime.Nexus.FindTokenBySymbol(auction.QuoteSymbol);
                Runtime.Expect(quoteToken != null, "invalid quote token");
                Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

                var balances = Runtime.Chain.GetTokenBalances(quoteToken);
                var balance = balances.Get(this.Storage, from);
                Runtime.Expect(balance >= auction.Price, "not enough balance");

                Runtime.Expect(quoteToken.Transfer(this.Storage, balances, from, auction.Creator, auction.Price), "payment failed");
            }

            Runtime.Expect(baseToken.Transfer(this.Storage, ownerships, Runtime.Chain.Address, from, auction.TokenID), "transfer failed");

            _auctionMap.Remove<string>(auctionID);
            _auctionIDs.Remove(auctionID);

            var nft = this.Runtime.Nexus.GetNFT(baseToken, tokenID);
            nft.CurrentChain = Runtime.Chain.Address;
            nft.CurrentOwner = from;

            if (auction.Creator == from)
            {
                Runtime.Notify(EventKind.AuctionCancelled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = 0 });
            }
            else
            {
                Runtime.Notify(EventKind.AuctionFilled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = auction.Price });
            }
        }

        public MarketAuction[] GetAuctions()
        {
            var ids = _auctionIDs.All<string>();
            var auctions = new MarketAuction[ids.Length];
            for (int i=0; i<auctions.Length; i++)
            {
                auctions[i] = _auctionMap.Get<string, MarketAuction>(ids[i]);
            }
            return auctions;
        }

        public MarketAuction GetAuctionInfo(BigInteger auctionID)
        {
            Runtime.Expect(_auctionMap.ContainsKey<BigInteger>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<BigInteger, MarketAuction>(auctionID);
            return auction;
        }
    }
}
