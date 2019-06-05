using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Cryptography.EdDSA;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum ExchangeOrderSide
    {
        Buy,
        Sell
    }

    public struct ExchangeOrder
    {
        public readonly BigInteger Uid;
        public readonly Timestamp Timestamp;
        public readonly Address Creator;

        public readonly BigInteger OrderSize;
        public readonly string BaseSymbol;

        public readonly BigInteger OrderPrice;
        public readonly string QuoteSymbol;

        public readonly ExchangeOrderSide Side;

        public readonly bool IoC;

        public ExchangeOrder(BigInteger uid, Timestamp timestamp, Address creator, BigInteger orderSize, string baseSymbol, BigInteger orderPrice, string quoteSymbol, ExchangeOrderSide side, bool ioC)
        {
            Uid = uid;
            Timestamp = timestamp;
            Creator = creator;

            OrderSize = orderSize;
            BaseSymbol = baseSymbol;

            OrderPrice = orderPrice;
            QuoteSymbol = quoteSymbol;

            Side = side;
            IoC = ioC;
        }

        public ExchangeOrder(ExchangeOrder order, BigInteger newOrderSize, Timestamp newTimestamp)
        {
            Uid = order.Uid;
            Timestamp = newTimestamp;
            Creator = order.Creator;

            OrderSize = newOrderSize;
            BaseSymbol = order.BaseSymbol;

            OrderPrice = order.OrderPrice;
            QuoteSymbol = order.QuoteSymbol;

            Side = order.Side;
            IoC = order.IoC;
        }
    }

    public struct OrderBook
    {
        public StorageMap OpenBuyOrders;            //<BigInteger, ExchangeOrder>
        public StorageMap BuyOrderLinkedList;       //<BigInteger, BigInteger>, sorted by ascending price of the corresponding orders
        public BigInteger BuyOrderListHead;

        public StorageMap OpenSellOrders;           //<BigInteger, ExchangeOrder>
        public StorageMap SellOrderLinkedList;      //<BigInteger, BigInteger>, sorted by ascending price of the corresponding orders
        public BigInteger SellOrderListHead;
    }

    public struct OrderBookHistory
    {
        public StorageMap FilledOrders;             //<BigInteger, ExchangeOrder>
        public StorageMap FilledOrderLinkedList;    //<BigInteger, BigInteger>
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
        private struct TokenPriceRatio
        {
            public readonly bool IsNumerator; //true if the ratio represents the numerator
            public readonly BigInteger Ratio;

            public TokenPriceRatio(BigInteger ratio, bool isNumerator)
            {
                Ratio = ratio;
                IsNumerator = isNumerator;
            }
        }

        public override string Name => "exchange";

        internal StorageMap _orders; //<string, OrderBook>

        public ExchangeContract() : base()
        {
        }

        private BigInteger GetMinimumSymbolQuantity(TokenInfo token) => BigInteger.Pow(10, token.Decimals / 2);

        /// <summary>
        /// Creates a limit order on the exchange
        /// </summary>
        /// <param name="from"></param>
        /// <param name="baseSymbol">For SOUL/KCAL pair, SOUL would be the base symbol</param>
        /// <param name="quoteSymbol">For SOUL/KCAL pair, KCAL would be the quote symbol</param>
        /// <param name="orderSize">Amount of base symbol tokens the user wants to buy/sell</param>
        /// <param name="orderPrice">Amount of quote symbol tokens the user wants to pay/receive per unit of base symbol tokens</param>
        /// <param name="side">If the order is a buy or sell order</param>
        /// <param name="IoC">"Immediate or Cancel" flag: if true, requires any unfulfilled parts of the order to be cancelled immediately after a single attempt at fulfilling it.</param>
        public void OpenOrder(Address from, string baseSymbol, string quoteSymbol, BigInteger orderSize, BigInteger orderPrice, ExchangeOrderSide side, bool IoC)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(baseSymbol), "invalid base token");
            var baseToken = Runtime.Nexus.GetTokenInfo(baseSymbol);
            Runtime.Expect(baseToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(orderSize >= GetMinimumSymbolQuantity(baseToken), "order size is not sufficient");

            Runtime.Expect(Runtime.Nexus.TokenExists(quoteSymbol), "invalid quote token");
            var quoteToken = Runtime.Nexus.GetTokenInfo(quoteSymbol);
            Runtime.Expect(quoteToken.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");
            Runtime.Expect(orderPrice >= GetMinimumSymbolQuantity(quoteToken), "order price is not sufficient");

            //var tokenABI = Chain.FindABI(NativeABI.Token);
            //Runtime.Expect(baseTokenContract.ABI.Implements(tokenABI));

            switch (side)
            {
                case ExchangeOrderSide.Sell:
                    {
                        var balances = new BalanceSheet(baseSymbol);
                        var balance = balances.Get(this.Storage, from);
                        Runtime.Expect(balance >= orderSize, "not enough balance");

                        Runtime.Expect(Runtime.Nexus.TransferTokens(baseSymbol, this.Storage, Runtime.Chain, from, Runtime.Chain.Address, orderSize), "transfer failed");

                        break;
                    }

                case ExchangeOrderSide.Buy:
                    {
                        var balances = new BalanceSheet(quoteSymbol);
                        var balance = balances.Get(this.Storage, from);
                        Runtime.Expect(balance >= orderPrice, "not enough balance");

                        // TODO check this. If order price is not the total amount of tokens to pay/charge, this needs to change
                        Runtime.Expect(Runtime.Nexus.TransferTokens(quoteSymbol, this.Storage, Runtime.Chain, from, Runtime.Chain.Address, orderPrice), "transfer failed");
                        break;
                    }

                default: throw new ContractException("invalid order side");
            }

            var uid = Runtime.Chain.GenerateUID(this.Storage);
            Runtime.Expect(uid >= 0, "Generated a negative UID");

            var order = new ExchangeOrder(uid, Runtime.Time, from, orderSize, baseSymbol, orderPrice, quoteSymbol, side, IoC);

            var isOrderFulfilled = AttemptOrderFulfill(order);

            if (isOrderFulfilled == false && IoC == false) //if order isn't completely fulfilled immediately and it isn't an IOC order, add it to the orderbook
            {
                AddToOrderBook(order);
            }
            else if(isOrderFulfilled)   //if it is completely fulfilled, add it to fulfilled order log
            {
                MoveToFulfilledLog(order);
            }

            //TODO: ADD FEES, SEND THEM TO Runtime.Chain.Address FOR NOW
        }

        private void MoveToFulfilledLog(ExchangeOrder order)
        {
            
        }

        private void AddToOrderBook(ExchangeOrder newOrder)
        {
            var pair = BuildPairName(newOrder);
            var orderBook = _orders.Get<string, OrderBook>(pair);
            StorageMap keys;
            StorageMap orders;

            BigInteger start;

            switch (newOrder.Side)
            {
                case ExchangeOrderSide.Buy:
                    keys = orderBook.BuyOrderLinkedList;
                    orders = orderBook.OpenBuyOrders;
                    start = orderBook.BuyOrderListHead;
                    break;

                case ExchangeOrderSide.Sell:
                    keys = orderBook.SellOrderLinkedList;
                    orders = orderBook.OpenSellOrders;
                    start = orderBook.SellOrderListHead;
                    break;

                default:
                    throw new ContractException("invalid order side");
            }
            
            var currentOrder = orders.Get<BigInteger, ExchangeOrder>(start);
            var previousOrder = currentOrder;
            var size = keys.Count();

            for (var i = new BigInteger(0); i < size; i++)
            {
                if (IsOrderBefore(newOrder, currentOrder))  //check if "new order" should be inserted before "current order"
                {
                    if (i == 0)     //if the current order is the first one, update the head of the list
                        start = newOrder.Uid;   //TODO: VERIFY "start" IS A REFERENCE TO THE LISTHEAD FIELD OF THE STRUCT
                    else            //otherwise, update the previous node to point to the current node
                        keys.Set(previousOrder.Uid, newOrder.Uid);

                    keys.Set(newOrder.Uid, currentOrder.Uid);   //but always set the pointer of the new order to the current order

                    break;
                }
                else
                {
                    if (i == size - 1)  //if we reached the end of the list without finding an insert point, insert at the end of the list
                    {
                        keys.Set(currentOrder.Uid, newOrder.Uid);
                        keys.Set(newOrder.Uid, new BigInteger(-1));     //the tail of the linked list is -1 as the order UID's are always positive
                        break;
                    }

                    //otherwise, simply update the iterators and have another go at this
                    previousOrder = currentOrder;
                    var nextOrderUid = keys.Get<BigInteger, BigInteger>(currentOrder.Uid);
                    currentOrder = orders.Get<BigInteger, ExchangeOrder>(nextOrderUid);
                }
            }

            orders.Set(newOrder.Uid, newOrder);
        }

        /// <summary>
        /// Checks if the source order has a higher price per token than the target order
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <returns>Returns true if the source order should come before the given target order</returns>
        private bool IsOrderBefore(ExchangeOrder source, ExchangeOrder target)
        {
            Runtime.Expect(source.Side == target.Side, "Attempt at comparing orders from different orderbook sides");

            switch (source.Side)
            {
                case ExchangeOrderSide.Buy:
                    return source.OrderPrice > target.OrderPrice;

                case ExchangeOrderSide.Sell:
                    return source.OrderPrice < target.OrderPrice;

                default:
                    throw new ContractException("invalid order side");
            }
        }


        /*
         ****
         * This won't be necessary if we implement the BigRational class
         ****
        private ExchangeOrder FixPriceRatio(ExchangeOrder order)
        {
            if (order.OrderSize % order.OrderPrice == 0)
                return order;

            BigInteger numerator, denominator;
            bool isNumerator = order.OrderSize > order.OrderPrice;

            numerator = isNumerator ? order.OrderSize : order.OrderPrice;
            denominator = isNumerator ? order.OrderPrice : order.OrderSize;

            var flooredRatio = numerator / denominator;
            var roundedRatio = BigInteger.DivideAndRoundToClosest(numerator, denominator);

            if (isNumerator)
            {
                
            }
            else
            {

            }
        }
        */

        private TokenPriceRatio GetPricePerToken(ExchangeOrder order)
        {
            BigInteger numerator, denominator;
            bool isNumerator = order.OrderSize > order.OrderPrice;

            numerator = isNumerator ? order.OrderSize : order.OrderPrice;
            denominator = isNumerator ? order.OrderPrice : order.OrderSize;

            var ratio = BigInteger.DivideAndRoundToClosest(numerator, denominator);

            return new TokenPriceRatio(ratio, isNumerator);
        }

        /// <summary>
        /// Returns true if order was completely fulfilled, false otherwise
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private bool AttemptOrderFulfill(ExchangeOrder order)
        {
            /*
            var pair = BuildPairName(order);
            var orderBook = _orders.Get<string, OrderBook>(pair);
            string[] keys;
            OrderBook[] orders;

            switch (order.Side)
            {
                case ExchangeOrderSide.Sell:
                    keys = orderBook.BuyOrderLinkedList.All<string>();
                    orders = orderBook.OpenBuyOrders.All<string, OrderBook>(keys);
                    break;

                case ExchangeOrderSide.Buy:
                    keys = orderBook.SellOrderLinkedList.All<string>();
                    orders = orderBook.OpenSellOrders.All<string, OrderBook>(keys);
                    break;

                default:
                    throw new ContractException("invalid order side");
            }
            */

            //TODO: ADD FEES, SEND THEM TO Runtime.Chain.Address FOR NOW

            return true;
        }

        /*
        TODO: implement methods that allow cleaning up the order history book.. make sure only the exchange that placed the orders can clear them
        */

        /*
         TODO: implement code for trail stops and a method to allow a 3rd party to update the trail stop, without revealing user or order info
         */

        private string BuildPairName(ExchangeOrder order) => order.BaseSymbol + "_" + order.QuoteSymbol;

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
