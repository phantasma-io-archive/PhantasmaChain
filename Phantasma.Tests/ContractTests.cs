using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Phantasma.Blockchain.Storage;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Blockchain;
using Phantasma.Numerics;
using static Phantasma.Blockchain.Contracts.Native.EnergyContract;

namespace Phantasma.Tests
{
    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void TestMarketContract()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 1000000);
            simulator.GenerateToken(owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.FindTokenBySymbol(nftSymbol);
            Assert.IsTrue(token != null, "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken directly on the user
            simulator.BeginBlock();
            simulator.GenerateNft(owner, testUser.Address, chain, token, tokenROM, tokenRAM);
            simulator.EndBlock();

            var auctions = (MarketAuction[])simulator.Nexus.RootChain.InvokeContract("market", "GetAuctions");
            var previousAuctionCount = auctions.Length;

            // verify nft presence on the user post-mint
            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
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
            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 0, "How does the seller still have one?");

            ownedTokenList = chain.GetTokenOwnerships(token).Get(chain.Storage, owner.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the buyer does not have what he bought?");
        }

        [TestMethod]
        public void TestEnergyRatioDecimals()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var stakeAmount = EnergyRatioDivisor;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -Nexus.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double) (StakeToFuel(stakeAmount))) * Math.Pow(10, -Nexus.FuelTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, stakeAmount);
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
            Assert.IsTrue(actualEnergyRatio == EnergyRatioDivisor);
        }

        [TestMethod]
        public void TestGetUnclaimed()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var testUser = KeyPair.Generate();
            var stakeAmount = EnergyRatioDivisor;
            var expectedUnclaimedAmount = StakeToFuel(stakeAmount);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, stakeAmount);
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

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var fuelToken = simulator.Nexus.FuelToken;
            var stakeToken = simulator.Nexus.StakingToken;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = EnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var desiredStakeAmount = 10 * EnergyRatioDivisor;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, desiredStakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Try to reduce the staked amount via Unstake function call: should fail, not enough time passed
            var initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            var stakeReduction = initialStakedAmount - EnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

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

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Try to reduce staked amount below what is staked: should fail
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address,
                            EnergyRatioDivisor * 10000000).SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkip(1);
            
            //-----------
            //Try a partial unstake: should pass
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount - EnergyRatioDivisor;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
            
            //-----------
            //Try a full unstake: should fail, didnt wait 24h
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Unstake", testUser.Address, stakeReduction).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(startingSoulBalance == finalSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            //-----------
            //Time skip 1 day
            simulator.TimeSkip(1);

            //-----------
            //Try a full unstake: should pass
            initialStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
        }

        [TestMethod]
        public void TestClaim()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var fuelToken = simulator.Nexus.FuelToken;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = FuelToStake(100);

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, accountBalance);
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
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
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
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
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
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
            
            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkip(1);

            //Perform another claim call: should get reward for total staked amount
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkip(days);

            //Perform another claim call: should get reward for accumulated days
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount) * days);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestProxies()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var fuelToken = simulator.Nexus.FuelToken;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = EnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, accountBalance);
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
            simulator.GenerateTransfer(owner, proxyA.Address, nexus.RootChain, nexus.FuelToken, 100000000);
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
            simulator.GenerateTransfer(owner, proxyB.Address, nexus.RootChain, nexus.FuelToken, 100000000);
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

            var startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            var startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            var finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            var finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);
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

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", testUser.Address, testUser.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            Assert.IsTrue(startingFuelBalance == finalFuelBalance);

            //-----------
            //Try to claim from proxy A: should fail
            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            Assert.IsTrue(startingMainFuelBalance == finalMainFuelBalance);
            Assert.IsTrue(startingProxyFuelBalance == finalProxyFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkip(1);

            //Try to claim from proxy A: should pass, but the proxy should earn nothing
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == 1);

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(proxyA, () =>
                ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);
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
            var desiredFuelClaim = StakeToFuel(initialStake + addedStake);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Time skip 1 day
            simulator.TimeSkip(1);

            //Try to claim from proxy A: should pass, and the proxy should earn 1 fuel satoshi
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(proxyA, () =>
                ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);
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
            simulator.TimeSkip(1);

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

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);
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
            simulator.TimeSkip(days);

            //Try to claim from main: should pass, check claimed amount is from 5 days worth of accumulation
            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            stakedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetStake", testUser.Address);

            Assert.IsTrue(unclaimedAmount == days * StakeToFuel(stakedAmount));

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("energy", "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(fuelToken, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

            unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestStaking()
        {
            var owner = KeyPair.Generate();

            var simulator = new ChainSimulator(owner, 1234, -1);
            var nexus = simulator.Nexus;

            var fuelToken = simulator.Nexus.FuelToken;
            var stakeToken = simulator.Nexus.StakingToken;

            var testUser = KeyPair.Generate();
            var unclaimedAmount = (BigInteger)simulator.Nexus.RootChain.InvokeContract("energy", "GetUnclaimed", testUser.Address);
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = EnergyRatioDivisor * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.FuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, nexus.StakingToken, accountBalance);
            simulator.EndBlock();

            //Try to stake an amount lower than EnergyRacioDivisor
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
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

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
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
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);

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

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
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

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(stakeToken, testUser.Address);
            Assert.IsTrue(totalExpectedStake == startingSoulBalance - finalSoulBalance);
        }

        [TestMethod]
        public void TestFuelStakeConversion()
        {
            var stake = 100;
            var fuel = StakeToFuel(stake);
            var stake2 = FuelToStake(fuel);
            Assert.IsTrue(stake == stake2);
        }
    }
}

