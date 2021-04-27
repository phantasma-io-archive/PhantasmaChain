using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain.Contracts;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM.Utils;
using static Phantasma.Blockchain.Contracts.ExchangeOrderSide;
using static Phantasma.Numerics.BigInteger;
using Phantasma.Domain;
using Phantasma.Blockchain;

namespace Phantasma.Tests
{
    [TestClass]
    
    public class ExchangeTests
    {
        private static PhantasmaKeys simulatorOwner = PhantasmaKeys.Generate();
        private static NexusSimulator simulatorOld;
        private static Nexus nexus;

        private const string maxDivTokenSymbol = "MADT";        //divisible token with maximum decimal count
        private const string minDivTokenSymbol = "MIDT";        //divisible token with minimum decimal count
        private const string nonDivisibleTokenSymbol = "NDT";

        [Ignore]
        [TestMethod]
        public void TestIoCLimitMinimumQuantity()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices at the limit

            var minimumBaseToken = UnitConversion.ToDecimal(simulatorOld.Nexus.RootChain.InvokeContract(simulatorOld.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
            var minimumQuoteToken = UnitConversion.ToDecimal(simulatorOld.Nexus.RootChain.InvokeContract(simulatorOld.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

            buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Buy, IoC: true);
            seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Sell, IoC: true);

            buyer.OpenLimitOrder(1m, 1m, Buy);
            buyer.OpenLimitOrder(1m, 1m, Buy);
            Assert.IsTrue(seller.OpenLimitOrder(1m + (minimumBaseToken*.99m), 1m, Sell) == 1m, "Used leftover under minimum quantity");
        }

        [Ignore]
        [TestMethod]
        public void TestIoCLimitOrderUnmatched()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test unmatched IoC orders 
            seller.OpenLimitOrder(0.01m, 0.5m, Sell);
            buyer.OpenLimitOrder(0.01m, 0.1m, Buy);
            Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
            Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
        }

        [Ignore]
        [TestMethod]
        public void TestIoCLimitOrderCompleteFulfilment()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test fully matched IoC orders
            buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
        }

        [Ignore]
        [TestMethod]
        public void TestIoCLimitOrderPartialFulfilment()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test partially matched IoC orders
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
        }

        [Ignore]
        [TestMethod]
        public void TestIoCLimitOrderMultipleFulfilsPerOrder()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test multiple fills per order
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 2m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 3m, Buy, IoC: false);
            buyer.OpenLimitOrder(0.05m, 0.5m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.15m, 1m, Sell, IoC: true) == 0.3m, "Unexpected amount of tokens received");

            core = new CoreClass();
            buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            seller = new ExchangeUser(baseSymbol, quoteSymbol, core);
            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 2m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 3m, Sell, IoC: false);
            seller.OpenLimitOrder(0.05m, 0.5m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.15m, 3m, Buy, IoC: true) == 0.2m, "Unexpected amount of tokens received");

            //TODO: test multiple IoC orders against each other on the same block!
        }

        [Ignore]
        [TestMethod]
        public void TestFailedIOC()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

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

        [Ignore]
        [TestMethod]
        public void TestLimitMinimumQuantity()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test order amount and prices at the limit

            var minimumBaseToken = UnitConversion.ToDecimal(simulatorOld.Nexus.RootChain.InvokeContract(simulatorOld.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.baseToken).AsNumber(), buyer.baseToken.Decimals);
            var minimumQuoteToken = UnitConversion.ToDecimal(simulatorOld.Nexus.RootChain.InvokeContract(simulatorOld.Nexus.RootStorage, "exchange", "GetMinimumTokenQuantity", buyer.quoteToken).AsNumber(), buyer.baseToken.Decimals);

            buyer.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Buy);
            seller.OpenLimitOrder(minimumBaseToken, minimumQuoteToken, Sell);
        }

        [Ignore]
        [TestMethod]
        public void TestLimitOrderUnmatched()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test unmatched IoC orders 
            seller.OpenLimitOrder(0.01m, 0.5m, Sell);
            buyer.OpenLimitOrder(0.01m, 0.1m, Buy);
            Assert.IsTrue(buyer.OpenLimitOrder(0.123m, 0.3m, Buy, IoC: true) == 0, "Shouldn't have filled any part of the order");
            Assert.IsTrue(seller.OpenLimitOrder(0.123m, 0.3m, Sell, IoC: true) == 0, "Shouldn't have filled any part of the order");
        }

        [Ignore]
        [TestMethod]
        public void TestLimitOrderCompleteFulfilment()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test fully matched IoC orders
            buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.1m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.1m, "Unexpected amount of tokens received");
        }

        [Ignore]
        [TestMethod]
        public void TestLimitOrderPartialFulfilment()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            //-----------------------------------------
            //test partially matched IoC orders
            buyer.OpenLimitOrder(0.05m, 1m, Buy, IoC: false);
            Assert.IsTrue(seller.OpenLimitOrder(0.1m, 1m, Sell, IoC: true) == 0.05m, "Unexpected amount of tokens received");

            seller.OpenLimitOrder(0.05m, 1m, Sell, IoC: false);
            Assert.IsTrue(buyer.OpenLimitOrder(0.1m, 1m, Buy, IoC: true) == 0.05m, "Unexpected amount of tokens received");
        }

        [Ignore]
        [TestMethod]
        public void TestLimitOrderMultipleFulfilsPerOrder()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

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

        [Ignore]
        [TestMethod]
        public void TestFailedRegular()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

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

        [Ignore]
        [TestMethod]
        public void TestEmptyBookMarketOrder()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            Assert.IsTrue(buyer.OpenMarketOrder(1, Buy) == 0, "Should not have bought anything");
        }

        [Ignore]
        [TestMethod]
        public void TestMarketOrderPartialFill()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.2m, 1m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.3m, Buy) == 0.2m, "");
        }

        [Ignore]
        [TestMethod]
        public void TestMarketOrderCompleteFulfilment()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.1m, 1m, Sell);
            seller.OpenLimitOrder(0.1m, 2m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.3m, Buy) == 0.2m, "");
        }

        [Ignore]
        [TestMethod]
        public void TestMarketOrderTotalFillNoOrderbookWipe()
        {
            InitExchange();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = maxDivTokenSymbol;

            var buyer = new ExchangeUser(baseSymbol, quoteSymbol);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol);

            buyer.FundQuoteToken(quantity: 2m, fundFuel: true);
            seller.FundBaseToken(quantity: 2m, fundFuel: true);

            seller.OpenLimitOrder(0.1m, 1m, Sell);
            seller.OpenLimitOrder(0.1m, 2m, Sell);
            Assert.IsTrue(buyer.OpenMarketOrder(0.25m, Buy) == 0.175m, "");
        }

        [TestMethod]
        public void TestOpenOTCOrder()
        {
            CoreClass core = new CoreClass();

            // Setup symbols
            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            // Create users
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            seller.FundUser(soul: 5000m, kcal: 5000m);

            // Get Initial Balance
            var initialBalance = seller.GetBalance(baseSymbol);

            // Verify my Funds
            Assert.IsTrue(initialBalance == UnitConversion.ToBigInteger(5000m, GetDecimals(baseSymbol)));

            // Create OTC Offer
            var txValue = seller.OpenOTCOrder(baseSymbol, quoteSymbol, 10m, 20m);

            // Test if the seller lost money.
            var finalBalance = seller.GetBalance(baseSymbol);

            Assert.IsFalse(initialBalance == finalBalance);

            // Test if lost the quantity used
            var subtractSpendToken = initialBalance - UnitConversion.ToBigInteger(20m, GetDecimals(baseSymbol));
            Assert.IsTrue(subtractSpendToken == finalBalance);
        }

        [TestMethod]
        public void TestGetOTC()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            // Create users
            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            buyer.FundUser(soul: 5000m, kcal: 5000m);
            seller.FundUser(soul: 5000m, kcal: 5000m);

            // Test Empty OTC
            var initialOTC = seller.GetOTC();

            var empytOTC = new ExchangeOrder[0];

            Assert.IsTrue(initialOTC.Length == 0);

            // Create an Order
            seller.OpenOTCOrder(baseSymbol, quoteSymbol, 1m, 1m);

            // Test if theres an order
            var finallOTC = seller.GetOTC();

            Assert.IsTrue(initialOTC != finallOTC);
        }


        [TestMethod]
        public void TestTakeOTCOrder()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            // Create users
            var buyer = new ExchangeUser(baseSymbol, quoteSymbol, core);
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            buyer.FundUser(soul: 5000m, kcal: 5000m);
            seller.FundUser(soul: 5000m, kcal: 5000m);

            // Get Initial Balance
            var initialBuyer_B = buyer.GetBalance(baseSymbol);
            var initialBuyer_Q = buyer.GetBalance(quoteSymbol);
            var initialSeller_B = seller.GetBalance(baseSymbol);
            var initialSeller_Q = seller.GetBalance(quoteSymbol);

            // Create Order
            var sellerTXFees = seller.OpenOTCOrder(baseSymbol, quoteSymbol, 5m, 10m);

            // Test if Seller lost balance
            var finalSeller_B = seller.GetBalance(baseSymbol);

            Assert.IsFalse(initialSeller_B == finalSeller_B);

            // Test if lost the quantity used
            Assert.IsTrue((initialSeller_B - UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalSeller_B);

            // Take an Order
            // Get Order UID
            var orderUID = seller.GetOTC().First<ExchangeOrder>().Uid;
            var buyerTXFees = buyer.TakeOTCOrder(orderUID);

            // Check if the order is taken
            var finalSeller_Q = seller.GetBalance(quoteSymbol);
            var finalBuyer_B = buyer.GetBalance(baseSymbol);
            var finalBuyer_Q = buyer.GetBalance(quoteSymbol);

            // Consider Transactions Fees

            // Test seller received
            Assert.IsTrue((initialSeller_Q + UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - sellerTXFees) == finalSeller_Q);

            // Test Buyer spend and receibed
            Assert.IsTrue((initialBuyer_B + UnitConversion.ToBigInteger(10m, GetDecimals(baseSymbol))) == finalBuyer_B);
            Assert.IsTrue((initialBuyer_Q - UnitConversion.ToBigInteger(5m, GetDecimals(quoteSymbol)) - buyerTXFees) == finalBuyer_Q);

        }

        [TestMethod]
        public void TestCancelOTCOrder()
        {
            CoreClass core = new CoreClass();

            var baseSymbol = DomainSettings.StakingTokenSymbol;
            var quoteSymbol = DomainSettings.FuelTokenSymbol;

            // Create users
            var seller = new ExchangeUser(baseSymbol, quoteSymbol, core);

            // Give Users tokens
            seller.FundUser(soul: 5000m, kcal: 5000m);

            // Get Initial Balance
            var initialBalance = seller.GetBalance(baseSymbol);

            // Create OTC Offer
            seller.OpenOTCOrder(baseSymbol, quoteSymbol, 10m, 50m);

            // Test if the seller lost money.
            var finalBalance = seller.GetBalance(baseSymbol);

            Assert.IsFalse(initialBalance == finalBalance);

            // Test if lost the quantity used
            Assert.IsTrue((initialBalance - UnitConversion.ToBigInteger(50m, GetDecimals(baseSymbol))) == finalBalance);

            // Cancel Order
            // Get Order UID
            var orderUID = seller.GetOTC().First<ExchangeOrder>().Uid;
            seller.CancelOTCOrder(orderUID);

            // Test if the token is back;
            var atualBalance = seller.GetBalance(baseSymbol);

            Assert.IsTrue(initialBalance == atualBalance);
        }

        #region AuxFunctions

        private static int GetDecimals(string symbol)
        {
            switch (symbol)
            {
                case "SOUL": return 8;
                case "KCAL": return 10;
                default: throw new System.Exception("Unknown decimals for " + symbol);
            }
        }

        private void CreateTokens()
        {
            string[] tokenList = { maxDivTokenSymbol, nonDivisibleTokenSymbol };

            simulatorOld.BeginBlock();

            foreach (var symbol in tokenList)
            {
                int decimals = 0;
                BigInteger supply = 0;
                TokenFlags flags = TokenFlags.Divisible;

                switch (symbol)
                {
                    case maxDivTokenSymbol:
                        decimals = DomainSettings.MAX_TOKEN_DECIMALS;
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

                simulatorOld.GenerateToken(simulatorOwner, symbol, $"{symbol}Token", supply, decimals, flags);
                simulatorOld.MintTokens(simulatorOwner, simulatorOwner.Address, symbol, supply);
            }

            simulatorOld.EndBlock();
        }

        private void InitExchange()
        {
            simulatorOwner = PhantasmaKeys.Generate();
            nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulatorOld = new NexusSimulator(nexus, simulatorOwner, 1234);
            //CreateTokens();
        }

        class CoreClass
        {
            public PhantasmaKeys owner;
            public NexusSimulator simulator;
            public Nexus nexus;

            public CoreClass()
            {
                InitExchange();
            }

            private void InitExchange()
            {
                owner = PhantasmaKeys.Generate();
                nexus = new Nexus("simnet", null, null);
                nexus.SetOracleReader(new OracleSimulator(nexus));
                simulator = new NexusSimulator(nexus, owner, 1234);
            }
        }

        class ExchangeUser
        {
            private readonly PhantasmaKeys user;
            public IToken baseToken;
            public IToken quoteToken;
            public PhantasmaKeys userKeys;
            public CoreClass core;
            public NexusSimulator simulator;
            public Nexus nexus;
            
            public enum TokenType { Base, Quote}

            public ExchangeUser(string baseSymbol, string quoteSymbol,  CoreClass core = null)
            {
                user = PhantasmaKeys.Generate();
                userKeys = user;
                this.core = core;
                simulator = core.simulator;
                nexus = core.nexus;
                baseToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, baseSymbol);
                quoteToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, quoteSymbol);
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

                var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
                var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

                BigInteger OpenerBaseTokensDelta = 0;
                BigInteger OpenerQuoteTokensDelta = 0;

                //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
                var otherSide = side == Buy ? Sell : Buy;
                var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
                var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************
                foreach (var oppositeOrder in startingOppositeOrderbook)
                {
                    if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                    {
                        var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                        var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);
                        OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, oppositeOrder.Creator));
                    }
                }
                //--------------------------


                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "OpenLimitOrder", user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, orderPriceBigint, side, IoC).
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
                if (baseSymbol == DomainSettings.FuelTokenSymbol)
                {
                    OpenerBaseTokensDelta -= txCost;
                }
                else
                if (quoteSymbol == DomainSettings.FuelTokenSymbol)
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
                        simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid);
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
                        createdOrderPostFill = (ExchangeOrder)simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid).ToObject();
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
                var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenClaim);

                foreach (var tokenExchangeEvent in tokenExchangeEvents)
                {
                    var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                    if (tokenExchangeEvent.Address == user.Address)
                    {
                        if(eventData.Symbol == baseSymbol)
                            baseTokensReceived += eventData.Value;
                        else
                        if(eventData.Symbol == quoteSymbol)
                            quoteTokensReceived += eventData.Value;
                    }
                    else
                    {
                        Console.WriteLine("tokenExchangeEvent.Contract " + tokenExchangeEvent.Contract);
                        Console.WriteLine("tokenExchangeEvent.Address " + tokenExchangeEvent.Address);
                        Console.WriteLine("tokenExchangeEvent.Address2 " + SmartContract.GetAddressForNative(NativeContractKind.Exchange));
                        Console.WriteLine("tokenExchangeEvent.Address gas " + SmartContract.GetAddressForName(Nexus.GasContractName));
                        Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                        if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                            OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.Value;
                        else
                            OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.Value);

                        escrowedUsage += eventData.Value;   //the tokens other addresses receive come from the escrowed amount of the order opener
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
                            Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == escrowedUsage - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                            break;

                        case Sell:
                            Assert.IsTrue(Abs(OpenerBaseTokensDelta) == escrowedUsage - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
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
                            simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderLeftoverEscrow", createdOrderUid);
                            actualRemainingEscrow = -1;
                        }
                        catch (Exception e)
                        {
                            actualRemainingEscrow = 0;
                        }
                    }
                    else
                    {
                        actualRemainingEscrow = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderLeftoverEscrow", createdOrderUid).AsNumber();
                    }
                    
                    Assert.IsTrue(expectedRemainingEscrow == actualRemainingEscrow);
                }


                //get the actual final balance of all addresses involved and make sure it matches the expected deltas
                var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
                var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

                Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
                Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

                foreach (var entry in OtherAddressesTokensInitial)
                {
                    var otherAddressInitialTokens = entry.Value;
                    BigInteger delta = 0;

                    if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                        delta = OtherAddressesTokensDelta[entry.Key];

                    var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                    var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                    var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

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

                var OpenerBaseTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
                var OpenerQuoteTokensInitial = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

                BigInteger OpenerBaseTokensDelta = 0;
                BigInteger OpenerQuoteTokensDelta = 0;

                //get the starting balance for every address on the opposite side of the orderbook, so we can compare it to the final balance of each of those addresses
                var otherSide = side == Buy ? Sell : Buy;
                var startingOppositeOrderbook = (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOrderBook", baseSymbol, quoteSymbol, otherSide).ToObject();
                var OtherAddressesTokensInitial = new Dictionary<Address, BigInteger>();

                //*******************************************************************************************************************************************************************************
                //*** the following method to check token balance state only works for the scenario of a single new exchange order per block that triggers other pre-existing exchange orders ***
                //*******************************************************************************************************************************************************************************
                foreach (var oppositeOrder in startingOppositeOrderbook)
                {
                    if (OtherAddressesTokensInitial.ContainsKey(oppositeOrder.Creator) == false)
                    {
                        var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                        var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);
                        OtherAddressesTokensInitial.Add(oppositeOrder.Creator, simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, oppositeOrder.Creator));
                    }
                }
                //--------------------------


                if (side == Buy)
                {
                    Console.WriteLine("buy now");
                }
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "OpenMarketOrder", user.Address, user.Address, baseSymbol, quoteSymbol, orderSizeBigint, side).
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
                if (baseSymbol == DomainSettings.FuelTokenSymbol)
                {
                    OpenerBaseTokensDelta -= txCost;
                }
                else
                if (quoteSymbol == DomainSettings.FuelTokenSymbol)
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
                    simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetExchangeOrder", createdOrderUid);
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

                Console.WriteLine("event count: " + events.Count());
                foreach (var evt in events)
                {
                    Console.WriteLine("kind: " + evt.Kind);
                }
                var tokenExchangeEvents = events.Where(x => x.Kind == EventKind.TokenClaim);
                Console.WriteLine("exchange event count: " + tokenExchangeEvents.Count());

                foreach (var tokenExchangeEvent in tokenExchangeEvents)
                {
                    var eventData = Serialization.Unserialize<TokenEventData>(tokenExchangeEvent.Data);

                    if (tokenExchangeEvent.Address == user.Address)
                    {
                        if (eventData.Symbol == baseSymbol)
                            baseTokensReceived += eventData.Value;
                        else
                        if (eventData.Symbol == quoteSymbol)
                            quoteTokensReceived += eventData.Value;
                    }
                    else
                    {
                        Assert.IsTrue(OtherAddressesTokensInitial.ContainsKey(tokenExchangeEvent.Address), "Address that was not on this orderbook received tokens");

                        if (OtherAddressesTokensDelta.ContainsKey(tokenExchangeEvent.Address))
                            OtherAddressesTokensDelta[tokenExchangeEvent.Address] += eventData.Value;
                        else
                            OtherAddressesTokensDelta.Add(tokenExchangeEvent.Address, eventData.Value);

                        escrowedUsage += eventData.Value;   //the tokens other addresses receive come from the escrowed amount of the order opener
                    }
                }

                OpenerBaseTokensDelta += baseTokensReceived;
                OpenerQuoteTokensDelta += quoteTokensReceived;

                var expectedRemainingEscrow = escrowedAmount - escrowedUsage;
                //Console.WriteLine("expectedRemainingEscrow: " + expectedRemainingEscrow);

                switch (side)
                {
                    case Buy:
                        //Console.WriteLine($"{Abs(OpenerQuoteTokensDelta)} == {escrowedUsage} - {(quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0)}");
                        Assert.IsTrue(Abs(OpenerQuoteTokensDelta) == expectedRemainingEscrow - (quoteSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;

                    case Sell:
                        Assert.IsTrue(Abs(OpenerBaseTokensDelta) == expectedRemainingEscrow - (baseSymbol == DomainSettings.FuelTokenSymbol ? txCost : 0));
                        break;
                }

                //get the actual final balance of all addresses involved and make sure it matches the expected deltas
                var OpenerBaseTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, baseToken, user.Address);
                var OpenerQuoteTokensFinal = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, quoteToken, user.Address);

                Console.WriteLine($"final: {OpenerBaseTokensFinal} == {OpenerBaseTokensDelta} + {OpenerBaseTokensInitial}");
                Assert.IsTrue(OpenerBaseTokensFinal == OpenerBaseTokensDelta + OpenerBaseTokensInitial);
                Assert.IsTrue(OpenerQuoteTokensFinal == OpenerQuoteTokensDelta + OpenerQuoteTokensInitial);

                foreach (var entry in OtherAddressesTokensInitial)
                {
                    var otherAddressInitialTokens = entry.Value;
                    BigInteger delta = 0;

                    if (OtherAddressesTokensDelta.ContainsKey(entry.Key))
                        delta = OtherAddressesTokensDelta[entry.Key];

                    var targetSymbol = otherSide == Buy ? baseSymbol : quoteSymbol;
                    var targetToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, targetSymbol);

                    var otherAddressFinalTokens = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, targetToken, entry.Key);

                    Assert.IsTrue(otherAddressFinalTokens == delta + otherAddressInitialTokens);
                }

                return side == Buy ? UnitConversion.ToDecimal(baseTokensReceived, baseToken.Decimals) : UnitConversion.ToDecimal(quoteTokensReceived, quoteToken.Decimals);
            }

            #region OTC
            public BigInteger OpenOTCOrder(string baseSymbol, string quoteSymbol, decimal amount, decimal price)
            {
                var amountBigint = UnitConversion.ToBigInteger(amount, GetDecimals(quoteSymbol));
                var priceBigint = UnitConversion.ToBigInteger(price, GetDecimals(baseSymbol));

                // Create OTC Order
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "OpenOTCOrder", user.Address, baseSymbol, quoteSymbol, amountBigint, priceBigint).
                        SpendGas(user.Address).EndScript());
                simulator.EndBlock();

                // Get Tx Cost
                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
                return txCost;
            }

            public BigInteger TakeOTCOrder(BigInteger uid)
            {
                // Take an Order
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "TakeOrder", user.Address, uid).
                        SpendGas(user.Address).EndScript());
                simulator.EndBlock();

                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);
                return txCost;
            }

            public void CancelOTCOrder(BigInteger uid)
            {
                // Take an Order
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(user.Address, Address.Null, 1, 9999)
                        .CallContract("exchange", "CancelOTCOrder", user.Address, uid).
                        SpendGas(user.Address).EndScript());
                simulator.EndBlock();
            }

            // Get OTC Orders
            public ExchangeOrder[] GetOTC()
            {
                return (ExchangeOrder[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "exchange", "GetOTC").ToObject();
            }
            #endregion

            public void FundUser(decimal soul, decimal kcal)
            {
                simulator.BeginBlock();
                var txA = simulator.GenerateTransfer(core.owner, user.Address, simulator.Nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(soul, DomainSettings.StakingTokenDecimals));
                var txB = simulator.GenerateTransfer(core.owner, user.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(kcal, DomainSettings.FuelTokenDecimals));
                simulator.EndBlock();
            }


            public void FundBaseToken(decimal quantity, bool fundFuel = false) => FundUser(true, quantity, fundFuel);
            public void FundQuoteToken(decimal quantity, bool fundFuel = false) => FundUser(false, quantity, fundFuel);


            //transfers the given quantity of a specified token to this user, plus some fuel to pay for transactions
            private void FundUser(bool fundBase, decimal quantity, bool fundFuel = false)
            {
                var nexus = simulator.Nexus;
                var token = fundBase ? baseToken : quoteToken;

                simulator.BeginBlock();
                simulator.GenerateTransfer(core.owner, user.Address, nexus.RootChain, token.Symbol, UnitConversion.ToBigInteger(quantity, GetDecimals(token.Symbol)));

                if (fundFuel)
                    simulator.GenerateTransfer(core.owner, user.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 500000);

                simulator.EndBlock();
            }

            public BigInteger GetBalance(string symbol)
            {
                var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
                return nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, user.Address);
            }
        }

        

        #endregion
    }
}
