using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.Storage.Context;
using System;
using static Phantasma.Blockchain.Contracts.ExchangeOrderSide;
using static Phantasma.Blockchain.Contracts.ExchangeOrderType;
using Phantasma.Domain;
using System.Linq;

namespace Phantasma.Blockchain.Contracts
{
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    public enum ExchangeOrderType
    {
        OTC,
        Limit,              //normal limit order
        ImmediateOrCancel,  //special limit order, any unfulfilled part of the order gets cancelled if not immediately fulfilled
        Market,             //normal market order
        //TODO: FillOrKill = 4,         //Either gets 100% fulfillment or it gets cancelled , no partial fulfillments like in IoC order types
    }

    public struct ExchangeOrder
    {
        public readonly BigInteger Uid;
        public readonly Timestamp Timestamp;
        public readonly Address Creator;
        public readonly Address Provider;

        public readonly BigInteger Amount;
        public readonly string BaseSymbol;

        public readonly BigInteger Price;
        public readonly string QuoteSymbol;

        public readonly ExchangeOrderSide Side;
        public readonly ExchangeOrderType Type;

        public ExchangeOrder(BigInteger uid, Timestamp timestamp, Address creator, Address provider, BigInteger amount, string baseSymbol, BigInteger price, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType type)
        {
            Uid = uid;
            Timestamp = timestamp;
            Creator = creator;
            Provider = provider;

            Amount = amount;
            BaseSymbol = baseSymbol;

            Price = price;
            QuoteSymbol = quoteSymbol;

            Side = side;
            Type = type;
        }

        public ExchangeOrder(ExchangeOrder order, BigInteger newPrice, BigInteger newOrderSize)
        {
            Uid = order.Uid;
            Timestamp = order.Timestamp;
            Creator = order.Creator;
            Provider = order.Provider;

            Amount = newOrderSize;
            BaseSymbol = order.BaseSymbol;

            Price = newOrderSize;
            QuoteSymbol = order.QuoteSymbol;

            Side = order.Side;
            Type = order.Type;

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

    public struct ExchangeProvider
    {
        public Address address;
        public string id;
        public string name;
        public Hash dapp;
    }

    public sealed class ExchangeContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Exchange;

        internal StorageList _availableBases; // string
        internal StorageList _availableQuotes; // string
        internal StorageMap _orders; //<string, List<Order>>
        internal StorageMap _orderMap; //<uid, string> // maps orders ids to pairs
        internal StorageMap _fills; //<uid, BigInteger>
        internal StorageMap _escrows; //<uid, BigInteger>

        internal StorageList _exchanges;

        public ExchangeContract() : base()
        {
        }

        private string BuildOrderKey(ExchangeOrderSide side, string baseSymbol, string quoteSymbol) => $"{side}_{baseSymbol}_{quoteSymbol}";

        public BigInteger GetMinimumQuantity(BigInteger tokenDecimals) => BigInteger.Pow(10, tokenDecimals / 2);
        public BigInteger GetMinimumTokenQuantity(IToken token) => GetMinimumQuantity(token.Decimals);

        public BigInteger GetMinimumSymbolQuantity(string symbol)
        {
            var token = Runtime.GetToken(symbol);
            return GetMinimumQuantity(token.Decimals);
        }

        public bool IsExchange(Address address)
        {
            var count = _exchanges.Count();
            for (int i=0; i<count; i++)
            {
                var exchange = _exchanges.Get<ExchangeProvider>(i);
                if (exchange.address == address)
                {
                    return true;
                }
            }

            return false;
        }

        public void CreateExchange(Address from, string id, string name)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(ValidationUtils.IsValidIdentifier(id), "invalid id");

            var exchange = new ExchangeProvider()
            {
                address = from,
                id = id,
                name = name,
            };

            _exchanges.Add<ExchangeProvider>(exchange);
        }

        public ExchangeProvider[] GetExchanges()
        {
            return _exchanges.All<ExchangeProvider>();
        }

        private void OpenOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, ExchangeOrderSide side, ExchangeOrderType orderType, BigInteger orderSize, BigInteger price)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(baseSymbol != quoteSymbol, "invalid base/quote pair");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            if (orderType == ExchangeOrderType.OTC)
            {
                Runtime.Expect(side == ExchangeOrderSide.Sell, "otc order must be sell");
                CreateOTC(from, baseSymbol, quoteSymbol, orderSize, price);
                return;
            }

            Runtime.Expect(Runtime.GasTarget == provider, "invalid gas target");

            if (orderType != Market)
            {
                Runtime.Expect(orderSize >= GetMinimumTokenQuantity(baseToken), "order size is not sufficient");
                Runtime.Expect(price >= GetMinimumTokenQuantity(quoteToken), "order price is not sufficient");
            }

            var uid = Runtime.GenerateUID();

            //--------------
            //perform escrow for non-market orders
            string orderEscrowSymbol = CalculateEscrowSymbol(baseToken, quoteToken, side);
            IToken orderEscrowToken = orderEscrowSymbol == baseSymbol ? baseToken : quoteToken;
            BigInteger orderEscrowAmount;
            BigInteger orderEscrowUsage = 0;

            if (orderType == Market)
            {
                orderEscrowAmount = orderSize;
                Runtime.Expect(orderEscrowAmount >= GetMinimumTokenQuantity(orderEscrowToken), "market order size is not sufficient");
            }
            else
            {
                orderEscrowAmount = CalculateEscrowAmount(orderSize, price, baseToken, quoteToken, side);
            }

            //BigInteger baseTokensUnfilled = orderSize;

            var balance = Runtime.GetBalance(orderEscrowSymbol, from);
            Runtime.Expect(balance >= orderEscrowAmount, "not enough balance");

            Runtime.TransferTokens(orderEscrowSymbol, from, this.Address, orderEscrowAmount);
            //------------

            var thisOrder = new ExchangeOrder();
            StorageList orderList;
            BigInteger orderIndex = 0;

            thisOrder = new ExchangeOrder(uid, Runtime.Time, from, provider, orderSize, baseSymbol, price, quoteSymbol, side, orderType);
            Runtime.Notify(EventKind.OrderCreated, from, uid);

            var key = BuildOrderKey(side, quoteSymbol, baseSymbol);

            orderList = _orders.Get<string, StorageList>(key);
            orderIndex = orderList.Add<ExchangeOrder>(thisOrder);
            _orderMap.Set<BigInteger, string>(uid, key);

            var makerSide = side == Buy ? Sell : Buy;
            var makerKey = BuildOrderKey(makerSide, quoteSymbol, baseSymbol);
            var makerOrders = _orders.Get<string, StorageList>(makerKey);

            do
            {
                int bestIndex = -1;
                BigInteger bestPrice = 0;
                Timestamp bestPriceTimestamp = 0;

                ExchangeOrder takerOrder = thisOrder;

                var makerOrdersCount = makerOrders.Count();
                for (int i = 0; i < makerOrdersCount; i++)
                {
                    var makerOrder = makerOrders.Get<ExchangeOrder>(i);

                    if (side == Buy)
                    {
                        if (makerOrder.Price > takerOrder.Price && orderType != Market) // too expensive, we wont buy at this price
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
                        if (makerOrder.Price < takerOrder.Price && orderType != Market) // too cheap, we wont sell at this price
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
                    //since order "uid" has found a match, the creator of this order will be a taker as he will remove liquidity from the market
                    //and the creator of the "bestIndex" order is the maker as he is providing liquidity to the taker
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
                        var makerEscrowBaseEquivalent = Runtime.ConvertQuoteToBase(makerEscrow, makerOrder.Price, baseToken, quoteToken);
                        takerEscrowUsage = takerAvailableEscrow < makerEscrowBaseEquivalent ? takerAvailableEscrow : makerEscrowBaseEquivalent;

                        makerEscrowUsage = CalculateEscrowAmount(takerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }
                    else
                    {
                        var takerEscrowBaseEquivalent = Runtime.ConvertQuoteToBase(takerAvailableEscrow, makerOrder.Price, baseToken, quoteToken);
                        makerEscrowUsage = makerEscrow < takerEscrowBaseEquivalent ? makerEscrow : takerEscrowBaseEquivalent;

                        takerEscrowUsage = CalculateEscrowAmount(makerEscrowUsage, makerOrder.Price, baseToken, quoteToken, Buy);
                    }

                    Runtime.Expect(takerEscrowUsage <= takerAvailableEscrow, "Taker tried to use more escrow than available");
                    Runtime.Expect(makerEscrowUsage <= makerEscrow, "Maker tried to use more escrow than available");

                    if (takerEscrowUsage < GetMinimumSymbolQuantity(takerEscrowSymbol) ||
                        makerEscrowUsage < GetMinimumSymbolQuantity(makerEscrowSymbol))
                    {

                        break;
                    }

                    Runtime.TransferTokens(takerEscrowSymbol, this.Address, makerOrder.Creator, takerEscrowUsage);
                    Runtime.TransferTokens(makerEscrowSymbol, this.Address, takerOrder.Creator, makerEscrowUsage);

                    orderEscrowUsage += takerEscrowUsage;

                    Runtime.Notify(EventKind.OrderFilled, takerOrder.Creator, takerOrder.Uid);
                    Runtime.Notify(EventKind.OrderFilled, makerOrder.Creator, makerOrder.Uid);

                    if (makerEscrowUsage == makerEscrow)
                    {
                        makerOrders.RemoveAt(bestIndex);
                        _orderMap.Remove(makerOrder.Uid);

                        Runtime.Expect(_escrows.ContainsKey(makerOrder.Uid), "An orderbook entry must have registered escrow");
                        _escrows.Remove(makerOrder.Uid);

                        Runtime.Notify(EventKind.OrderClosed, makerOrder.Creator, makerOrder.Uid);
                    }
                    else
                        _escrows.Set(makerOrder.Uid, makerEscrow - makerEscrowUsage);

                }
                else
                    break;

            } while (orderEscrowUsage < orderEscrowAmount);

            var leftoverEscrow = orderEscrowAmount - orderEscrowUsage;

            if (leftoverEscrow == 0 || orderType != Limit)
            {
                orderList.RemoveAt(orderIndex);
                _orderMap.Remove(thisOrder.Uid);
                _escrows.Remove(thisOrder.Uid);

                if (leftoverEscrow > 0)
                {
                    Runtime.TransferTokens(orderEscrowSymbol, this.Address, thisOrder.Creator, leftoverEscrow);
                    Runtime.Notify(EventKind.OrderCancelled, thisOrder.Creator, thisOrder.Uid);
                }
                else
                    Runtime.Notify(EventKind.OrderClosed, thisOrder.Creator, thisOrder.Uid);
            }
            else
            {
                _escrows.Set(uid, leftoverEscrow);
            }

            //TODO: ADD FEES, SEND THEM TO this.Address FOR NOW
        }

        public void OpenMarketOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, BigInteger orderSize, ExchangeOrderSide side)
        {
            OpenOrder(from, provider, baseSymbol, quoteSymbol, side, Market, orderSize, 0);
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
        public void OpenLimitOrder(Address from, Address provider, string baseSymbol, string quoteSymbol, BigInteger orderSize, BigInteger price, ExchangeOrderSide side, bool IoC)
        {
            OpenOrder(from, provider, baseSymbol, quoteSymbol, side, IoC ? ImmediateOrCancel : Limit, orderSize, price);
        }

        public void OpenOTCOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger ammount, BigInteger price)
        {
            OpenOrder(from, Address.Null, baseSymbol, quoteSymbol, ExchangeOrderSide.Sell, ExchangeOrderType.OTC, ammount, price);
        }

        public void CancelOrder(BigInteger uid)
        {
            Runtime.Expect(_orderMap.ContainsKey<BigInteger>(uid), "order not found");
            var key = _orderMap.Get<BigInteger, string>(uid);
            StorageList orderList = _orders.Get<string, StorageList>(key);

            var count = orderList.Count();
            for (int i = 0; i < count; i++)
            {
                var order = orderList.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");

                    orderList.RemoveAt(i);
                    _orderMap.Remove<BigInteger>(uid);
                    _fills.Remove<BigInteger>(uid);

                    if (_escrows.ContainsKey<BigInteger>(uid))
                    {
                        var leftoverEscrow = _escrows.Get<BigInteger, BigInteger>(uid);
                        if (leftoverEscrow > 0)
                        {
                            var escrowSymbol = order.Side == ExchangeOrderSide.Sell ? order.QuoteSymbol : order.BaseSymbol;
                            Runtime.TransferTokens(escrowSymbol, this.Address, order.Creator, leftoverEscrow);
                            Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData(escrowSymbol, leftoverEscrow, Runtime.Chain.Name));
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

        public BigInteger CalculateEscrowAmount(BigInteger orderSize, BigInteger orderPrice, IToken baseToken, IToken quoteToken, ExchangeOrderSide side)
        {
            switch (side)
            {
                case Sell:
                    return orderSize;

                case Buy:
                    return Runtime.ConvertBaseToQuote(orderSize, orderPrice, baseToken, quoteToken);

                default: throw new ContractException("invalid order side");
            }
        }

        public string CalculateEscrowSymbol(IToken baseToken, IToken quoteToken, ExchangeOrderSide side) => side == Sell ? baseToken.Symbol : quoteToken.Symbol;

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
                    //Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");
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

        public ExchangeOrder[] GetOrderBooks(string baseSymbol, string quoteSymbol)
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

            ExchangeOrder[] orderbook = new ExchangeOrder[(long)(buyCount + sellCount)];

            for (long i = 0; i < buyCount; i++)
            {
                orderbook[i] = buyOrders.Get<ExchangeOrder>(i);
            }

            for (long i = (long)buyCount; i < orderbook.Length; i++)
            {
                orderbook[i] = sellOrders.Get<ExchangeOrder>(i);
            }

            return orderbook;
        }

        #region OTC TRADES
        internal StorageList _otcBook;

        public ExchangeOrder[] GetOTC()
        {
            return _otcBook.All<ExchangeOrder>();
        }

        private void CreateOTC(Address from, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price)
        {
            var uid = Runtime.GenerateUID();

            var count = _otcBook.Count();
            ExchangeOrder lockUpOrder;
            for (int i = 0; i < count; i++)
            {
                lockUpOrder = _otcBook.Get<ExchangeOrder>(i);
                if(lockUpOrder.Creator == from)
                {
                    throw new Exception("Already have an offer created");
                    return;
                }
            }

            var baseBalance = Runtime.GetBalance(baseSymbol, from);
            Runtime.Expect(baseBalance >= amount, "invalid seller amount");
            Runtime.TransferTokens(baseSymbol, from, this.Address, price);

            var order = new ExchangeOrder(uid, Runtime.Time, from, this.Address, amount, baseSymbol, price, quoteSymbol, ExchangeOrderSide.Sell, ExchangeOrderType.OTC);
            _otcBook.Add<ExchangeOrder>(order);
        }

        public void TakeOrder(Address from, BigInteger uid)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var count = _otcBook.Count();
            for (int i=0; i<count; i++)
            {
                var order = _otcBook.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    var baseBalance = Runtime.GetBalance(order.BaseSymbol, this.Address);
                    Runtime.Expect(baseBalance >= order.Price, "invalid seller amount");

                    var quoteBalance = Runtime.GetBalance(order.QuoteSymbol, from);
                    Runtime.Expect(quoteBalance >= order.Amount, "invalid buyer amount");

                    Runtime.TransferTokens(order.BaseSymbol, this.Address, from, order.Price);
                    Runtime.TransferTokens(order.QuoteSymbol, from, order.Creator, order.Amount);
                    _otcBook.RemoveAt(i);
                    return;
                }
            }

            Runtime.Expect(false, "order not found");
        }

        public void CancelOTCOrder(Address from, BigInteger uid)
        {
            var count = _otcBook.Count();
            ExchangeOrder order;
            for (int i = 0; i < count; i++)
            {
                order = _otcBook.Get<ExchangeOrder>(i);
                if (order.Uid == uid)
                {
                    Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
                    Runtime.Expect(Runtime.IsWitness(order.Creator), "invalid witness");
                    Runtime.Expect(from == order.Creator, "invalid owner");
                    _otcBook.RemoveAt(i);

                    Runtime.TransferTokens(order.BaseSymbol, this.Address, order.Creator, order.Price);
                    Runtime.Notify(EventKind.TokenReceive, order.Creator, new TokenEventData(order.BaseSymbol, order.Amount, Runtime.Chain.Name));
                    return;
                }
            }

            // if it reaches here, it means it not found nothing in previous part
            throw new Exception("order not found");            
        }

        /*public void SwapTokens(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger amount, BigInteger price, byte[] signature)
        {
            Runtime.Expect(Runtime.IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(seller.IsUser, "seller must be user address");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var baseBalance = Runtime.GetBalance(baseSymbol, seller);
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
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.ToByteArray().Skip(2).ToArray()), "invalid signature");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var quoteBalance = Runtime.GetBalance(quoteSymbol, buyer);
            Runtime.Expect(quoteBalance >= price, "invalid balance");

            Runtime.TransferTokens(quoteSymbol, buyer, seller, price);
            Runtime.TransferTokens(baseSymbol, seller, buyer, amount);
        }

        public void SwapToken(Address buyer, Address seller, string baseSymbol, string quoteSymbol, BigInteger tokenID, BigInteger price, byte[] signature)
        {
            Runtime.Expect(Runtime.IsWitness(buyer), "invalid witness");
            Runtime.Expect(seller != buyer, "invalid seller");

            Runtime.Expect(seller.IsUser, "seller must be user address");

            Runtime.Expect(Runtime.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.GetToken(baseSymbol);
            Runtime.Expect(!baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be non-fungible");

            var nft = Runtime.ReadToken(baseSymbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "invalid owner");

            var owner = nft.CurrentOwner;
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
            Runtime.Expect(Ed25519.Verify(signature, msg, seller.ToByteArray().Skip(1).ToArray()), "invalid signature");

            Runtime.Expect(Runtime.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.GetToken(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balance = Runtime.GetBalance(quoteSymbol, buyer);
            Runtime.Expect(balance >= price, "invalid balance");

            Runtime.TransferTokens(quoteSymbol, buyer, owner, price);
            Runtime.TransferToken(baseSymbol, owner, buyer, tokenID);
        }*/
        #endregion
    }
}
