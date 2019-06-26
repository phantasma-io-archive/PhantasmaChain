using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using static Phantasma.Blockchain.Contracts.Native.ExchangeOrderSide;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    [Flags]
    public enum ExchangeOrderType
    {
        Limit = 0,              //normal limit order
        ImmediateOrCancel = 1,  //any unfulfilled part of the order gets cancelled if not immediately fulfilled
        Market = 2,             //an IoC order that has no boundaries 
        //TODO: FillOrKill = 4,         //Either gets 100% fulfillment or it gets cancelled , no partial fulfillments like in IoC order types
    }

    public struct ExchangeOrder
    {
        public readonly BigInteger Uid;
        public readonly Timestamp Timestamp;
        public readonly Address Creator;

        public readonly BigInteger Amount;
        public readonly string BaseSymbol;

        public readonly BigInteger Price;
        public readonly string QuoteSymbol;

        public readonly ExchangeOrderSide Side;

        public ExchangeOrder(BigInteger uid, Timestamp timestamp, Address creator, BigInteger amount, string baseSymbol, BigInteger price, string quoteSymbol, ExchangeOrderSide side)
        {
            Uid = uid;
            Timestamp = timestamp;
            Creator = creator;

            Amount = amount;
            BaseSymbol = baseSymbol;

            Price = price;
            QuoteSymbol = quoteSymbol;

            Side = side;
        }

        public ExchangeOrder(ExchangeOrder order, BigInteger newOrderSize, Timestamp newTimestamp)
        {
            Uid = order.Uid;
            Timestamp = newTimestamp;
            Creator = order.Creator;

            Amount = newOrderSize;
            BaseSymbol = order.BaseSymbol;

            Price = order.Price;
            QuoteSymbol = order.QuoteSymbol;

            Side = order.Side;
        }
    }

    public struct TokenSwap
    {
        public Address buyer;
        public Address seller;
        public string baseSymbol;
        public string quoteSymbol;
        public BigInteger value;
        public BigInteger price;
    }

    public sealed class ExchangeContract : SmartContract
    {
        public override string Name => "exchange";

        internal StorageList _availableBases; // string
        internal StorageList _availableQuotes; // string
        internal StorageMap _orders; //<string, List<Order>>
        internal StorageMap _orderMap; //<uid, string> // maps orders ids to pairs
        internal StorageMap _fills; //<uid, BigInteger>
        internal StorageMap _escrows; //<uid, BigInteger>

        public ExchangeContract() : base()
        {
        }

        private string BuildOrderKey(ExchangeOrderSide side, string baseSymbol, string quoteSymbol) => $"{side}_{baseSymbol}_{quoteSymbol}";

        public BigInteger GetMinimumSymbolQuantity(BigInteger tokenDecimals) => BigInteger.Pow(10, tokenDecimals / 2);
        private BigInteger GetMinimumSymbolQuantity(TokenInfo token) => GetMinimumSymbolQuantity(token.Decimals);

        public void OpenMarketOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger orderSize, ExchangeOrderSide side)
        {
            //OpenLimitOrder(from, baseSymbol, quoteSymbol, orderSize, );
        }

        /// <summary>
        /// Creates a limit order on the exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="baseSymbol">For SOUL/KCAL pair, SOUL would be the base symbol</param>
        /// <param name="quoteSymbol">For SOUL/KCAL pair, KCAL would be the quote symbol</param>
        /// <param name="orderSize">Amount of base symbol tokens the user wants to buy/sell</param>
        /// <param name="price">Amount of quote symbol tokens the user wants to pay/receive per unit of base symbol tokens</param>
        /// <param name="side">If the order is a buy or sell order</param>
        /// <param name="IoC">"Immediate or Cancel" flag: if true, requires any unfulfilled parts of the order to be cancelled immediately after a single attempt at fulfilling it.</param>
        public void OpenLimitOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger orderSize, BigInteger price, ExchangeOrderSide side, bool IoC)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(baseSymbol != quoteSymbol, "invalid base/quote pair");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(orderSize >= GetMinimumSymbolQuantity(baseToken), "order size is not sufficient");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(price >= GetMinimumSymbolQuantity(quoteToken), "order price is not sufficient");

            //var tokenABI = Chain.FindABI(NativeABI.Token);
            //Runtime.Expect(baseTokenContract.ABI.Implements(tokenABI));

            var uid = Runtime.Chain.GenerateUID(this.Storage);
            Runtime.Expect(uid >= 0, "Generated an invalid UID");

            //--------------
            //perform escrow
            string orderEscrowSymbol = CalculateEscrowSymbol(baseToken, quoteToken, side);
            BigInteger orderEscrowAmount = CalculateEscrowAmount(orderSize, price, baseToken, quoteToken, side);
            BigInteger orderEscrowUsage = 0;

            //BigInteger baseTokensUnfilled = orderSize;


            var balances = new BalanceSheet(orderEscrowSymbol);
            var balance = balances.Get(this.Storage, from);
            Runtime.Expect(balance >= orderEscrowAmount, "not enough balance");

            Runtime.Expect(Runtime.Nexus.TransferTokens(orderEscrowSymbol, this.Storage, Runtime.Chain, from, Runtime.Chain.Address, orderEscrowAmount), "transfer failed");
            //------------

            var order = new ExchangeOrder(uid, Runtime.Time, from, orderSize, baseSymbol, price, quoteSymbol, side);
            Runtime.Notify(EventKind.OrderCreated, from, uid);

            var key = BuildOrderKey(side, quoteSymbol, baseSymbol);
            StorageList orderList = _orders.Get<string, StorageList>(key);
            var orderIndex = orderList.Add<ExchangeOrder>(order);
            _orderMap.Set<BigInteger, string>(uid, key);

            
            var makerSide = side == Buy ? Sell : Buy;
            var makerKey = BuildOrderKey(makerSide, quoteSymbol, baseSymbol);
            var makerOrders = _orders.Get<string, StorageList>(makerKey);

            do
            {
                int bestIndex = -1;
                BigInteger bestPrice = 0;
                Timestamp bestPriceTimestamp = 0;

                var makerOrdersCount = makerOrders.Count();
                for (int i=0; i<makerOrdersCount; i++)
                {
                    var makerOrder = makerOrders.Get<ExchangeOrder>(i);

                    if (side == Buy)
                    {
                        if (makerOrder.Price > order.Price) // too expensive, we wont buy at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price < bestPrice || (makerOrder.Price == bestPrice && makerOrder.Timestamp < bestPriceTimestamp))
                        {
                            bestIndex = i;
                            bestPrice = makerOrder.Price;
                            bestPriceTimestamp = makerOrder.Timestamp;
                        }
                    }
                    else
                    {
                        if (makerOrder.Price < order.Price) // too cheap, we wont sell at this price
                        {
                            continue;
                        }

                        if (bestIndex == -1 || makerOrder.Price > bestPrice || (makerOrder.Price == bestPrice && makerOrder.Timestamp < bestPriceTimestamp))
                        {
                            bestIndex = i;
                            bestPrice = makerOrder.Price;
                            bestPriceTimestamp = makerOrder.Timestamp;
                        }
                    }
                }

                if (bestIndex >= 0)
                {

                    /*
                    var other = otherOrders.Get<ExchangeOrder>(bestIndex);
                    var otherFilled = _fills.Get<BigInteger, BigInteger>(other.Uid);
                    var otherUnfilled = other.Amount - otherFilled;
                    
                    // pick the smallest of both unfilled amounts
                    BigInteger filledAmount = otherUnfilled < baseTokensUnfilled ? otherUnfilled : baseTokensUnfilled;

                    baseTokensUnfilled -= filledAmount;
                    otherFilled += filledAmount;

                    var quoteAmount = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(filledAmount, baseToken.Decimals) * UnitConversion.ToDecimal(other.Price, quoteToken.Decimals), quoteToken.Decimals);

                    if (side == Sell)
                    {
                        Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, other.Creator, filledAmount);
                        Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, order.Creator, quoteAmount);

                        Runtime.Notify(EventKind.TokenReceive, other.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = filledAmount });
                        Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = quoteAmount });

                        escrowUsage += filledAmount;
                    }
                    else
                    {
                        Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, order.Creator, filledAmount);
                        Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, other.Creator, quoteAmount);

                        Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = filledAmount });
                        Runtime.Notify(EventKind.TokenReceive, other.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = quoteAmount });

                        escrowUsage += quoteAmount;
                    }

                    Runtime.Notify(EventKind.OrderFilled, order.Creator, uid);
                    Runtime.Notify(EventKind.OrderFilled, other.Creator, other.Uid);

                    if (otherFilled >= other.Amount)
                    {
                        otherOrders.RemoveAt<ExchangeOrder>(bestIndex);
                        _orderMap.Remove<BigInteger>(other.Uid);
                        _fills.Remove<BigInteger>(other.Uid);

                        Runtime.Expect(_escrows.ContainsKey<BigInteger>(other.Uid), "An orderbook entry must have registered escrow");

                        var otherEscrow = _escrows.Get<BigInteger, BigInteger>(other.Uid);
                        var otherEscrowSymbol = side == Sell ? quoteSymbol : baseSymbol;
                        Runtime.Nexus.TransferTokens(otherEscrowSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, order.Creator, otherEscrow);
                        Runtime.Notify(EventKind.TokenReceive, other.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = otherEscrowSymbol, value = otherEscrow});

                        Runtime.Notify(EventKind.OrderClosed, other.Creator, other.Uid);
                    }
                    else
                    {
                        _fills.Set<BigInteger, BigInteger>(other.Uid, otherFilled);

                        //TODO calculate how much escrow changed and update _escrows with other.uid as key

                        //TODO optimization, if filledAmount = orderUnfilled break here, would this be correct?
                    }
                    */
                    //since order "uid" has found a match, the creator of this order will be a taker as he will remove liquidity from the market
                    //and the creator of the "bestIndex" order is the maker as he is providing liquidity to the taker
                    var takerOrder = order;
                    var takerAvailableEscrow = orderEscrowAmount - orderEscrowUsage;
                    var takerEscrowUsage = BigInteger.Zero;
                    var takerEscrowSymbol = orderEscrowSymbol;

                    var makerOrder = makerOrders.Get<ExchangeOrder>(bestIndex);  
                    var makerEscrow = _escrows.Get<BigInteger, BigInteger>(makerOrder.Uid);
                    var makerEscrowUsage = BigInteger.Zero; ;
                    var makerEscrowSymbol = orderEscrowSymbol == baseSymbol ? quoteSymbol : baseSymbol;

                    //Get fulfilled order size in base tokens
                    //and then calculate the corresponding fulfilled order size in quote tokens
                    if (takerEscrowSymbol == baseSymbol)
                    {
                        var makerEscrowBaseEquivalent = ConvertQuoteToBase(makerEscrow, makerOrder.Price, baseToken, quoteToken);
                        takerEscrowUsage = takerAvailableEscrow < makerEscrowBaseEquivalent ? takerAvailableEscrow : makerEscrowBaseEquivalent;
                        
                        makerEscrowUsage = CalculateEscrowAmount(takerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }
                    else
                    {
                        var takerEscrowBaseEquivalent = ConvertQuoteToBase(takerAvailableEscrow, makerOrder.Price, baseToken, quoteToken);
                        makerEscrowUsage = makerEscrow < takerEscrowBaseEquivalent ? makerEscrow : takerEscrowBaseEquivalent;

                        takerEscrowUsage = CalculateEscrowAmount(makerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }

                    Runtime.Expect(takerEscrowUsage <= takerAvailableEscrow, "Tried to escrow more than available");
                    Runtime.Expect(makerEscrowUsage <= makerEscrow, "Tried to escrow more than available");

                    Runtime.Nexus.TransferTokens(takerEscrowSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, makerOrder.Creator, takerEscrowUsage);
                    Runtime.Nexus.TransferTokens(makerEscrowSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, takerOrder.Creator, makerEscrowUsage);

                    Runtime.Notify(EventKind.TokenReceive, makerOrder.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = takerEscrowSymbol, value = takerEscrowUsage });
                    Runtime.Notify(EventKind.TokenReceive, takerOrder.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = makerEscrowSymbol, value = makerEscrowUsage });

                    orderEscrowUsage += takerEscrowUsage;

                    Runtime.Notify(EventKind.OrderFilled, takerOrder.Creator, takerOrder.Uid);
                    Runtime.Notify(EventKind.OrderFilled, makerOrder.Creator, makerOrder.Uid);

                    if (makerEscrowUsage == makerEscrow)
                    {
                        makerOrders.RemoveAt<ExchangeOrder>(bestIndex);
                        _orderMap.Remove(makerOrder.Uid);

                        Runtime.Expect(_escrows.ContainsKey(makerOrder.Uid), "An orderbook entry must have registered escrow");
                        _escrows.Remove(makerOrder.Uid);

                        Runtime.Notify(EventKind.OrderClosed, makerOrder.Creator, makerOrder.Uid);
                    }
                    else
                        _escrows.Set(makerOrder.Uid, makerEscrow - makerEscrowUsage);
                }
                else
                {
                    break;
                }

            } while (orderEscrowUsage < orderEscrowAmount);

            var leftoverEscrow = orderEscrowAmount - orderEscrowUsage;

            if (leftoverEscrow == 0 || IoC)
            {
                orderList.RemoveAt<ExchangeOrder>(orderIndex);
                _orderMap.Remove(uid);
                _escrows.Remove(uid);

                if (leftoverEscrow > 0)
                {
                    Runtime.Nexus.TransferTokens(orderEscrowSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, order.Creator, leftoverEscrow);
                    Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = orderEscrowSymbol, value = leftoverEscrow });
                    Runtime.Notify(EventKind.OrderCancelled, order.Creator, order.Uid);
                }
                else
                    Runtime.Notify(EventKind.OrderClosed, order.Creator, order.Uid);
            }
            else
            {
                _escrows.Set(uid, leftoverEscrow);
            }

            //TODO: ADD FEES, SEND THEM TO Runtime.Chain.Address FOR NOW
        }

        public void CancelOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey<BigInteger>(uid), "order not found");
            var key = _orderMap.Get<BigInteger, string>(uid);
            StorageList orderList = _orders.Get<string, StorageList>(key);

            var count = orderList.Count();
            for (int i=0; i<count; i++)
            {
                var order = orderList.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    Runtime.Expect(IsWitness(order.Creator), "invalid witness");

                    orderList.RemoveAt<ExchangeOrder>(i);
                    _orderMap.Remove<BigInteger>(uid);
                    _fills.Remove<BigInteger>(uid);

                    if (_escrows.ContainsKey<BigInteger>(uid))
                    {
                        var leftoverEscrow = _escrows.Get<BigInteger, BigInteger>(uid);
                        if (leftoverEscrow > 0)
                        {
                            var escrowSymbol = order.Side == ExchangeOrderSide.Sell ? order.QuoteSymbol : order.BaseSymbol;
                            Runtime.Nexus.TransferTokens(escrowSymbol, this.Storage, this.Runtime.Chain, this.Runtime.Chain.Address, order.Creator, leftoverEscrow);
                            Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = escrowSymbol, value = leftoverEscrow });
                        }
                    }

                    return;
                }
            }

            // if it reaches here, it means it not found nothing in previous part
            throw new Exception("order not found");
        }

        /*
        TODO: implement methods that allow cleaning up the order history book.. make sure only the exchange that placed the orders can clear them
        */

        /*
         TODO: implement code for trail stops and a method to allow a 3rd party to update the trail stop, without revealing user or order info
         */

        public BigInteger CalculateEscrowAmount(BigInteger orderSize, BigInteger orderPrice, TokenInfo baseToken, TokenInfo quoteToken, ExchangeOrderSide side)
        {
            switch (side)
            {
                case Sell:
                    return orderSize;

                case Buy:
                    return UnitConversion.ToBigInteger(UnitConversion.ToDecimal(orderSize, baseToken.Decimals) * UnitConversion.ToDecimal(orderPrice, quoteToken.Decimals), quoteToken.Decimals);

                default: throw new ContractException("invalid order side");
            }
        }

        private BigInteger ConvertQuoteToBase(BigInteger quoteAmount, BigInteger orderPrice, TokenInfo baseToken, TokenInfo quoteToken)
        {
            return UnitConversion.ToBigInteger(UnitConversion.ToDecimal(quoteAmount, quoteToken.Decimals) / UnitConversion.ToDecimal(orderPrice, quoteToken.Decimals), baseToken.Decimals);
        }

        public string CalculateEscrowSymbol(TokenInfo baseToken, TokenInfo quoteToken, ExchangeOrderSide side) => side == Sell ? baseToken.Symbol : quoteToken.Symbol;

        public ExchangeOrder GetExchangeOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey<BigInteger>(uid), "order not found");

            var key = _orderMap.Get<BigInteger, string>(uid);
            StorageList orderList = _orders.Get<string, StorageList>(key);

            var count = orderList.Count();
            var order = new ExchangeOrder();
            for (int i = 0; i < count; i++)
            {
                order = orderList.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    //Runtime.Expect(IsWitness(order.Creator), "invalid witness");
                    break;
                }
            }

            return order;
        }

        public BigInteger GetOrderLeftoverEscrow(BigInteger uid)
        {
            Runtime.Expect(_escrows.ContainsKey(uid), "order not found");

            return _escrows.Get<BigInteger, BigInteger>(uid);
        }

        public ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol)
        {
            return GetOrderBook(baseSymbol, quoteSymbol, false);
        }

        public ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol, ExchangeOrderSide side)
        {
            return GetOrderBook(baseSymbol, quoteSymbol, true, side);
        }

        private ExchangeOrder[] GetOrderBook(string baseSymbol, string quoteSymbol, bool oneSideFlag, ExchangeOrderSide side = Buy)
        {
            var buyKey = BuildOrderKey(Buy, quoteSymbol, baseSymbol);
            var sellKey = BuildOrderKey(Sell, quoteSymbol, baseSymbol);

            var buyOrders = ((oneSideFlag && side == Buy) || !oneSideFlag) ? _orders.Get<string, StorageList>(buyKey) : new StorageList();
            var sellOrders = ((oneSideFlag && side == Sell) || !oneSideFlag) ? _orders.Get<string, StorageList>(sellKey) : new StorageList();

            var buyCount = buyOrders.Context == null ? 0 : buyOrders.Count();
            var sellCount = sellOrders.Context == null ? 0 : sellOrders.Count();

            ExchangeOrder[] orderbook = new ExchangeOrder[(long) (buyCount + sellCount)];

            for (long i = 0; i < buyCount; i++)
            {
                orderbook[i] = buyOrders.Get<ExchangeOrder>(i);
            }

            for (long i = (long) buyCount; i < orderbook.Length; i++)
            {
                orderbook[i] = sellOrders.Get<ExchangeOrder>(i);
            }

            return orderbook;
        }


        #region OTC TRADES
        public void SwapTokens(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price, byte[] signature)
        {
            Runtime.Expect(IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var baseBalances = new BalanceSheet(baseSymbol);
            var baseBalance = baseBalances.Get(this.Storage, seller);
            Runtime.Expect(baseBalance >= amount, "invalid amount");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = amount,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.PublicKey), "invalid signature");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var quoteBalances = new BalanceSheet(quoteSymbol);
            var quoteBalance = quoteBalances.Get(this.Storage, buyer);
            Runtime.Expect(quoteBalance >= price, "invalid balance");

            Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, Runtime.Chain, buyer, seller, price), "payment failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, Runtime.Chain, seller, buyer, amount), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = amount });
            Runtime.Notify(EventKind.TokenSend, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });

            Runtime.Notify(EventKind.TokenReceive, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });
            Runtime.Notify(EventKind.TokenReceive, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = amount });
        }

        public void SwapToken(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, byte[] signature)
        {
            Runtime.Expect(IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var ownerships = new OwnershipSheet(baseSymbol);
            var owner = ownerships.GetOwner(this.Storage, tokenID);
            Runtime.Expect(owner == seller, "invalid owner");

            var swap = new TokenSwap()
            {
                baseSymbol = baseSymbol,
                quoteSymbol = quoteSymbol,
                buyer = buyer,
                seller = seller,
                price = price,
                value = tokenID,
            };

            var msg = Serialization.Serialize(swap);
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.PublicKey), "invalid signature");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = new BalanceSheet(quoteSymbol);
            var balance = balances.Get(this.Storage, buyer);
            Runtime.Expect(balance >= price, "invalid balance");

            Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, Runtime.Chain, buyer, owner, price), "payment failed");
            Runtime.Expect(Runtime.Nexus.TransferToken(baseSymbol, this.Storage, Runtime.Chain, owner, buyer, tokenID), "transfer failed");

            Runtime.Notify(EventKind.TokenSend, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = tokenID });
            Runtime.Notify(EventKind.TokenSend, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });

            Runtime.Notify(EventKind.TokenReceive, seller, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = quoteSymbol, value = price });
            Runtime.Notify(EventKind.TokenReceive, buyer, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = baseSymbol, value = tokenID });
        }
        #endregion
    }
}
