using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.Native.ExchangeOrderSide;
using static Phantasma.Numerics.BigInteger;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class ExchangeTests
    {
        private static KeyPair simulatorOwner = KeyPair.Generate();
        private static NexusSimulator simulator = new NexusSimulator(simulatorOwner, 1234);

        private const string maxDivTokenSymbol = "MADT";        //divisible token with maximum decimal count
        private const string minDivTokenSymbol = "MIDT";        //divisible token with minimum decimal count
        private const string nonDivisibleTokenSymbol = "NDT";

        [TestMethod]
        public void TestIoCLimitMinimumQuantity()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices at the limit

            var minimumBaseToken = UnitConversion.ToDecimal(simulator.Nexus.RootChain.InvokeContract("exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
            var minimumQuoteToken = UnitConversion.ToDecimal(simulator.Nexus.RootChain.InvokeContract("exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

            buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Buy, IoC: true);
            seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Sell, IoC: true);

            buyer.OpenLimitOrder(1m, 1m, Buy);
            buyer.OpenLimitOrder(1m, 1m, Buy);
            Assert.IsTrue(seller.OpenLimitOrder(1m + (minimumBaseToken*.99m), 1m, Sell) == 1m, "Used leftover under minimum quantity");
        }

        [TestMethod]
        public void TestIoCLimitOrderUnmatched()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test unmatched IoC orders 
            seller.OpenLimitOrder(0.01m, 0.5m, Sell);
            buyer.OpenLimitOrder(0.01m, 0.1m, Buy);
            Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
            Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
        }

        [TestMethod]
        public void TestIoCLimitOrderCompleteFulfilment()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test fully matched IoC orders
            buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
        }

        [TestMethod]
        public void TestIoCLimitOrderPartialFulfilment()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test partially matched IoC orders
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
        }

        [TestMethod]
        public void TestIoCLimitOrderMultipleFulfilsPerOrder()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test multiple fills per order
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 2m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 3m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 0.5m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.15m, 1m, Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

            InitExchange();
            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 2m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 3m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 0.5m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.15m, 3m, Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

            //TODO: test multiple IoC orders against each other on the same block!
        }

        [TestMethod]
        public void TestFailedIOC()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices below limit
            try
            {
                buyer.OpenLimitOrder(0, 0.5m, Buy, IoC: true);
                Assert.IsTrue(false, "Order should fail due to insufficient amount");
            }
            catch (Exception e) { }
            try
            {
                buyer.OpenLimitOrder(0.5m, 0, Buy, IoC: true);
                Assert.IsTrue(false, "Order should fail due to insufficient price");
            }
            catch (Exception e) { }

            Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
            Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
        }

        [TestMethod]
        public void TestLimitMinimumQuantity()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices at the limit

            var minimumBaseToken = UnitConversion.ToDecimal(simulator.Nexus.RootChain.InvokeContract("exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
            var minimumQuoteToken = UnitConversion.ToDecimal(simulator.Nexus.RootChain.InvokeContract("exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

            buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Buy);
            seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Sell);
        }

        [TestMethod]
        public void TestLimitOrderUnmatched()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test unmatched IoC orders 
            seller.OpenLimitOrder(0.01m, 0.5m, Sell);
            buyer.OpenLimitOrder(0.01m, 0.1m, Buy);
            Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
            Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
        }

        [TestMethod]
        public void TestLimitOrderCompleteFulfilment()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test fully matched IoC orders
            buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
        }

        [TestMethod]
        public void TestLimitOrderPartialFulfilment()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test partially matched IoC orders
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
        }

        [TestMethod]
        public void TestLimitOrderMultipleFulfilsPerOrder()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test multiple fills per order
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 2m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 3m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 0.5m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.15m, 1m, Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

            InitExchange();
            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 2m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 3m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 0.5m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.15m, 3m, Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

            //TODO: test multiple IoC orders against each other on the same block!
        }

        [TestMethod]
        public void TestFailedRegular()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices below limit
            try
            {
                buyer.OpenLimitOrder(0, 0.5m, Buy);
                Assert.IsTrue(false, "Order should fail due to insufficient amount");
            }
            catch (Exception e) { }
            try
            {
                buyer.OpenLimitOrder(0.5m, 0, Buy);
                Assert.IsTrue(false, "Order should fail due to insufficient price");
            }
            catch (Exception e) { }
        }

        [TestMethod]
        public void TestEmptyBookMarketOrder()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            Assert.IsTrue(buyer.OpenMarketOrder(1, Buy) == 0, "Should not have bought anything");
        }

        [TestMethod]
        public void TestMarketOrderPartialFill()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.2m, 1m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.3m, Buy) == 0.2m, "");
        }

        [TestMethod]
        public void TestMarketOrderCompleteFulfilment()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.1m, 1m, Sell);
            seller.OpenLimitOrder(0.1m, 2m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.3m, Buy) == 0.2m, "");
        }

        [TestMethod]
        public void TestMarketOrderTotalFillNoOrderbookWipe()
        {
            InitExchange();

            var baseSymbol = Nexus.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.1m, 1m, Sell);
            seller.OpenLimitOrder(0.1m, 2m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.25m, Buy) == 0.175m, "");
        }

        #region AuxFunctions

        private void CreateTokens()
        {
            string[] tokenList = { maxDivTokenSymbol, nonDivisibleTokenSymbol };

            simulator.BeginBlock();

            foreach (var symbol in tokenList)
            {
                int decimals = 0;
                BigInteger supply = 0;
                TokenFlags flags = TokenFlags.Divisible;

                switch (symbol)
                {
                    case maxDivTokenSymbol:
                        decimals = NexusContract.MAX_TOKEN_DECIMALS;
                        supply = UnitConversion.ToBigInteger(100000000, decimals);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;
                        break;

                    case minDivTokenSymbol:
                        decimals = 1;
                        supply = UnitConversion.ToBigInteger(100000000, 18);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible;
                        break;

                    case nonDivisibleTokenSymbol:
                        decimals = 0;
                        supply = UnitConversion.ToBigInteger(100000000, 18);
                        flags = TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite;
                        break;
                }

                simulator.GenerateToken(simulatorOwner, symbol, $"{symbol}Token", Nexus.PlatformName, Hash.FromString(symbol), supply, decimals, flags);
                simulator.MintTokens(simulatorOwner, simulatorOwner.Address, symbol, supply);
            }

            simulator.EndBlock();
        }

        private void InitExchange()
        {
            simulatorOwner = KeyPair.Generate();
            simulator = new NexusSimulator(simulatorOwner, 1234);
            CreateTokens();
        }

        class ExchangeUser
        {
            private readonly KeyPair user;
            public TokenInfo baseToken;
            public TokenInfo quoteToken;

            public enum TokenType { Base, Quote}

            public ExchangeUser(string baseSymbol, string quoteSymbol)
            {
                user = KeyPair.Generate();
                baseToken = simulator.Nexus.GetTokenInfo(baseSymbol);
                quoteToken = simulator.Nexus.GetTokenInfo(quoteSymbol);
            }

            public decimal OpenLimitOrder(BigInteger orderSize, BigInteger orderPrice, ExchangeOrderSide side, bool IoC = false)
            {
                return OpenLimitOrder(UnitConversion.ToDecimal(orderSize, baseToken.Decimals), UnitConversion.ToDecimal(orderPrice, quoteToken.Decimals), side, IoC);
            }

            //Opens a limit order and returns how many tokens the user purchased/sold
            public decimal OpenLimitOrder(decimal orderSize, decimal orderPrice, ExchangeOrderSide side, bool IoC = false)
            {
                var nexus = simulator.Nexus;       

                var baseSymbol = baseToken.Symbol;
                var baseDecimals = baseToken.Decimals;
                var quoteSymbol = quoteToken.Symbol;
                var quoteDecimals = quoteToken.Decimals;

                var orderSizeBigint = UnitConversion.ToBigInteger(orderSize, baseDecimals);
                var orderPriceBigint = UnitConversion.ToBigInteger(orderPrice, quoteDecimals);

                var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(baseSymbol, user.Address);
                var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(quoteSymbol, user.Address);

                BigInteger OpenerBaseTokensDelta = 0;
                BigInteger OpenerQuoteTokensDelta = 0;

                //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
                var otherSide = side == Buy ? Sell : Buy;
                var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract("exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
                var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************
                foreach (var oppositeOrder in startingOppositeOrderbook)
                {
                    if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                    {
                        var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                        OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(targetSymbol, oppositeOrder.Creator));
                    }
                }
                //--------------------------


                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "OpenLimitOrder", user.Address, baseSymbol, quoteSymbol, orderSizeBigint, orderPriceBigint, side, IoC).
                        SpendGas(user.Address).EndScript());
                simulator.EndBlock();

                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                BigInteger escrowedAmount = 0;

                //take into account the transfer of the owner's wallet to the chain address
                if (side == Buy)
                {
                    escrowedAmount = UnitConversion.ToBigInteger(orderSize * orderPrice, quoteDecimals);
                    OpenerQuoteTokensDelta -= escrowedAmount;
                }
                else if (side == Sell)
                {
                    escrowedAmount = orderSizeBigint;
                    OpenerBaseTokensDelta -= escrowedAmount;
                }

                //take into account tx cost in case one of the symbols is the FuelToken
                if (baseSymbol == Nexus.FuelTokenSymbol)
                {
                    OpenerBaseTokensDelta -= txCost;
                }
                else
                if (quoteSymbol == Nexus.FuelTokenSymbol)
                {
                    OpenerQuoteTokensDelta -= txCost;
                }

                var events = nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);

                var wasNewOrderCreated = events.Count(x => x.Kind == EventKind.OrderCreated && x.Address == user.Address) == 1;
                Assert.IsTrue(wasNewOrderCreated, "Order was not created");

                var wasNewOrderClosed = events.Count(x => x.Kind == EventKind.OrderClosed && x.Address == user.Address) == 1;
                var wasNewOrderCancelled = events.Count(x => x.Kind == EventKind.OrderCancelled && x.Address == user.Address) == 1;

                var createdOrderEvent = events.First(x => x.Kind == EventKind.OrderCreated);
                var createdOrderUid = Serialization.Unserialize<BigInteger>(createdOrderEvent.Data);
                ExchangeOrder createdOrderPostFill = new ExchangeOrder();

                //----------------
                //verify the order is still in the orderbook according to each case

                //in case the new order was IoC and it wasnt closed, order should have been cancelled
                if (wasNewOrderClosed == false && IoC)
                {
                    Assert.IsTrue(wasNewOrderCancelled, "Non closed IoC order did not get cancelled");
                }
                else
                //if the new order was closed
                if (wasNewOrderClosed)
                {
                    //and check that the order no longer exists on the orderbook
                    try
                    {
                        simulator.Nexus.RootChain.InvokeContract("exchange", "GetExchangeOrder", createdOrderUid);
                        Assert.IsTrue(false, "Closed order exists on the orderbooks");
                    }
                    catch (Exception e)
                    {
                        //purposefully empty, this is the expected code-path
                    }
                }
                else //if the order was not IoC and it wasn't closed, then:
                {
                    Assert.IsTrue(IoC == false, "All IoC orders should have been triggered by the previous ifs");

                    //check that it still exists on the orderbook
                    try
                    {
                        createdOrderPostFill = (ExchangeOrder)simulator.Nexus.RootChain.InvokeContract("exchange", "GetExchangeOrder", createdOrderUid).ToObject();
                    }
                    catch (Exception e)
                    {
                        Assert.IsTrue(false, "Non-IoC unclosed order does not exist on the orderbooks");
                    }
                }
                //------------------

                //------------------
                //validate that everyone received their tokens appropriately

                BigInteger escrowedUsage = 0;   //this will hold the amount of the escrowed amount that was actually used in the filling of the order
                                                //for IoC orders, we need to make sure that what wasn't used gets returned properly
                                                //for non IoC orders, we need to make sure that what wasn't used stays on the orderbook
                BigInteger baseTokensReceived = 0, quoteTokensReceived = 0;
                var OtherAddressesTokensDelta = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************

                //calculate the expected delta of the balances of all addresses involved
                var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenReceive);

                foreach (var tokenExchangeEvent in tokenExchangeEvents)
                {
                    var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                    if (tokenExchangeEvent.Address == user.Address)
                    {
                        if(eventData.symbol == baseSymbol)
                            baseTokensReceived += eventData.value;
                        else
                        if(eventData.symbol == quoteSymbol)
                            quoteTokensReceived += eventData.value;
                    }
                    else
                    {
                        Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                        if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                            OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.value;
                        else
                            OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.value);

                        escrowedUsage += eventData.value;   //the tokens other addresses receive come from the escrowed amount of the order opener
                    }
                }

                OpenerBaseTokensDelta += baseTokensReceived;
                OpenerQuoteTokensDelta += quoteTokensReceived;

                var expectedRemainingEscrow = escrowedAmount - escrowedUsage;

                if (IoC)
                {
                    switch (side)
                    {
                        case Buy:
                            Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == escrowedUsage - (quoteSymbol == Nexus.FuelTokenSymbol ? txCost : 0));
                            break;

                        case Sell:
                            Assert.IsTrue(Abs(OpenerBaseTokensDelta) == escrowedUsage - (baseSymbol == Nexus.FuelTokenSymbol ? txCost : 0));
                            break;
                    }
                }
                else //if the user order was not closed and it wasnt IoC, it should have the correct unfilled amount
                {
                    BigInteger actualRemainingEscrow;
                    if (expectedRemainingEscrow == 0)
                    {
                        Assert.IsTrue(wasNewOrderClosed, "Order wasn't closed but we expect no leftover escrow");
                        try
                        {
                            //should throw an exception because order should not exist
                            simulator.Nexus.RootChain.InvokeContract("exchange", "GetOrderLeftoverEscrow", createdOrderUid);
                            actualRemainingEscrow = -1;
                        }
                        catch (Exception e)
                        {
                            actualRemainingEscrow = 0;
                        }
                    }
                    else
                    {
                        actualRemainingEscrow = simulator.Nexus.RootChain.InvokeContract("exchange", "GetOrderLeftoverEscrow", createdOrderUid).AsNumber();
                    }
                    
                    Assert.IsTrue(expectedRemainingEscrow == actualRemainingEscrow);
                }


                //get the actual final balance of all addresses involved and make sure it matches the expected deltas
                var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(baseSymbol, user.Address);
                var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(quoteSymbol, user.Address);

                Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
                Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

                foreach (var entry in OtherAddressesTokensInitial)
                {
                    var otherAddressInitialTokens = entry.Value;
                    BigInteger delta = 0;

                    if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                        delta = OtherAddressesTokensDelta[entry.Key];

                    var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;

                    var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(targetSymbol, entry.Key);

                    Assert.IsTrue(otherAddressFinalTokens == delta + otherAddressInitialTokens);
                }

                return side == Buy ? UnitConversion.ToDecimal(baseTokensReceived, baseToken.Decimals) : UnitConversion.ToDecimal(quoteTokensReceived, quoteToken.Decimals);
            }

            public decimal OpenMarketOrder(decimal orderSize, ExchangeOrderSide side)
            {
                var nexus = simulator.Nexus;

                var baseSymbol = baseToken.Symbol;
                var baseDecimals = baseToken.Decimals;
                var quoteSymbol = quoteToken.Symbol;
                var quoteDecimals = quoteToken.Decimals;

                var orderToken = side == Buy ? quoteToken : baseToken;

                var orderSizeBigint = UnitConversion.ToBigInteger(orderSize, orderToken.Decimals);

                var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(baseSymbol, user.Address);
                var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(quoteSymbol, user.Address);

                BigInteger OpenerBaseTokensDelta = 0;
                BigInteger OpenerQuoteTokensDelta = 0;

                //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
                var otherSide = side == Buy ? Sell : Buy;
                var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract("exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
                var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************
                foreach (var oppositeOrder in startingOppositeOrderbook)
                {
                    if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                    {
                        var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                        OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(targetSymbol, oppositeOrder.Creator));
                    }
                }
                //--------------------------


                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "OpenMarketOrder", user.Address, baseSymbol, quoteSymbol, orderSizeBigint, side).
                        SpendGas(user.Address).EndScript());
                simulator.EndBlock();

                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
                
                BigInteger escrowedAmount = orderSizeBigint;

                //take into account the transfer of the owner's wallet to the chain address
                if (side == Buy)
                {
                    OpenerQuoteTokensDelta -= escrowedAmount;
                }
                else if (side == Sell)
                {
                    OpenerBaseTokensDelta -= escrowedAmount;
                }

                //take into account tx cost in case one of the symbols is the FuelToken
                if (baseSymbol == Nexus.FuelTokenSymbol)
                {
                    OpenerBaseTokensDelta -= txCost;
                }
                else
                if (quoteSymbol == Nexus.FuelTokenSymbol)
                {
                    OpenerQuoteTokensDelta -= txCost;
                }

                var events = nexus.FindBlockByTransaction(tx).GetEventsForTransaction(tx.Hash);

                var ordersCreated = events.Count(x => x.Kind == EventKind.OrderCreated && x.Address == user.Address);
                var wasNewOrderCreated = ordersCreated >= 1;
                Assert.IsTrue(wasNewOrderCreated, "No orders were created");

                var ordersClosed = events.Count(x => x.Kind == EventKind.OrderClosed && x.Address == user.Address);
                var wasNewOrderClosed = ordersClosed == 1;
                var wasNewOrderCancelled = events.Count(x => x.Kind == EventKind.OrderCancelled && x.Address == user.Address) == 1;

                var createdOrderEvent = events.First(x => x.Kind == EventKind.OrderCreated);
                var createdOrderUid = Serialization.Unserialize<BigInteger>(createdOrderEvent.Data);
                ExchangeOrder createdOrderPostFill = new ExchangeOrder();

                //----------------
                //verify the order does not exist in the orderbook

                //in case the new order was IoC and it wasnt closed, order should have been cancelled
                if (wasNewOrderClosed == false)
                {
                    Assert.IsTrue(wasNewOrderCancelled, "Non closed order did not get cancelled");
                }
                else
                    //if the new order was closed
                if (wasNewOrderClosed)
                {
                    Assert.IsTrue(wasNewOrderCancelled == false, "Closed order also got cancelled");
                }

                //check that the order no longer exists on the orderbook
                try
                {
                    simulator.Nexus.RootChain.InvokeContract("exchange", "GetExchangeOrder", createdOrderUid);
                    Assert.IsTrue(false, "Market order exists on the orderbooks");
                }
                catch (Exception e)
                {
                    //purposefully empty, this is the expected code-path
                }

                //------------------
                //validate that everyone received their tokens appropriately

                BigInteger escrowedUsage = 0;   //this will hold the amount of the escrowed amount that was actually used in the filling of the order
                                                //for IoC orders, we need to make sure that what wasn't used gets returned properly
                                                //for non IoC orders, we need to make sure that what wasn't used stays on the orderbook
                BigInteger baseTokensReceived = 0, quoteTokensReceived = 0;
                var OtherAddressesTokensDelta = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************

                //calculate the expected delta of the balances of all addresses involved
                var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenReceive);

                foreach (var tokenExchangeEvent in tokenExchangeEvents)
                {
                    var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                    if (tokenExchangeEvent.Address == user.Address)
                    {
                        if (eventData.symbol == baseSymbol)
                            baseTokensReceived += eventData.value;
                        else
                        if (eventData.symbol == quoteSymbol)
                            quoteTokensReceived += eventData.value;
                    }
                    else
                    {
                        Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                        if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                            OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.value;
                        else
                            OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.value);

                        escrowedUsage += eventData.value;   //the tokens other addresses receive come from the escrowed amount of the order opener
                    }
                }

                OpenerBaseTokensDelta += baseTokensReceived;
                OpenerQuoteTokensDelta += quoteTokensReceived;

                var expectedRemainingEscrow = escrowedAmount - escrowedUsage;

                switch (side)
                {
                    case Buy:
                        Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == escrowedUsage - (quoteSymbol == Nexus.FuelTokenSymbol ? txCost : 0));
                        break;

                    case Sell:
                        Assert.IsTrue(Abs(OpenerBaseTokensDelta) == escrowedUsage - (baseSymbol == Nexus.FuelTokenSymbol ? txCost : 0));
                        break;
                }

                //get the actual final balance of all addresses involved and make sure it matches the expected deltas
                var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(baseSymbol, user.Address);
                var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(quoteSymbol, user.Address);

                Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
                Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

                foreach (var entry in OtherAddressesTokensInitial)
                {
                    var otherAddressInitialTokens = entry.Value;
                    BigInteger delta = 0;

                    if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                        delta = OtherAddressesTokensDelta[entry.Key];

                    var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;

                    var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(targetSymbol, entry.Key);

                    Assert.IsTrue(otherAddressFinalTokens == delta + otherAddressInitialTokens);
                }

                return side == Buy ? UnitConversion.ToDecimal(baseTokensReceived, baseToken.Decimals) : UnitConversion.ToDecimal(quoteTokensReceived, quoteToken.Decimals);
            }

            public void FundBaseToken(decimal quantity, bool fundFuel = false) => FundUser(true, quantity, fundFuel);
            public void FundQuoteToken(decimal quantity, bool fundFuel = false) => FundUser(false, quantity, fundFuel);


            //transfers the given quantity of a specified token to this user, plus some fuel to pay for transactions
            private void FundUser(bool fundBase, decimal quantity, bool fundFuel = false)
            {
                var nexus = simulator.Nexus;
                var token = fundBase ? baseToken : quoteToken;

                simulator.BeginBlock();
                simulator.GenerateTransfer(simulatorOwner, user.Address, nexus.RootChain, token.Symbol, UnitConversion.ToBigInteger(quantity, token.Decimals));

                if (fundFuel)
                    simulator.GenerateTransfer(simulatorOwner, user.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 500000);

                simulator.EndBlock();
            }
        }

        

        #endregion
    }
}
