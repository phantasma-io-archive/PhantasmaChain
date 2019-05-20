using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Phantasma.API;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Numerics;
using static Phantasma.Blockchain.Contracts.Native.EnergyContract;
using Phantasma.VM;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Tests
{
    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void TestMarketContract()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 1000000);
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nftSymbol);
            Assert.IsTrue(nexus.TokenExists(nftSymbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(nftSymbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(owner, testUser.Address, nftSymbol, tokenROM, tokenRAM);
            simulator.EndBlock();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var price = 1000;

            Timestamp endDate = Timestamp.Now + TimeSpan.FromDays(2);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(testUser.Address, Address.Null, 1, 9999).
                  CallContract("market", "SellToken", testUser.Address, token.Symbol, Nexus.FuelTokenSymbol, tokenID, price, endDate).
                  SpendGas(testUser.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == 1 + previousAuctionCount, "auction ids missing");

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, () =>
            ScriptUtils.
                  BeginScript().
                  AllowGas(owner.Address, Address.Null, 1, 9999).
                  CallContract("market", "BuyToken", owner.Address, token.Symbol, auctions[previousAuctionCount].TokenID).
                  SpendGas(owner.Address).
                  EndScript()
            );
            simulator.EndBlock();

            auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            Assert.IsTrue(auctions.Length == previousAuctionCount, "auction ids should be empty at this point");

            // verify that the nft was really moved
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = ownerships.Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");
        }


        [TestMethod]
        public void TestEnergyRatioDecimals()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var stakeAmount = BaseEnergyRatioDivisor;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -Nexus.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double) (StakeToFuel(stakeAmount))) * Math.Pow(10, -Nexus.FuelTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -Nexus.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger) (realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == BaseEnergyRatioDivisor);
        }

        [TestMethod]
        public void TestGetUnclaimed()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var stakeAmount = BaseEnergyRatioDivisor;
            var expectedUnclaimedAmount = StakeToFuel(stakeAmount);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);

            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            }

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);

            Assert.IsTrue(unclaimedAmount == expectedUnclaimedAmount);
        }

        [TestMethod]
        public void TestUnstake()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var desiredStakeAmount = 10 * BaseEnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, desiredStakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StableTokenSymbol, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Try to reduce the staked amount via Unstake function call: should fail, not enough time passed
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = initialStakedAmount - BaseEnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Try to reduce staked amount below what is staked: should fail
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address,
                            BaseEnergyRatioDivisor * 10000000).SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);
            
            //-----------
            //Try a partial unstake: should pass
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount - BaseEnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
            
            //-----------
            //Try a full unstake: should fail, didnt wait 24h
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(startingSoulBalance == finalSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try a full unstake: should pass
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
        }

        [TestMethod]
        public void TestClaim()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = FuelToStake(100);

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var desiredStake = FuelToStake(1);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, desiredStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Perform a claim call: should pass
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount
            var previousStake = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var addedStake = FuelToStake(1);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Perform another claim call: should get reward only for the newly staked amount
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 2nd time
            previousStake = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            addedStake = FuelToStake(10);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Perform another claim call: should get reward only for the newly staked amount
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
            
            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Perform another claim call: should get reward for total staked amount
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var expectedUnclaimed = StakeToFuel(stakedAmount);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for accumulated days
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount) * days);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 3rd time
            previousStake = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            addedStake = FuelToStake(10);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for 1 day of full stake and 1 day of partial stake
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            expectedUnclaimed = StakeToFuel(stakedAmount) + StakeToFuel(addedStake);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //----------
            //Increase stake by X
            previousStake = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            addedStake = FuelToStake(10);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Total unstake
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, previousStake + addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStake = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(finalStake == 0);

            //Claim -> should get StakeToFuel(X)
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            expectedUnclaimed = StakeToFuel(addedStake);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestHalving()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var desiredStakeAmount = FuelToStake(10);
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, desiredStakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //Time skip over 4 years and 8 days
            var startDate = simulator.CurrentTime;
            simulator.CurrentTime = ((DateTime) nexus.RootChain.FindBlockByHeight(1).Timestamp).AddYears(2);
            simulator.TimeSkipDays(0);
            var firstHalvingDate = simulator.CurrentTime;
            var firstHalvingDayCount = (firstHalvingDate - startDate).Days;

            simulator.CurrentTime = simulator.CurrentTime.AddYears(2);
            simulator.TimeSkipDays(0);
            var secondHalvingDate = simulator.CurrentTime;
            var secondHalvingDayCount = (secondHalvingDate - firstHalvingDate).Days;


            var thirdHalvingDayCount = 8;
            simulator.TimeSkipDays(thirdHalvingDayCount);

            //Validate halving
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            var expectedUnclaimed = StakeToFuel(stakedAmount) * (1 + firstHalvingDayCount + (secondHalvingDayCount / 2) + (thirdHalvingDayCount / 4));
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestProxies()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var initialStake = FuelToStake(1);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == initialStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Add main account as proxy to itself: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, testUser.Address, 50).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 0% proxy: should fail
            var proxyA = KeyPair.Generate();
            var proxyAPercentage = 25;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, proxyA.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, 0).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 0);

            var api = new NexusAPI(nexus);
            var script = ScriptUtils.BeginScript().CallContract("energy", "GetProxies", testUser.Address).EndScript();
            var apiResult = api.InvokeRawScript("main", Base16.Encode(script));

            //-----------
            //Add and remove 90% proxy: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, 90).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 90);

            apiResult = api.InvokeRawScript("main", Base16.Encode(script));

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add and remove 100% proxy: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, 100).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 100);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 101% proxy: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, 101).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 25% proxy A: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 25);

            //-----------
            //Re-add proxy A: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, 25).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add an 80% proxy: should fail
            var proxyB = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, proxyB.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, proxyB.Address, 80).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 25% proxy B and remove it: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyB.Address, 25).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 2);
            Assert.IsTrue(proxyList[0].percentage == 25 && proxyList[1].percentage == 25);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "RemoveProxy", testUser.Address, proxyB.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 75% proxy B and remove it: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyB.Address, 75).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 2);
            Assert.IsTrue(proxyList[0].percentage == 25 && proxyList[1].percentage == 75);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "RemoveProxy", testUser.Address, proxyB.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Try to remove proxy B again: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "RemoveProxy", testUser.Address, proxyB.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 76% proxy B: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "AddProxy", testUser.Address, proxyB.Address, 76).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);

            //Try to claim from main: should pass
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            var startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            var startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            var finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            var finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            var proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            var leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Try to claim from main: should fail
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            Assert.IsTrue(startingFuelBalance == finalFuelBalance);

            //-----------
            //Try to claim from proxy A: should fail
            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            Assert.IsTrue(startingMainFuelBalance == finalMainFuelBalance);
            Assert.IsTrue(startingProxyFuelBalance == finalProxyFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Try to claim from proxy A: should pass, but the proxy should earn nothing
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == 1);

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(proxyA, () =>
                ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(proxyQuota == 0);
            Assert.IsTrue(leftover == 1);

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover ));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount
            var addedStake = FuelToStake(3);
            var desiredFuelClaim = StakeToFuel(initialStake + addedStake*2);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Try to claim from proxy A: should pass, and the proxy should earn 1 fuel satoshi
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount + addedStake));

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(proxyA, () =>
                ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(proxyQuota == proxyAPercentage * desiredFuelClaim / 100);
            Assert.IsTrue(desiredFuelClaim == unclaimedAmount);

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover ));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Remove proxy A
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Try to claim from proxy A: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Try to claim from main: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Try to claim from proxy A: should fail
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Try to claim from main: should pass, check removed proxy received nothing
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + unclaimedAmount - txCost));
            Assert.IsTrue(finalProxyFuelBalance == startingProxyFuelBalance);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Add 25% proxy A: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract("energy", "GetProxies", testUser.Address);
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 25);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Try to claim from main: should pass, check claimed amount is from 5 days worth of accumulation
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == days * StakeToFuel(stakedAmount));

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestVotingPower()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);
            Assert.IsTrue(actualVotingPower == 0);

            var initialStake = 10;

            //-----------
            //Try a partial unstake
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);
            Assert.IsTrue(actualVotingPower == initialStake);

            var addedStake = 20;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);
            Assert.IsTrue(actualVotingPower == initialStake + addedStake);

            var firstWait = 10;
            simulator.TimeSkipDays(firstWait);

            BigInteger expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var secondWait = 5;
            simulator.TimeSkipDays(secondWait);

            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) + (addedStake * (100 + secondWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Try a partial unstake
            var stakeReduction = 5;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) / 100;
            expectedVotingPower += ((addedStake - stakeReduction) * (100 + secondWait)) / 100;
            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Try full unstake of the last stake
            var thirdWait = 1;
            simulator.TimeSkipDays(thirdWait);

            stakeReduction = addedStake - stakeReduction;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait + thirdWait)) / 100;
            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Test max voting power bonus cap

            simulator.TimeSkipDays(1500);

            expectedVotingPower = ((initialStake + addedStake) * (100 + MaxVotingPowerBonus)) / 100;
            actualVotingPower = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetAddressVotingPower", testUser.Address);

            Assert.IsTrue(actualVotingPower == expectedVotingPower);
        }

        [TestMethod]
        public void TestStaking()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = BaseEnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //Try to stake an amount lower than EnergyRacioDivisor
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            var initialStake = FuelToStake(1) - 1;

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Stake", testUser.Address, initialStake).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //----------
            //Try to stake an amount higher than the account's balance
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Stake", testUser.Address, accountBalance * 10).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform a valid Stake call
            initialStake = FuelToStake(10);
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == initialStake);
            

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(initialStake == startingSoulBalance - finalSoulBalance);

            Assert.IsTrue(accountBalance == finalSoulBalance + stakedAmount);

            //-----------
            //Perform another valid Stake call
            var addedStake = FuelToStake(10);
            var totalExpectedStake = initialStake + addedStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == totalExpectedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(totalExpectedStake));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUser.Address);
            Assert.IsTrue(totalExpectedStake == startingSoulBalance - finalSoulBalance);
        }

        [TestMethod]
        public void TestSoulMaster()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUserA.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = MasterAccountThreshold;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //A stakes under master threshold -> verify A is not master
            var initialStake = accountBalance - FuelToStake(1);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserA.Address, initialStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserA.Address);
            Assert.IsTrue(isMaster == false);

            //-----------
            //A attempts master claim -> verify failure: not a master
            var startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //----------
            //A stakes the master threshold -> verify A is master
            var missingStake = MasterAccountThreshold - initialStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserA.Address, missingStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserA.Address);
            Assert.IsTrue(isMaster);

            //-----------
            //A attempts master claim -> verify failure: didn't wait until the 1st of the month after genesis block
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "MasterClaim", testUserA.Address).SpendGas(testUserA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A attempts master claim during the first valid staking period -> verify failure: rewards are only available at the end of each staking period
            var missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "MasterClaim", testUserA.Address).SpendGas(testUserA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A attempts master claim -> verify success
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            var claimMasterCount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetClaimMasterCount", (Timestamp)simulator.CurrentTime);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            
            var expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim -> verify failure: not enough time passed since last claim
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A unstakes under master thresold -> verify lost master status
            var stakeReduction = FuelToStake(1);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUserA.Address, stakeReduction).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserA.Address);
            Assert.IsTrue(isMaster == false);

            //-----------
            //A restakes to the master threshold -> verify won master status again
            missingStake = MasterAccountThreshold - initialStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserA.Address, missingStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserA.Address);
            Assert.IsTrue(isMaster);

            //-----------
            //Time skip to the next possible claim date
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify failure, because he lost master status once during this reward period
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //Time skip to the next possible claim date
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify success
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //Let B and C be other addresses
            var testUserB = KeyPair.Generate();
            var testUserC = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, Nexus.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //----------
            //B and C stake the master threshold -> verify both become masters

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserB.Address, accountBalance).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserC, () =>
                ScriptUtils.BeginScript().AllowGas(testUserC.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUserC.Address, accountBalance).
                    SpendGas(testUserC.Address).EndScript());
            simulator.EndBlock();

            isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserB.Address);
            Assert.IsTrue(isMaster);

            isMaster = (bool)simulator.Nexus.RootChain.InvokeContract("energy", "IsMaster", testUserC.Address);
            Assert.IsTrue(isMaster);

            //----------
            //Confirm that B and C should only receive master claim rewards on the 2nd closest claim date

            var closeClaimDate = (Timestamp) simulator.Nexus.RootChain.InvokeContract("energy", "GetMasterClaimDate", 1);
            var farClaimDate = (Timestamp) simulator.Nexus.RootChain.InvokeContract("energy", "GetMasterClaimDate", 2);

            var closeClaimMasters = (BigInteger) simulator.Nexus.RootChain.InvokeContract("energy", "GetClaimMasterCount", closeClaimDate);
            var farClaimMasters = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetClaimMasterCount", farClaimDate);
            Assert.IsTrue(closeClaimMasters == 1 && farClaimMasters == 3);

            //----------
            //Confirm in fact that only A receives rewards on the closeClaimDate
            
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            var startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            var startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserB.Address);
            var startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserC.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            expectedBalance = startingBalanceA + (MasterClaimGlobalAmount / closeClaimMasters) + (MasterClaimGlobalAmount % closeClaimMasters);
            var finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            var finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserB.Address);
            var finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserC.Address);

            Assert.IsTrue(finalBalanceA == expectedBalance);
            Assert.IsTrue(finalBalanceB == startingBalanceB);
            Assert.IsTrue(finalBalanceC == startingBalanceC);

            //----------
            //Confirm in fact that A, B and C receive rewards on the farClaimDate

            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserB.Address);
            startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserC.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var expectedBalanceA = startingBalanceA + (MasterClaimGlobalAmount / farClaimMasters) + (MasterClaimGlobalAmount % farClaimMasters);
            var expectedBalanceB = startingBalanceB + (MasterClaimGlobalAmount / farClaimMasters);
            var expectedBalanceC = startingBalanceC + (MasterClaimGlobalAmount / farClaimMasters);


            finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserA.Address);
            finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserB.Address);
            finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(Nexus.StakingTokenSymbol, testUserC.Address);

            Assert.IsTrue(finalBalanceA == expectedBalanceA);
            Assert.IsTrue(finalBalanceB == expectedBalanceB);
            Assert.IsTrue(finalBalanceC == expectedBalanceC);
        }

        [TestMethod]
        public void TestFuelStakeConversion()
        {
            var stake = 100;
            var fuel = StakeToFuel(stake);
            var stake2 = FuelToStake(fuel);
            Assert.IsTrue(stake == stake2);
        }


        [TestMethod]
        public void TestFriendsContract()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var stakeAmount = BaseEnergyRatioDivisor;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -Nexus.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double) (StakeToFuel(stakeAmount))) * Math.Pow(10, -Nexus.FuelTokenDecimals);

            var fuelToken = Nexus.FuelTokenSymbol;
            var stakingToken = Nexus.StakingTokenSymbol;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, stakingToken, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -Nexus.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger) (realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == BaseEnergyRatioDivisor);
        }

        private struct FriendTestStruct
        {
            public string name;
            public Address address;
        }

        private byte[] GetScriptForFriends(Address target)
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var fuelToken = Nexus.FuelTokenSymbol;
            var stakingToken = Nexus.StakingTokenSymbol;

            //Let A be an address
            var testUserA = KeyPair.Generate();
            var testUserB = KeyPair.Generate();
            var testUserC = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptString = new string[]
            {
                "load r0 \"friends\"",
                "ctx r0 r1",

                $"load r0 0x{Base16.Encode( target.PublicKey)}",
                "push r0",
                "extcall \"Address()\"",

                "load r0 \"GetFriends\"",
                "push r0",
                "switch r1",

                "alias r4 $friends",
                "alias r5 $address",
                "alias r6 $name",
                "alias r7 $i",
                "alias r8 $count",
                "alias r9 $loopflag",
                "alias r10 $friendname",
                "alias r11 $friendnamelist",

                "pop r0",
                "cast r0 $friends #Struct",
                "count $friends $count",

                "load $i 0",
                "@loop: ",
                "lt $i $count $loopflag",
                "jmpnot $loopflag @finish",

                "get $friends $address $i",
                "push $address",
                "call @lookup",
                "pop $name",

                "load r0 \"name\"",
                "load r1 \"address\"",
                "put $name $friendname[r0]",
                "put $address $friendname[r1]",

                "put $friendname $friendnamelist $i",

                "inc $i",
                "jmp @loop",
                "@finish: push $friendnamelist",
                "ret",

                "@lookup: load r0 \"account\"",
                "ctx r0 r1",
                "load r0 \"LookUpAddress\"",
                "push r0",
                "switch r1",
                "ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            return script;
        }

        [TestMethod]
        public void TestFriendArray()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var fuelToken = simulator.Nexus.FuelToken;
            var stakeToken = simulator.Nexus.StakingToken;

            //Let A be an address
            var testUserA = KeyPair.Generate();
            var testUserB = KeyPair.Generate();
            var testUserC = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, nexus.StakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, nexus.StakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, nexus.StakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptA = GetScriptForFriends(testUserA.Address);
            var resultA = nexus.RootChain.InvokeScript(scriptA);
            Assert.IsTrue(resultA != null);

            var tempA = resultA.ToArray<FriendTestStruct>();
            Assert.IsTrue(tempA.Length == 2);
            Assert.IsTrue(tempA[0].address == testUserB.Address);
            Assert.IsTrue(tempA[1].address == testUserC.Address);

            // we also test that the API can handle complex return types
            var api = new NexusAPI(nexus);
            var apiResult = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptA));

            // NOTE objBytes will contain a serialized VMObject
            var objBytes = Base16.Decode(apiResult.result);
            var resultB = Serialization.Unserialize<VMObject>(objBytes);

            // finally as last step, convert it to a C# struct
            var tempB = resultB.ToArray<FriendTestStruct>();
            Assert.IsTrue(tempB.Length == 2);
            Assert.IsTrue(tempB[0].address == testUserB.Address);
            Assert.IsTrue(tempB[1].address == testUserC.Address);

            // check what happens when no friends available
            var scriptB = GetScriptForFriends(testUserB.Address);
            var apiResultB = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptB));

            // NOTE objBytes will contain a serialized VMObject
            var objBytesB = Base16.Decode(apiResultB.result);
            var resultEmpty = Serialization.Unserialize<VMObject>(objBytesB);
            Assert.IsTrue(resultEmpty != null);
        }


        [TestMethod]
        public void TestGetNachoConfig()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            var fuelToken = Nexus.FuelTokenSymbol;
            var stakingToken = Nexus.StakingTokenSymbol;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            var script = ScriptUtils.BeginScript().CallContract("nacho", "GetConfig", new object[0]).EmitPop(0).Emit(Opcode.CAST, new byte[] { 0, 0, (byte)VMType.Struct }).EmitPush(0).EndScript();
            //var result = nexus.RootChain.InvokeScript(script);
            //Assert.IsTrue(result != null);
            
            var api = new NexusAPI(nexus);
            var apiResult = (ScriptResult)api.InvokeRawScript("nacho", Base16.Encode(script));

            // NOTE objBytes will contain a serialized VMObject
            var objBytes = Base16.Decode(apiResult.result);
            var resultObj = Serialization.Unserialize<VMObject>(objBytes);

            // finally as last step, convert it to a C# struct
            var userConfig = resultObj.ToStruct<NachoConfig>();

            Assert.IsTrue(userConfig.time > 0);
            Assert.IsTrue(userConfig.suspendedTransfers == false);
        }


        [TestMethod]
        public void TestGetNachoAccount()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, 100000000);
            simulator.EndBlock();

            var script = ScriptUtils.BeginScript().CallContract("nacho", "GetAccount", new object[] { testUser.Address }).EmitPop(0).Emit(Opcode.CAST, new byte[] { 0, 0, (byte)VMType.Struct }).EmitPush(0).EndScript();

            var api = new NexusAPI(nexus);
            var apiResult = (ScriptResult)api.InvokeRawScript("nacho", Base16.Encode(script));

            // NOTE objBytes will contain a serialized VMObject
            var objBytes = Base16.Decode(apiResult.result);
            var resultObj = Serialization.Unserialize<VMObject>(objBytes);

            // finally as last step, convert it to a C# struct
            var userAccount = resultObj.ToStruct<NachoAccount>();

            Assert.IsTrue(userAccount.ELO == NachoConstants.DEFAULT_ELO);
        }
    }
}

