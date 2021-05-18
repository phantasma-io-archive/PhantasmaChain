using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Phantasma.API;
using Phantasma.VM.Utils;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;
using static Phantasma.Blockchain.Contracts.StakeContract;
using static Phantasma.Domain.DomainSettings;
using static Phantasma.Numerics.UnitConversion;

namespace Phantasma.Tests
{
    [TestClass]
    public class MarketContractTests
    {

        [TestMethod]
        public void TestMarketContractBasic()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1000;

            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(2);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "SellToken", testUser.Address, token.Symbol, DomainSettings.FuelTokenSymbol, tokenID, price, endDate).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BuyToken", owner.Address, token.Symbol, auctions[previousAuctionCount].TokenID).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");
        }

        [TestMethod]
        public void TestMarketContractAuctionDutch()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 500;
            var extensionPeriod = 300;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 3; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token as dutch auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid before auction starts (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time half way through auction
            simulator.TimeSkipDays(2);

            // make one self bid (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", testUser.Address, token.Symbol, tokenID, price + 1000, buyingFee, Address.Null).
                    SpendGas(testUser.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - 1000, " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter);
        }

        [TestMethod]
        public void TestMarketContractAuctionClassic()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();
            var testUser2 = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var bidPrice = 2500;
            var extensionPeriod = 3600;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 1; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token with a fee but no fee address (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, 2, Address.Null).
                    SpendGas(testUser.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // list token as Classic auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid before auction starts (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time post start date
            simulator.TimeSkipDays(2);

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price - 100, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice, buyingFee, Address.Null).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // make one more bid from same address
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser2.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", testUser2.Address, token.Symbol, tokenID, bidPrice - 100, buyingFee, Address.Null).
                    SpendGas(testUser2.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time 5 minutes before end of auction
            simulator.TimeSkipHours(23);
            simulator.TimeSkipMinutes(55);

            // make one bid < 1% more (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser2.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", testUser2.Address, token.Symbol, tokenID, bidPrice + 101, buyingFee, Address.Null).
                    SpendGas(testUser2.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid which will trigger extend time
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(testUser2.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", testUser2.Address, token.Symbol, tokenID, bidPrice + 200, buyingFee, Address.Null).
                SpendGas(testUser2.Address).
                EndScript()
            );
            simulator.EndBlock();

            // move time 45 minutes to check if time was properly extended
            simulator.TimeSkipMinutes(45);

            // make one more outbid, which would have claimed without extension
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice + 300, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // check if auction is still there post extension
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount + 1, "auction ids should not be empty at this point");

            // move time post end date
            simulator.TimeSkipDays(2);

            // make one bid to claim nft
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - (bidPrice + 300), " balanceOwnerBefore: " + balanceOwnerBefore + " bidPrice + 200: " + bidPrice + 300 + " balanceOwnerAfter: " + balanceOwnerAfter);
        }

        [TestMethod]
        public void TestMarketContractAuctionClassicNoWinner()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();
            var testUser2 = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var bidPrice = 2500;
            var extensionPeriod = 3600;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 1; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token with a negative price (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, -3000, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                    SpendGas(testUser.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // list token as Classic auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // move time post auction end date
            simulator.TimeSkipDays(4);

            // make one bid to claim nft
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft is back to the original owner
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the seller did not get back his NFT?");
        }

        [TestMethod]
        public void TestMarketContractAuctionReserve()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();
            var testUser2 = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser2.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var bidPrice = 2500;
            var bidPrice2 = 3500;
            var bidPrice3 = 4500;
            var extensionPeriod = 300;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 2; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            var balanceTestUserBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser.Address);
            var balanceTestUser2Before = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser2.Address);

            // list token as reserve auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid below reserve price (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid above reserve price
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice, buyingFee, Address.Null).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice - 100, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one other address outbids previous one
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser2, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(testUser2.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", testUser2.Address, token.Symbol, tokenID, bidPrice2, buyingFee, Address.Null).
                SpendGas(testUser2.Address).
                EndScript()
            );
            simulator.EndBlock();

            // move time between bids
            simulator.TimeSkipHours(2);

            // make original address outbids previous one
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice3, buyingFee, Address.Null).
                SpendGas(owner.Address).
                EndScript()
            );
            simulator.EndBlock();

            // move time post end date
            simulator.TimeSkipDays(2);

            // claim nft
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, 0, buyingFee, Address.Null).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            var balanceTestUserAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser.Address);
            var balanceTestUser2After = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, testUser2.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - bidPrice3, " balanceOwnerBefore: " + balanceOwnerBefore + " bidPrice: " + bidPrice3 + " balanceOwnerAfter: " + balanceOwnerAfter);
            Assert.IsTrue(balanceTestUserAfter == balanceTestUserBefore + bidPrice3, " balanceOwnerBefore: " + balanceTestUserBefore + " bidPrice: " + bidPrice3 + " balanceOwnerAfter: " + balanceTestUserAfter);
            Assert.IsTrue(balanceTestUser2After == balanceTestUser2Before, " balanceTestUser2After: " + balanceTestUser2After + " balanceTestUser2Before: " + balanceTestUser2Before);
        }

        [TestMethod]
        public void TestMarketContractAuctionFixed()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var bidPrice = 0;
            var extensionPeriod = 0;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 0; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token as fixed auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid before auction starts (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time post start date
            simulator.TimeSkipDays(2);

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price - 100, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one higher (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, Address.Null).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid - also claims it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price, buyingFee, Address.Null).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - price, " balanceOwnerBefore: " + balanceOwnerBefore + " price: " + price + " balanceOwnerAfter: " + balanceOwnerAfter);
        }

        [TestMethod]
        public void TestMarketContractAuctionEdit()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var extensionPeriod = 0;
            var listingFee = 0;
            var buyingFee = 0;
            var auctionType = 0; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);
            Timestamp endDateWrong = simulator.CurrentTime + TimeSpan.FromDays(1);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token as fixed auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // edit auction price with wrong symbol (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.
                        BeginScript().
                        AllowGas(testUser.Address, Address.Null, 1, 9999).
                        CallContract("market", "EditAuction", testUser.Address, DomainSettings.StakingTokenSymbol, DomainSettings.StakingTokenSymbol, tokenID, 2500, endPrice, startDate, endDate, extensionPeriod).
                        SpendGas(testUser.Address).
                        EndScript()
                    );
                simulator.EndBlock();
            });

            // edit auction price with correct symbol
            simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "EditAuction", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, 2500, 0, Timestamp.Null, Timestamp.Null, 0).
                    SpendGas(testUser.Address).
                    EndScript()
                );
            simulator.EndBlock();

            // edit auction price with incorrect end date (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
            simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "EditAuction", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, 0, 0, startDate, endDateWrong, 0).
                    SpendGas(testUser.Address).
                    EndScript()
                );
            simulator.EndBlock();
            });

            // move time post start date
            simulator.TimeSkipDays(2);

            // make one bid - also claims it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, 2500, buyingFee, Address.Null).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - 2500, " balanceOwnerBefore: " + balanceOwnerBefore + " price: " + 2500 + " balanceOwnerAfter: " + balanceOwnerAfter);
        }

        [TestMethod]
        public void TestMarketContractAuctionCancel()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var extensionPeriod = 300;
            var listingFee = 0;
            var auctionType = 1; 
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);

            // list token as classic auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, DomainSettings.StakingTokenSymbol, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, Address.Null).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // move half way through sale
            simulator.TimeSkipDays(1);

            // cancel auction from wrong owner (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                    ScriptUtils.
                        BeginScript().
                        AllowGas(owner.Address, Address.Null, 1, 9999).
                        CallContract("market", "CancelSale", token.Symbol, tokenID).
                        SpendGas(owner.Address).
                        EndScript()
                    );
                simulator.EndBlock();
            });

            // cancel auction after it started (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.
                        BeginScript().
                        AllowGas(testUser.Address, Address.Null, 1, 9999).
                        CallContract("market", "CancelSale", token.Symbol, tokenID).
                        SpendGas(testUser.Address).
                        EndScript()
                    );
                simulator.EndBlock();
            });

            // move past end date
            simulator.TimeSkipDays(2);

            // cancel auction price from current owner
            simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract("market", "CancelSale", token.Symbol, tokenID).
                    SpendGas(testUser.Address).
                    EndScript()
                );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was not moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the seller still have zero?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the buyer has it while it was cancelled?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenToSell, owner.Address);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore, " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter);

        }

        [TestMethod]
        public void TestMarketContractAuctionFeesDivisible()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();
            var buyingFeeUser = PhantasmaKeys.Generate();
            var listingFeeUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 1500;
            var endPrice = 0;
            var bidPrice = 0;
            var extensionPeriod = 0;
            var listingFee = 4;
            var buyingFee = 5;
            var auctionType = 0; 
            var listingFeeAddress = listingFeeUser.Address;
            var buyingFeeAddress = buyingFeeUser.Address;
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenTicker = DomainSettings.StakingTokenSymbol;
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, tokenTicker);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
            var balanceSellerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, testUser.Address);
            var balanceListFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
            var balanceBuyFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);

            // list token as fixed auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, tokenTicker, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, listingFeeAddress).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid before auction starts (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time post start date
            simulator.TimeSkipDays(2);

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price - 100, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one higher (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid - also claims it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price, buyingFee, buyingFeeAddress).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
            var balanceSellerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, testUser.Address);
            var balanceListFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
            var balanceBuyFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);
            Assert.IsTrue(balanceListFeeAfter == balanceListFeeBefore + (listingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceBuyFeeAfter == balanceBuyFeeBefore + (buyingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceSellerAfter == balanceSellerBefore + price, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - price - (listingFee * price / 100) - (buyingFee * price / 100), " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        }

        [TestMethod]
        public void TestMarketContractAuctionFeesIndivisible()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();
            var buyingFeeUser = PhantasmaKeys.Generate();
            var listingFeeUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 1000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, "MKNI", 10000);
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            tokenID = ownedTokenList.First();

            var price = 3;
            var endPrice = 0;
            var bidPrice = 0;
            var extensionPeriod = 0;
            var listingFee = 2;
            var buyingFee = 3;
            var auctionType = 0; 
            var listingFeeAddress = listingFeeUser.Address;
            var buyingFeeAddress = buyingFeeUser.Address;
            Timestamp startDate = simulator.CurrentTime + TimeSpan.FromDays(1);
            Timestamp endDate = simulator.CurrentTime + TimeSpan.FromDays(3);

            // verify balance before
            var tokenTicker = "MKNI";
            var tokenToSell = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, tokenTicker);
            var balanceOwnerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
            var balanceSellerBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, testUser.Address);
            var balanceListFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
            var balanceBuyFeeBefore = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);

            // list token as fixed auction
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "ListToken", testUser.Address, token.Symbol, tokenTicker, tokenID, price, endPrice, startDate, endDate, extensionPeriod, auctionType, listingFee, listingFeeAddress).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auction is here
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            // make one bid before auction starts (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, endPrice, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // move time post start date
            simulator.TimeSkipDays(2);

            // make one bid lower (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price - 100, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one higher (should fail)
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.
                    BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, bidPrice + 100, buyingFee, buyingFeeAddress).
                    SpendGas(owner.Address).
                    EndScript()
                );
                simulator.EndBlock();
            });

            // make one bid - also claims it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BidToken", owner.Address, token.Symbol, tokenID, price, buyingFee, buyingFeeAddress).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            // verify auctions empty
            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "market", "GetAuctions").ToObject();
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");

            // verify balance after
            var balanceOwnerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, owner.Address);
            var balanceSellerAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, testUser.Address);
            var balanceListFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, listingFeeAddress);
            var balanceBuyFeeAfter = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, tokenTicker, buyingFeeAddress);
            Assert.IsTrue(balanceListFeeAfter == balanceListFeeBefore + 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceBuyFeeAfter == balanceBuyFeeBefore + 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceSellerAfter == balanceSellerBefore + price, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
            Assert.IsTrue(balanceOwnerAfter == balanceOwnerBefore - price - 1 - 1, " balanceSellerBefore: " + balanceSellerBefore + " balanceSellerAfter: " + balanceSellerAfter + " balanceOwnerBefore: " + balanceOwnerBefore + " balanceOwnerAfter: " + balanceOwnerAfter + " balanceListFeeBefore: " + balanceListFeeBefore + " balanceListFeeAfter: " + balanceListFeeAfter + " balanceBuyFeeBefore: " + balanceBuyFeeBefore + " balanceBuyFeeAfter: " + balanceBuyFeeAfter);
        }
    }
}

