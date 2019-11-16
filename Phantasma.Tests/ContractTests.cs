using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using Phantasma.API;
using Phantasma.VM.Utils;
using Phantasma.Contracts.Native;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Core.Types;
using Phantasma.Blockchain;
using Phantasma.CodeGen.Assembler;
using Phantasma.Numerics;
using static Phantasma.Contracts.Native.StakeContract;
using Phantasma.VM;
using Phantasma.Storage;
using Phantasma.Blockchain.Tokens;
using Phantasma.Contracts.Extra;
using Phantasma.Blockchain.Contracts;
using static Phantasma.Domain.DomainSettings;
using static Phantasma.Numerics.UnitConversion;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class ContractTests
    {
        [TestMethod]
        public void CustomEvents()
        {
            var A = NachoEvent.Buff;
            EventKind evt = DomainExtensions.EncodeCustomEvent(A);
            var B = evt.DecodeCustomEvent<NachoEvent>();
            Assert.IsTrue(A == B);
        }

        [TestMethod]
        public void TestMarketContract()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1000000);
            simulator.GenerateToken(owner, symbol, "CoolToken", DomainSettings.PlatformName, Hash.FromString(symbol), 0, 0, Domain.TokenFlags.Transferable);
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
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM);
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
        public void TestEnergyRatioDecimals()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == BaseEnergyRatioDivisor);
        }

        [TestMethod]
        public void TestGetUnclaimed()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            var expectedUnclaimedAmount = StakeToFuel(stakeAmount);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();

            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            }

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == expectedUnclaimedAmount);
        }

        [TestMethod]
        public void TestUnstake()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var desiredStakeAmount = 10 * MinimumValidStake;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, desiredStakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Try to reduce the staked amount via Unstake function call: should fail, not enough time passed
            var initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            var stakeReduction = initialStakedAmount - MinimumValidStake;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Try to reduce staked amount below what is staked: should fail
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            stakeReduction = stakedAmount * 2;

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address,
                            stakeReduction).SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //-----------
            //Try a full unstake: should fail, didnt wait 24h
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(startingSoulBalance == finalSoulBalance);

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try a partial unstake: should pass
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            stakeReduction = initialStakedAmount - MinimumValidStake;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try a full unstake: should pass
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            stakeReduction = initialStakedAmount;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(stakeReduction == finalSoulBalance - startingSoulBalance);

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
        }

        [TestMethod]
        public void TestFreshAddressStakeClaim()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 10;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            simulator.TimeSkipToDate(DateTime.UtcNow);

            //-----------
            //Perform a valid Stake & Claim call
            var desiredStake = MinimumValidStake;
            var t1 = simulator.CurrentTime;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript()
                    .CallContract(Nexus.StakeContractName, nameof(StakeContract.Stake), testUser.Address, desiredStake)
                    .CallContract(Nexus.StakeContractName, nameof(StakeContract.Claim),testUser.Address, testUser.Address)
                    .AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .SpendGas(testUser.Address).EndScript());
            var blocks = simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            BigInteger stakedAmount =
                simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(kcalBalance == StakeToFuel(stakedAmount) - txCost);
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            var startingFuelBalance = kcalBalance;

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(startingFuelBalance == finalFuelBalance);
        }

        [TestMethod]
        public void TestClaim()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 10;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            simulator.TimeSkipToDate(DateTime.UtcNow);

            //-----------
            //Perform a valid Stake call
            var desiredStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, desiredStake).
                    SpendGas(testUser.Address).EndScript());
            var blocks = simulator.EndBlock();

            //simulator.TimeSkipToDate(blocks.ElementAt(0).Timestamp);


            BigInteger stakedAmount =
                simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Perform a claim call: should pass
            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

            //-----------
            //Increase the staked amount
            var previousStake = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            var addedStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Perform another claim call: should get reward only for the newly staked amount
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 2nd time
            previousStake = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            addedStake = MinimumValidStake * 3;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Perform another claim call: should get reward only for the newly staked amount
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform another claim call: should fail, not enough time passed between claim calls
            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Perform another claim call: should get reward for total staked amount
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(stakedAmount);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for accumulated days
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount) * days);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 3rd time
            previousStake = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            addedStake = MinimumValidStake * 2;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //-----------
            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for 1 day of full stake and 1 day of partial stake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(stakedAmount) + StakeToFuel(addedStake);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //----------
            //Increase stake by X
            previousStake = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            addedStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake));

            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Total unstake
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, previousStake + addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalStake = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(finalStake == 0);

            //Claim -> should get StakeToFuel(X) for same day reward and StakeToFuel(X + previous stake) due to full 1 day staking reward before unstake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(addedStake * 2 + previousStake);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount =
                simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestUnclaimedAccumulation()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            simulator.TimeSkipToDate(DateTime.UtcNow);

            var stakeUnit = MinimumValidStake;
            var rewardPerStakeUnit = StakeToFuel(stakeUnit);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeUnit).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeUnit);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == rewardPerStakeUnit);

            //-----------
            //Time skip 4 days: make sure appropriate stake reward accumulation 
            var days = 4;
            simulator.TimeSkipDays(days);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            var expectedUnclaimed = rewardPerStakeUnit * (days + 1);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            //Perform another stake call
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeUnit).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            expectedUnclaimed = rewardPerStakeUnit * (days + 2);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);
        }

        [TestMethod]
        public void TestHalving()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();


            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //Perform a valid Stake call
            var desiredStakeAmount = MinimumValidStake * 10;
            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, desiredStakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //Time skip over 4 years and 8 days
            var startDate = simulator.CurrentTime;
            var firstBlockHash = nexus.RootChain.GetBlockHashAtHeight(1);
            var firstBlock = nexus.RootChain.GetBlockByHash(firstBlockHash);

            simulator.CurrentTime = ((DateTime)firstBlock.Timestamp).AddYears(2);
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
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            var expectedUnclaimed = StakeToFuel(stakedAmount) * (1 + firstHalvingDayCount + (secondHalvingDayCount / 2) + (thirdHalvingDayCount / 4));
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestProxies()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //Perform a valid Stake call
            var initialStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == initialStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            //-----------
            //Add main account as proxy to itself: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, testUser.Address, 50).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 0% proxy: should fail
            var proxyA = PhantasmaKeys.Generate();
            var proxyAPercentage = 25;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, proxyA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, 0).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 0);

            var api = new NexusAPI(nexus);
            var script = ScriptUtils.BeginScript().CallContract(Nexus.StakeContractName, "GetProxies", testUser.Address).EndScript();
            var apiResult = api.InvokeRawScript("main", Base16.Encode(script));

            //-----------
            //Add and remove 90% proxy: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, 90).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 90);

            apiResult = api.InvokeRawScript("main", Base16.Encode(script));

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add and remove 100% proxy: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, 100).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 100);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 101% proxy: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, 101).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 0);

            //-----------
            //Add 25% proxy A: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 25);

            //-----------
            //Re-add proxy A: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, 25).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add an 80% proxy: should fail
            var proxyB = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, proxyB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyB.Address, 80).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 25% proxy B and remove it: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyB.Address, 25).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 2);
            Assert.IsTrue(proxyList[0].percentage == 25 && proxyList[1].percentage == 25);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyB.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 75% proxy B and remove it: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyB.Address, 75).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 2);
            Assert.IsTrue(proxyList[0].percentage == 25 && proxyList[1].percentage == 75);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyB.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Try to remove proxy B again: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyB.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //-----------
            //Add 76% proxy B: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyB.Address, 76).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);

            //Try to claim from main: should pass
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            var startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            var finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            var finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            var proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            var leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Try to claim from main: should fail, less than 24h since last claim
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(startingFuelBalance == finalFuelBalance);

            //-----------
            //Try to claim from proxy A: should fail, less than 24h since last claim
            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            Assert.IsTrue(startingMainFuelBalance == finalMainFuelBalance);
            Assert.IsTrue(startingProxyFuelBalance == finalProxyFuelBalance);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Try to claim from proxy A: should pass, and the proxy should earn some fuel
            var desiredFuelClaim = StakeToFuel(initialStake);
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            var expectedUnclaimed = StakeToFuel(stakedAmount);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(proxyQuota == proxyAPercentage * desiredFuelClaim / 100);
            Assert.IsTrue(desiredFuelClaim == unclaimedAmount);

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota - txCost));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Remove proxy A
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "RemoveProxy", testUser.Address, proxyA.Address).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Try to claim from proxy A: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Try to claim from main: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //Try to claim from proxy A: should fail
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(proxyA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(proxyA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", proxyA.Address, testUser.Address).SpendGas(proxyA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            //-----------
            //Try to claim from main: should pass, check removed proxy received nothing
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + unclaimedAmount - txCost));
            Assert.IsTrue(finalProxyFuelBalance == startingProxyFuelBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Add 25% proxy A: should pass
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "AddProxy", testUser.Address, proxyA.Address, proxyAPercentage).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            proxyList = (EnergyProxy[])simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetProxies", simulator.CurrentTime, testUser.Address).ToObject();
            Assert.IsTrue(proxyList.Length == 1);
            Assert.IsTrue(proxyList[0].percentage == 25);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Try to claim from main: should pass, check claimed amount is from 5 days worth of accumulation
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == days * StakeToFuel(stakedAmount));

            startingMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            startingProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            finalMainFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            finalProxyFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, proxyA.Address);
            txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            proxyQuota = proxyAPercentage * unclaimedAmount / 100;
            leftover = unclaimedAmount - proxyQuota;

            Assert.IsTrue(finalMainFuelBalance == (startingMainFuelBalance + leftover - txCost));
            Assert.IsTrue(finalProxyFuelBalance == (startingProxyFuelBalance + proxyQuota));

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);
        }

        [TestMethod]
        public void TestVotingPower()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 5000;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(actualVotingPower == 0);

            var MinimumVotingStake = MinimumValidStake * 1000;
            Assert.IsTrue(accountBalance >= MinimumVotingStake);

            var initialStake = MinimumVotingStake;

            //-----------
            //Perform stake operation
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(actualVotingPower == initialStake);

            //-----------
            //Perform stake operation
            var addedStake = MinimumVotingStake * 2;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(actualVotingPower == initialStake + addedStake);

            //-----------
            //Skip 10 days
            var firstWait = 10;
            simulator.TimeSkipDays(firstWait);

            //-----------
            //Check current voting power
            BigInteger expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //------------
            //Perform stake operation
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            //-----------
            //Skip 5 days
            var secondWait = 5;
            simulator.TimeSkipDays(secondWait);

            //-----------
            //Check current voting power
            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) + (addedStake * (100 + secondWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Try a partial unstake
            var stakeReduction = MinimumVotingStake;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait)) / 100;
            expectedVotingPower += ((addedStake - stakeReduction) * (100 + secondWait)) / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Try full unstake of the last stake
            var thirdWait = 1;
            simulator.TimeSkipDays(thirdWait);

            stakeReduction = addedStake - stakeReduction;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUser.Address, stakeReduction).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait + secondWait + thirdWait)) / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Test max voting power bonus cap

            simulator.TimeSkipDays(1500);

            expectedVotingPower = ((initialStake + addedStake) * (100 + MaxVotingPowerBonus)) / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetAddressVotingPower", simulator.CurrentTime, testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);
        }

        [TestMethod]
        public void TestStaking()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //Try to stake an amount lower than EnergyRacioDivisor
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            var initialStake = FuelToStake(1) - 1;

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, initialStake).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(finalSoulBalance == startingSoulBalance);

            //----------
            //Try to stake an amount higher than the account's balance
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, accountBalance * 10).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Perform a valid Stake call
            initialStake = MinimumValidStake * 10;
            startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, initialStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == initialStake);


            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(initialStake == startingSoulBalance - finalSoulBalance);

            Assert.IsTrue(accountBalance == finalSoulBalance + stakedAmount);

            //-----------
            //Perform another valid Stake call
            var addedStake = MinimumValidStake * 10;
            var totalExpectedStake = initialStake + addedStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == totalExpectedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(totalExpectedStake));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(totalExpectedStake == startingSoulBalance - finalSoulBalance);
        }

        [TestMethod]
        public void TestSoulMaster()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //-----------
            //A stakes under master threshold -> verify A is not master
            var initialStake = accountBalance - MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, initialStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            Assert.IsTrue(isMaster == false);

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            //-----------
            //A attempts master claim -> verify failure: not a master
            var startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //----------
            //A stakes the master threshold -> verify A is master
            var masterAccountThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            var missingStake = masterAccountThreshold - initialStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, missingStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            //-----------
            //A attempts master claim -> verify failure: didn't wait until the 1st of the month after genesis block
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).SpendGas(testUserA.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A attempts master claim during the first valid staking period -> verify success: rewards should be available at the end of mainnet's release month
            var missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();


            var expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim after another month of staking -> verify success
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            claimMasterCount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();


            expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //-----------
            //A attempts master claim -> verify failure: not enough time passed since last claim
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //A unstakes under master thresold -> verify lost master status
            var stakeReduction = MinimumValidStake;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Unstake", testUserA.Address, stakeReduction).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            Assert.IsTrue(isMaster == false);

            //-----------
            //A restakes to the master threshold -> verify won master status again
            missingStake = masterAccountThreshold - initialStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, missingStake).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            //-----------
            //Time skip to the next possible claim date
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify failure, because he lost master status once during this reward period
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });

            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == startingBalance);

            //-----------
            //Time skip to the next possible claim date
            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify success
            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalBalance == expectedBalance);

            //Let B and C be other addresses
            var testUserB = PhantasmaKeys.Generate();
            var testUserC = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //----------
            //B and C stake the master threshold -> verify both become masters

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserB.Address, accountBalance).
                    SpendGas(testUserB.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserC, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserC.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserC.Address, accountBalance).
                    SpendGas(testUserC.Address).EndScript());
            simulator.EndBlock();

            isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserB.Address).AsBool();
            Assert.IsTrue(isMaster);

            isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserC.Address).AsBool();
            Assert.IsTrue(isMaster);

            //----------
            //Confirm that B and C should only receive master claim rewards on the 2nd closest claim date

            var closeClaimDate = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetMasterClaimDate", simulator.CurrentTime, 1).AsTimestamp();
            var farClaimDate = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetMasterClaimDate", simulator.CurrentTime, 2).AsTimestamp();

            var closeClaimMasters = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, closeClaimDate).AsNumber();
            var farClaimMasters = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, farClaimDate).AsNumber();
            Assert.IsTrue(closeClaimMasters == 1 && farClaimMasters == 3);

            //----------
            //Confirm in fact that only A receives rewards on the closeClaimDate

            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            var startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            var startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            expectedBalance = startingBalanceA + (MasterClaimGlobalAmount / closeClaimMasters) + (MasterClaimGlobalAmount % closeClaimMasters);
            var finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            var finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            Assert.IsTrue(finalBalanceA == expectedBalance);
            Assert.IsTrue(finalBalanceB == startingBalanceB);
            Assert.IsTrue(finalBalanceC == startingBalanceC);

            //----------
            //Confirm in fact that A, B and C receive rewards on the farClaimDate

            missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var expectedBalanceA = startingBalanceA + (MasterClaimGlobalAmount / farClaimMasters) + (MasterClaimGlobalAmount % farClaimMasters);
            var expectedBalanceB = startingBalanceB + (MasterClaimGlobalAmount / farClaimMasters);
            var expectedBalanceC = startingBalanceC + (MasterClaimGlobalAmount / farClaimMasters);


            finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            Assert.IsTrue(finalBalanceA == expectedBalanceA);
            Assert.IsTrue(finalBalanceB == expectedBalanceB);
            Assert.IsTrue(finalBalanceC == expectedBalanceC);
        }

        [TestMethod]
        public void TestBigStakes()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            var masterAccountThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
            BigInteger accountBalance = 2 * masterAccountThreshold;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            //----------
            //A stakes twice the master threshold -> verify A is master
            
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, accountBalance).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            //-----------
            //Perform a claim call: should pass
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUserA.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(accountBalance);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Claim", testUserA.Address, testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);
            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            Assert.IsTrue(finalFuelBalance == (startingFuelBalance + unclaimedAmount - txCost));

            var stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == accountBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip to the next possible claim date
            var missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify success
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, (Timestamp)simulator.CurrentTime).AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var expectedSoulBalance = startingSoulBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            Assert.IsTrue(finalSoulBalance == expectedSoulBalance);
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
        public void TestSwaping()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
            simulator.EndBlock();

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            var startingKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            BigInteger swapAmount = UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals) / 100;

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUser.Address, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var currentSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            var currentKcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(currentSoulBalance < startingSoulBalance);
            Assert.IsTrue(currentKcalBalance > startingKcalBalance);
        }

        [TestMethod]
        public void TestFriendsContract()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, stakingToken, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == BaseEnergyRatioDivisor);
        }

        private struct FriendTestStruct
        {
            public string name;
            public Address address;
        }

        private byte[] GetScriptForFriends(Address target)
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();
            var testUserC = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptString = new string[]
            {
                "load r0 \"friends\"",
                "ctx r0 r1",

                $"load r0 0x{Base16.Encode(target.ToByteArray())}",
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
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var fuelToken = DomainSettings.FuelTokenSymbol;
            var stakingToken = DomainSettings.StakingTokenSymbol;

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();
            var testUserC = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, fuelToken, 100000000);
            simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, stakingToken, 100000000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserB.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("friends", "AddFriend", testUserA.Address, testUserC.Address).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var scriptA = GetScriptForFriends(testUserA.Address);
            var resultA = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptA);
            Assert.IsTrue(resultA != null);

            var tempA = resultA.ToArray<FriendTestStruct>();
            Assert.IsTrue(tempA.Length == 2);
            Assert.IsTrue(tempA[0].address == testUserB.Address);
            Assert.IsTrue(tempA[1].address == testUserC.Address);

            // we also test that the API can handle complex return types
            var api = new NexusAPI(nexus);
            var apiResult = (ScriptResult)api.InvokeRawScript("main", Base16.Encode(scriptA));

            // NOTE objBytes will contain a serialized VMObject
            var objBytes = Base16.Decode(apiResult.results[0]);
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
            var objBytesB = Base16.Decode(apiResultB.results[0]);
            var resultEmpty = Serialization.Unserialize<VMObject>(objBytesB);
            Assert.IsTrue(resultEmpty != null);
        }

        [TestMethod]
        public void TestInfiniteTokenTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, "INFI", "infinity token", "phantasma",
                Hash.FromString("infinity stable token"), BigInteger.Zero, 8,
            TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible);
            simulator.EndBlock();

            var user = PhantasmaKeys.Generate();
            var nexus = simulator.Nexus;

            var infiToken = nexus.GetTokenInfo(nexus.RootStorage, "INFI");

            var infiAmount = 1000 * GetUnitValue(infiToken.Decimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, FuelTokenSymbol, 100000000);
            simulator.MintTokens(owner, owner.Address, infiToken.Symbol, infiAmount);
            simulator.EndBlock();

            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, infiToken, owner.Address);
            Assert.IsTrue(balance == infiAmount);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, user.Address, nexus.RootChain, infiToken.Symbol, infiAmount);
            simulator.EndBlock();

            balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, infiToken, user.Address);
            Assert.IsTrue(balance == infiAmount);
        }

           
    }
}

