using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using Phantasma.VM;
using System;

namespace Phantasma.Blockchain.Contracts
{
    public enum TypeAuction
    {
        Fixed = 0,
        Schedule = 1,
        Reserve = 2,
        Dutch = 3,
    }           
    public struct MarketEventData
    {
        public string BaseSymbol;
        public string QuoteSymbol;
        public BigInteger ID;
        public BigInteger Price;
        public TypeAuction Type;
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
        public readonly BigInteger EndPrice;
        public readonly BigInteger ExtensionPeriod;
        public readonly TypeAuction Type;
        public readonly BigInteger ListingFee;
        public readonly Address ListingFeeAddress;
        public readonly BigInteger BuyingFee;
        public readonly Address BuyingFeeAddress;
        public readonly Address CurrentBidWinner;
        public MarketAuction(Address creator, Timestamp startDate, Timestamp endDate, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, BigInteger endPrice, BigInteger extensionPeriod, TypeAuction typeAuction, BigInteger listingFee, Address listingFeeAddress, BigInteger buyingFee, Address buyingFeeAddress, Address currentBidWinner)
        {
            Creator = creator;
            StartDate = startDate;
            EndDate = endDate;
            BaseSymbol = baseSymbol;
            QuoteSymbol = quoteSymbol;
            TokenID = tokenID;
            Price = price;
            EndPrice = endPrice;
            ExtensionPeriod = extensionPeriod;
            Type = typeAuction;
            ListingFee = listingFee;
            ListingFeeAddress = listingFeeAddress;
            BuyingFee = buyingFee;
            BuyingFeeAddress = buyingFeeAddress;
            CurrentBidWinner = currentBidWinner;
        }
    }

    public sealed class MarketContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Market;

        private const int minutesPerDay = 86400 / 24 / 60;
        private const int fiveMinutes = 86400 / 24 / 12;
        private const int oneDay = 86400;

        internal StorageMap _auctionMap; //<string, MarketAuction>
        internal StorageMap _auctionIds; //<string, MarketAuction>

        public MarketContract() : base()
        {
        }

        public void EditAuction(Address from, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, BigInteger endPrice, Timestamp startDate, Timestamp endDate, BigInteger extensionPeriod) // new method
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

            var nft = Runtime.ReadToken(baseSymbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "token not currently in this chain");
            var marketAddress = SmartContract.GetAddressForNative(NativeContractKind.Market);
            Runtime.Expect(nft.CurrentOwner == marketAddress, "invalid owner");

            var auctionID = baseSymbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");

            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            if (auction.Type != TypeAuction.Fixed) // prevent edit already started auctions
            {
                Runtime.Expect(auction.StartDate > Runtime.Time, "EditAuction can only be used before listing start");
            }

            if (price == 0) 
            {
                price = auction.Price;
            }

            if (endPrice == 0) 
            {
                endPrice = auction.EndPrice;
            }

            if (startDate == 0) 
            {
                startDate = auction.StartDate;
            }

            if (endDate == 0) 
            {
                endDate = auction.EndDate;
            }
            Runtime.Expect(endDate > startDate, "invalid end date");

            if (extensionPeriod == 0 || auction.Type == TypeAuction.Fixed) 
            {
                extensionPeriod = auction.ExtensionPeriod;
            }

            if (auction.Type == TypeAuction.Schedule || auction.Type == TypeAuction.Reserve)
            {
                Runtime.Expect(extensionPeriod <= oneDay, "extensionPeriod must be <= 1 day");
                Runtime.Expect(extensionPeriod >= fiveMinutes, "extensionPeriod must be >= 5 minutes");
            }

            var auctionNew = new MarketAuction(from, startDate, endDate, baseSymbol, quoteSymbol, tokenID, price, endPrice, extensionPeriod, auction.Type, auction.ListingFee, auction.ListingFeeAddress, auction.BuyingFee, auction.BuyingFeeAddress, auction.CurrentBidWinner);
            _auctionMap.Set(auctionID, auctionNew);

            Runtime.Notify(EventKind.OrderCancelled, auctionNew.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = 0, Type = auctionNew.Type });
            Runtime.Notify(EventKind.OrderCreated, auctionNew.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = price, Type = auctionNew.Type });
        }

        public void ListToken(Address from, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, BigInteger endPrice, Timestamp startDate, Timestamp endDate, BigInteger extensionPeriod, BigInteger typeAuction, BigInteger listingFee, Address listingFeeAddress) // new method
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(startDate > Runtime.Time, "invalid start date");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "base token must be non-fungible");

            var nft = Runtime.ReadToken(baseSymbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "token not currently in this chain");
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            Runtime.Expect(listingFee <= 5, "listingFee has to be <= 5%");

            TypeAuction type;

            if (typeAuction == 1) // Schedule
            {
                Runtime.Expect(endDate > startDate, "end date must be later than start date");
                Runtime.Expect(extensionPeriod <= oneDay, "extensionPeriod must be <= 1 day");
                Runtime.Expect(extensionPeriod >= fiveMinutes, "extensionPeriod must be >= 5 minutes");
                var maxAllowedDate = Runtime.Time + TimeSpan.FromDays(30);
                Runtime.Expect(endDate <= maxAllowedDate, "end date is too distant, max: " + maxAllowedDate + ", received: " + endDate);
                endPrice = 0;
                type = TypeAuction.Schedule;
            }
            else if (typeAuction == 2) // Reserve
            {
                Runtime.Expect(extensionPeriod <= oneDay, "extensionPeriod must be <= 1 day");
                Runtime.Expect(extensionPeriod >= fiveMinutes, "extensionPeriod must be >= 5 minutes");
                endPrice = 0;
                startDate = 0;
                endDate = 0;
                type = TypeAuction.Reserve;
            }
            else if (typeAuction == 3) // Dutch
            {
                Runtime.Expect(endDate > startDate, "end date must be later than start date");
                Runtime.Expect(endPrice < price, "final price has to be lower than initial price");
                var maxAllowedDate = Runtime.Time + TimeSpan.FromDays(30);
                Runtime.Expect(endDate <= maxAllowedDate, "end date is too distant, max: " + maxAllowedDate + ", received: " + endDate);
                extensionPeriod = 0;
                type = TypeAuction.Dutch;
            }
            else // Default - Fixed
            {
                if (endDate != 0)
                {
                    Runtime.Expect(endDate > Runtime.Time, "invalid end date");
                }
                endPrice = 0;
                extensionPeriod = 0;
                type = TypeAuction.Fixed;
            }

            Runtime.TransferToken(baseToken.Symbol, from, this.Address, tokenID);

            var auction = new MarketAuction(from, startDate, endDate, baseSymbol, quoteSymbol, tokenID, price, endPrice, extensionPeriod, type, listingFee, listingFeeAddress, 0, Address.Null, Address.Null);
            var auctionID = baseSymbol + "." + tokenID;
            _auctionMap.Set(auctionID, auction);
            _auctionIds.Set(auctionID, auctionID);

            Runtime.Notify(EventKind.OrderCreated, from, new MarketEventData() { ID = tokenID, BaseSymbol = baseSymbol, QuoteSymbol = quoteSymbol, Price = price, Type = type });
        }
        public void BidToken(Address from, string symbol, BigInteger tokenID, BigInteger price, BigInteger buyingFee, Address buyingFeeAddress) // new method
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            Runtime.Expect(buyingFee <= 5, "buyingFee has to be <= 5%");

            Runtime.Expect(auction.StartDate > Runtime.Time, "you can not bid on an auction which has not started");

            MarketAuction auctionNew;

            if (Runtime.Time >= auction.EndDate && auction.EndDate != 0 && auction.Type != TypeAuction.Dutch) // if auction ended trigger sale end
            {
                EndSaleInternal(auction.CurrentBidWinner, auction.BaseSymbol, auction.TokenID, auction, auction.ListingFee, auction.ListingFeeAddress, auction.BuyingFee, auction.BuyingFeeAddress);
                if (auction.Type == TypeAuction.Fixed)
                {
                    Runtime.Notify(EventKind.OrderFilled, auction.Creator, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = auction.Price, Type = auction.Type });
                }
                else
                {
                    Runtime.Notify(EventKind.OrderFilled, auction.Creator, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = auction.EndPrice, Type = auction.Type }); 
                }
            }
            else
            {
                if (auction.Type == TypeAuction.Schedule)
                {
                    Runtime.Expect(price > auction.Price, "bid has to be higher than last bid");
                    Runtime.Expect(from != auction.CurrentBidWinner, "you can not outbid yourself");

                    var timeLeft = Runtime.Time + TimeSpan.FromHours(1);
                    Timestamp endDateNew;
                    
                    if ((auction.EndDate - Runtime.Time) < timeLeft) // extend timer if < 1 hour left
                    {
                        endDateNew = Runtime.Time + TimeSpan.FromSeconds((double) auction.ExtensionPeriod);
                    }
                    else 
                    {
                        endDateNew = auction.EndDate;
                    }

                    // calculate listing & buying & refund fees
                    BigInteger combinedFees = 0;
                    BigInteger combinedRefund = 0;
                    if (auction.ListingFee != 0)
                    {
                        var listFee = price * auction.ListingFee / 100;
                        combinedFees += listFee;
                        var listFeeRefund = auction.EndPrice * auction.ListingFee / 100;
                        combinedRefund += listFeeRefund;
                    }
                    if (buyingFee != 0)
                    {
                        var buyFee = price * buyingFee / 100;
                        combinedFees += buyFee;
                    }
                    if (auction.BuyingFee != 0)
                    {
                        var buyFeeRefund = auction.EndPrice * auction.BuyingFee / 100;
                        combinedRefund += buyFeeRefund;
                    }
                    combinedFees += price;
                    combinedRefund += auction.EndPrice;

                    // transfer price + listing + buying fees to contract
                    Runtime.TransferTokens(auction.QuoteSymbol, from, this.Address, combinedFees);

                    // refund old bid amount + listing + buying fees to previous current winner if any
                    if (auction.CurrentBidWinner != Address.Null)
                    {
                        Runtime.TransferTokens(auction.QuoteSymbol, this.Address, auction.CurrentBidWinner, combinedRefund);
                    }

                    auctionNew = new MarketAuction(auction.Creator, auction.StartDate, endDateNew, auction.BaseSymbol, auction.QuoteSymbol, auction.TokenID, auction.Price, price, auction.ExtensionPeriod, auction.Type, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress, from);
                    _auctionMap.Set(auctionID, auctionNew);
                    Runtime.Notify(EventKind.OrderBid, auction.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = price, Type = auctionNew.Type });
                }

                if (auction.Type == TypeAuction.Reserve)
                {
                    Timestamp endDateNew;

                    if (auction.StartDate == 0) // if reserve auction not started
                    {
                        Runtime.Expect(price >= auction.Price, "bid has to be higher than reserve price");

                        endDateNew = Runtime.Time + TimeSpan.FromDays(1);
                    }
                    else // if reserve auction already started
                    {
                        Runtime.Expect(price >= auction.Price, "bid has to be higher than last bid");
                        Runtime.Expect(from != auction.CurrentBidWinner, "you can not outbid yourself");

                        var timeLeft = Runtime.Time + TimeSpan.FromHours(1);

                        if (auction.ExtensionPeriod != 0 && auction.EndDate < timeLeft) // extend timer if < 1 hour left
                        {
                            endDateNew = Runtime.Time + TimeSpan.FromSeconds((double) auction.ExtensionPeriod);
                        }
                        else 
                        {
                            endDateNew = auction.EndDate;
                        }
                    }

                    // calculate listing & buying fees then transfer them to contract
                    BigInteger combinedFees = 0;
                    if (auction.ListingFee != 0)
                    {
                        var listFee = price * auction.ListingFee / 100;
                        combinedFees += listFee;
                    }
                    if (buyingFee != 0)
                    {
                        var buyFee = price * buyingFee / 100;
                        combinedFees += buyFee;
                    }
                    combinedFees += price;
                    
                    Runtime.TransferTokens(auction.QuoteSymbol, from, this.Address, combinedFees);

                    // refund old bid amount to previous current winner if any
                    if (auction.CurrentBidWinner != Address.Null)
                    {
                        Runtime.TransferTokens(auction.QuoteSymbol, this.Address, auction.CurrentBidWinner, auction.EndPrice);
                    }

                    auctionNew = new MarketAuction(auction.Creator, auction.StartDate, endDateNew, auction.BaseSymbol, auction.QuoteSymbol, auction.TokenID, auction.Price, price, auction.ExtensionPeriod, auction.Type, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress, from);
                    _auctionMap.Set(auctionID, auctionNew);
                    Runtime.Notify(EventKind.OrderBid, auctionNew.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = price, Type = auctionNew.Type });

                }

                if (auction.Type == TypeAuction.Dutch)
                {
                    Runtime.Expect(price < auction.Price, "bid has to be lower than initial price");

                    var priceDiff = auction.Price - auction.EndPrice;
                    var timeDiff = auction.EndDate - auction.StartDate;
                    var minutesSinceStart = timeDiff / minutesPerDay; // unsure
                    var priceDiffPerMinute = priceDiff / timeDiff / minutesPerDay; // unsure
                    var currentPrice = auction.Price - (minutesSinceStart * priceDiffPerMinute); // unsure
                    if (currentPrice < auction.EndPrice)
                    {
                        currentPrice = auction.EndPrice;
                    }

                    // calculate listing & buying fees then transfer them to contract
                    BigInteger combinedFees = 0;
                    if (auction.ListingFee != 0)
                    {
                        var listFee = currentPrice * auction.ListingFee / 100;
                        combinedFees += listFee;
                    }
                    if (buyingFee != 0)
                    {
                        var buyFee = currentPrice * buyingFee / 100;
                        combinedFees += buyFee;
                    }
                    combinedFees += currentPrice;
                    
                    Runtime.TransferTokens(auction.QuoteSymbol, from, this.Address, combinedFees);

                    auctionNew = new MarketAuction(auction.Creator, auction.StartDate, auction.EndDate, auction.BaseSymbol, auction.QuoteSymbol, auction.TokenID, auction.Price, currentPrice, auction.ExtensionPeriod, auction.Type, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress, from);
                    _auctionMap.Set(auctionID, auctionNew);
                    EndSaleInternal(from, auction.BaseSymbol, auction.TokenID, auctionNew, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress);
                    Runtime.Notify(EventKind.OrderFilled, auctionNew.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = currentPrice, Type = auctionNew.Type });
                }

                if (auction.Type == TypeAuction.Fixed)
                {
                    // calculate listing & buying fees then transfer them to contract
                    BigInteger combinedFees = 0;
                    if (auction.ListingFee != 0)
                    {
                        var listFee = auction.Price * auction.ListingFee / 100;
                        combinedFees += listFee;
                    }
                    if (buyingFee != 0)
                    {
                        var buyFee = auction.Price * buyingFee / 100;
                        combinedFees += buyFee;
                    }
                    combinedFees += auction.Price;
                    
                    Runtime.TransferTokens(auction.QuoteSymbol, from, this.Address, combinedFees);

                    auctionNew = new MarketAuction(auction.Creator, auction.StartDate, auction.EndDate, auction.BaseSymbol, auction.QuoteSymbol, auction.TokenID, auction.Price, 0, auction.ExtensionPeriod, auction.Type, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress, from);
                    _auctionMap.Set(auctionID, auctionNew);
                    EndSaleInternal(from, auction.BaseSymbol, auction.TokenID, auctionNew, auction.ListingFee, auction.ListingFeeAddress, buyingFee, buyingFeeAddress);
                    Runtime.Notify(EventKind.OrderFilled, auctionNew.Creator, new MarketEventData() { ID = auctionNew.TokenID, BaseSymbol = auctionNew.BaseSymbol, QuoteSymbol = auctionNew.QuoteSymbol, Price = auctionNew.Price, Type = auctionNew.Type });
                }
            }

        }


        public void SellToken(Address from, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, Timestamp endDate) // original method
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(endDate > Runtime.Time, "invalid end date");

            var maxAllowedDate = Runtime.Time + TimeSpan.FromDays(30);
            Runtime.Expect(endDate <= maxAllowedDate, "end date is too distant, max: " + maxAllowedDate + ", received: " + endDate);

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "base token must be non-fungible");

            var nft = Runtime.ReadToken(baseSymbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "token not currently in this chain");
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            Runtime.TransferToken(baseToken.Symbol, from, this.Address, tokenID);

            var auction = new MarketAuction(from, Runtime.Time, endDate, baseSymbol, quoteSymbol, tokenID, price, 0, 0, TypeAuction.Fixed, 0, Address.Null, 0, Address.Null, Address.Null); // differ from original
            var auctionID = baseSymbol + "." + tokenID;
            _auctionMap.Set(auctionID, auction);
            _auctionIds.Set(auctionID, auctionID);

            Runtime.Notify(EventKind.OrderCreated, from, new MarketEventData() { ID = tokenID, BaseSymbol = baseSymbol, QuoteSymbol = quoteSymbol, Price = price, Type = TypeAuction.Fixed }); // differ from original
        }

        public void BuyToken(Address from, string symbol, BigInteger tokenID) // original method
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            Runtime.Expect(auction.Type == TypeAuction.Fixed, "BuyToken only supports fixed price listings"); // differ from original

            if (auction.Creator == from) // dev branch changes
            {
                Runtime.Expect(Runtime.ProtocolVersion < 5, "seller and buyer are the same, use CancelSale instead");
                CancelSale(symbol, tokenID);
                return;
            }

            EndSaleInternal(from, symbol, tokenID, auction, 0, Address.Null, 0, Address.Null); // differ from original

            Runtime.Notify(EventKind.OrderFilled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = auction.Price, Type = auction.Type }); // differ from original
        }

        public void CancelSale(string symbol, BigInteger tokenID) // original method
        {
            var auctionID = symbol + "." + tokenID;

            Runtime.Expect(_auctionMap.ContainsKey<string>(auctionID), "invalid auction");
            var auction = _auctionMap.Get<string, MarketAuction>(auctionID);

            var from = auction.Creator;

            if (Runtime.Time < auction.EndDate)
            {
                Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            }

            if (auction.Type == TypeAuction.Reserve)
            {
                Runtime.Expect(auction.EndDate == 0, "reserve auction can not be cancelled once it started");
            }
            else
            {
                Runtime.Expect(auction.Type != TypeAuction.Fixed && Runtime.Time > auction.StartDate && Runtime.Time < auction.EndDate, "auction can not be cancelled once it started");
            }

            EndSaleInternal(from, symbol, tokenID, auction, 0, Address.Null, 0, Address.Null); // differ from original
            Runtime.Notify(EventKind.OrderCancelled, from, new MarketEventData() { ID = auction.TokenID, BaseSymbol = auction.BaseSymbol, QuoteSymbol = auction.QuoteSymbol, Price = 0, Type = auction.Type }); // differ from original
        }

        private void EndSaleInternal(Address from, string symbol, BigInteger tokenID, MarketAuction auction, BigInteger listingFee, Address listingFeeAddress, BigInteger buyingFee, Address buyingFeeAddress) // original method
        {
            Runtime.Expect(Runtime.TokenExists(auction.BaseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(auction.BaseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var nft = Runtime.ReadToken(symbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "token not currently in this chain");
            Runtime.Expect(nft.CurrentOwner == this.Address, "invalid owner");

            // if not a cancellation
            if (auction.Creator != from)
            {
                Runtime.Expect(Runtime.TokenExists(auction.QuoteSymbol), "invalid quote token");
                var quoteToken = Runtime.GetToken(auction.QuoteSymbol);
                Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "quote token must be fungible");

                var balance = Runtime.GetBalance(quoteToken.Symbol, from);
                Runtime.Expect(balance >= auction.Price, $"not enough {quoteToken.Symbol} balance at {from.Text}");

                var finalAmount = auction.Price;

                // if new auctions type, use EndPrice
                if (auction.Type != TypeAuction.Fixed) // differ from original
                {
                    finalAmount = auction.EndPrice;
                }

                // handle royalties
                if (Runtime.ProtocolVersion >= 4)
                {
                    var nftSymbol = auction.BaseSymbol;
                    var nftData = Runtime.ReadToken(nftSymbol, tokenID);
                    var series = Runtime.GetTokenSeries(nftSymbol, nftData.SeriesID);

                    var royaltyProperty = new ContractMethod("getRoyalties", VMType.Number, -1);

                    if (series.ABI.Implements(royaltyProperty))
                    {
                        var nftRoyalty = Runtime.CallNFT(nftSymbol, nftData.SeriesID, royaltyProperty, tokenID).AsNumber();
                        if (nftRoyalty > 50)
                        {
                            nftRoyalty = 50; // we don't allow more than 50% royalties fee
                        }
                        var royaltyFee = finalAmount * nftRoyalty / 100;
                        Runtime.TransferTokens(quoteToken.Symbol, from, nftData.Creator, royaltyFee);
                        finalAmount -= royaltyFee;
                    }
                }

                // transfer sale amount
                Runtime.TransferTokens(quoteToken.Symbol, this.Address, auction.Creator, finalAmount);

                // transfer listing fees
                if (auction.ListingFee != 0)
                {
                    var listFee = finalAmount * auction.ListingFee / 100;
                    Runtime.TransferTokens(quoteToken.Symbol, this.Address, listingFeeAddress, listFee);
                }

                // transfer buying fees
                if (auction.BuyingFee != 0)
                {
                    var buyFee = finalAmount * auction.BuyingFee / 100;
                    Runtime.TransferTokens(quoteToken.Symbol, this.Address, buyingFeeAddress, buyFee);
                }

            }

            // send nft to buyer
            Runtime.TransferToken(baseToken.Symbol, this.Address, from, auction.TokenID);

            var auctionID = symbol + "." + tokenID;
            _auctionMap.Remove<string>(auctionID);
            _auctionIds.Remove<string>(auctionID);
        }

        public MarketAuction[] GetAuctions()
        {
            var ids = _auctionIds.AllValues<string>();
            var auctions = new MarketAuction[ids.Length];
            for (int i = 0; i < auctions.Length; i++)
            {
                auctions[i] = _auctionMap.Get<string, MarketAuction>(ids[i]);
            }
            return auctions;
        }
        public bool HasAuction(string symbol, BigInteger tokenID)
        {
            var auctionID = symbol + "." + tokenID;
            return _auctionMap.ContainsKey<string>(auctionID);
        }

        public MarketAuction GetAuction(BigInteger tokenID)
        {
            Runtime.Expect(_auctionMap.ContainsKey<BigInteger>(tokenID), "invalid auction");
            var auction = _auctionMap.Get<BigInteger, MarketAuction>(tokenID);
            return auction;
        }
    }
}
