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
        public string Symbol;
        public BigInteger ID;
        public BigInteger Price;
    }

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
        public override string Name => "market";

        internal StorageMap _auctionMap; //<string, Collection<MarketAuction>>
        internal StorageList _auctionIDs;

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

            Runtime.Expect(token.Transfer(ownerships, from, Runtime.Chain.Address, tokenID), "transfer failed");

            var auction = new MarketAuction(from, Timestamp.Now, Timestamp.Now + TimeSpan.FromDays(5), symbol, tokenID, price);
            var auctionID = symbol + "." + tokenID;
            _auctionMap.Set(auctionID, auction);
            _auctionIDs.Add(auctionID);

            Runtime.Notify(EventKind.AuctionCreated, from, new MarketEventData() { ID = tokenID, Symbol = symbol, Price = price });
        }

        public void BuyToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            var token = Runtime.Nexus.FindTokenBySymbol(auction.Symbol);
            Runtime.Expect(token != null, "invalid base token");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = Runtime.Chain.GetTokenOwnerships(token);
            var owner = ownerships.GetOwner(auction.TokenID);
            Runtime.Expect(owner == Runtime.Chain.Address, "invalid owner");

            if (auction.Creator != from)
            {
                var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
                var balance = balances.Get(from);
                Runtime.Expect(balance >= auction.Price, "not enough balance");

                Runtime.Expect(Runtime.Nexus.NativeToken.Transfer(balances, from, auction.Creator, auction.Price), "payment failed");
            }

            Runtime.Expect(token.Transfer(ownerships, Runtime.Chain.Address, from, auction.TokenID), "transfer failed");

            _auctionMap.Remove<string>(auctionID);
            _auctionIDs.Remove(auctionID);

            if (auction.Creator == from)
            {
                Runtime.Notify(EventKind.AuctionCancelled, from, new MarketEventData() { ID = auction.TokenID, Symbol = auction.Symbol, Price = 0 });
            }
            else
            {
                Runtime.Notify(EventKind.AuctionFilled, from, new MarketEventData() { ID = auction.TokenID, Symbol = auction.Symbol, Price = auction.Price });
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
