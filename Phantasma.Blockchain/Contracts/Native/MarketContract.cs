using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
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

        internal StorageMap _auctionMap; //<string, MarketAuction>
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

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "base token must be non-fungible");

            var ownerships = new OwnershipSheet(baseSymbol);
            var owner = ownerships.GetOwner(this.Storage, tokenID);
            Runtime.Expect(owner == from, "invalid owner");

            Runtime.Expect(Runtime.Nexus.TransferToken(Runtime, baseToken.Symbol, from, this.Address, tokenID), "transfer failed");

            var auction = new MarketAuction(from, Runtime.Time, endDate, baseSymbol, quoteSymbol, tokenID, price);
            var auctionID = baseSymbol + "." + tokenID;
            _auctionMap.Set(auctionID, auction);
            _auctionIDs.Add(auctionID);

            Runtime.Notify(EventKind.OrderCreated, from, new MarketEventData() { ID = tokenID, BaseSymbol = baseSymbol, QuoteSymbol = quoteSymbol, Price = price });
            Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = auction.BaseSymbol, value = tokenID });
        }

        public void BuyToken(Address from, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            Runtime.Expect(Runtime.Nexus.TokenExists(auction.BaseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(auction.BaseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = new OwnershipSheet(baseToken.Symbol);
            var owner = ownerships.GetOwner(this.Storage, auction.TokenID);
            Runtime.Expect(owner == this.Address, "invalid owner");

            if (auction.Creator != from)
            {
                Runtime.Expect(Runtime.Nexus.TokenExists(auction.QuoteSymbol), "invalid quote token");
                var quoteToken = Runtime.Nexus.GetTokenInfo(auction.QuoteSymbol);
                Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

                var balances = new BalanceSheet(quoteToken.Symbol);
                var balance = balances.Get(this.Storage, from);
                Runtime.Expect(balance >= auction.Price, "not enough balance");

                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, quoteToken.Symbol, from, auction.Creator, auction.Price), "payment failed");
            }

            Runtime.Expect(Runtime.Nexus.TransferToken(Runtime, baseToken.Symbol, this.Address, from, auction.TokenID), "transfer failed");

            _auctionMap.Remove<string>(auctionID);
            _auctionIDs.Remove(auctionID);

            if (auction.Creator == from)
            {
                Runtime.Notify(EventKind.OrderCancelled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = 0 });
            }
            else
            {
                Runtime.Notify(EventKind.TokenSend, from, new TokenEventData() { chainAddress = this.Address, symbol = auction.QuoteSymbol, value = auction.Price });
                Runtime.Notify(EventKind.TokenReceive, auction.Creator, new TokenEventData() { chainAddress = this.Address, symbol = auction.QuoteSymbol, value = auction.Price });

                Runtime.Notify(EventKind.OrderFilled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = auction.Price });
            }

            Runtime.Notify(EventKind.TokenReceive, from, new TokenEventData() { chainAddress = this.Address, symbol = auction.BaseSymbol, value = auction.TokenID });
        }

        public MarketAuction[] GetAuctions()
        {
            var ids = _auctionIDs.All<string>();
            var auctions = new MarketAuction[ids.Length];
            for (int i = 0; i < auctions.Length; i++)
            {
                auctions[i] = _auctionMap.Get<string, MarketAuction>(ids[i]);
            }
            return auctions;
        }

        public bool HasAuction(BigInteger tokenID)
        {
            return _auctionMap.ContainsKey<BigInteger>(tokenID);
        }

        public MarketAuction GetAuction(BigInteger tokenID)
        {
            Runtime.Expect(_auctionMap.ContainsKey<BigInteger>(tokenID), "invalid auction");
            var auction = _auctionMap.Get<BigInteger, MarketAuction>(tokenID);
            return auction;
        }
    }
}
