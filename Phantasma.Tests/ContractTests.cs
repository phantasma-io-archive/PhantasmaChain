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
    public class ContractTests
    {
        public enum CustomEvent
        {
            None,
            Stuff = 20,
        }

        [TestMethod]
        public void CustomEvents()
        {
            var A = CustomEvent.Stuff;
            EventKind evt = DomainExtensions.EncodeCustomEvent(A);
            var B = evt.DecodeCustomEvent<CustomEvent>();
            Assert.IsTrue(A == B);
        }

        [TestMethod]
        public void TestSale()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var saleUser = PhantasmaKeys.Generate();
            var saleBuyer = PhantasmaKeys.Generate();
            var otherSaleBuyer = PhantasmaKeys.Generate();

            var stakeAmount = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals);
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, saleUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, saleUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);

            simulator.GenerateTransfer(owner, saleBuyer.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, saleBuyer.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);

            simulator.GenerateTransfer(owner, otherSaleBuyer.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, otherSaleBuyer.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            var saleSymbol = "DANK";
            var decimals = 18;
            var supply = UnitConversion.ToBigInteger(2000000, decimals);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, saleSymbol, "Dank Token", supply, decimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.MintTokens(owner, saleUser.Address, saleSymbol, supply);
            simulator.EndBlock();

            var oldSellerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, "SOUL", saleUser.Address);

            var saleRate = 3;

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(saleUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(saleUser.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.CreateSale), saleUser.Address, "Dank pre-sale", SaleFlags.Whitelist, (Timestamp) simulator.CurrentTime, (Timestamp)( simulator.CurrentTime + TimeSpan.FromDays(2)), saleSymbol, "SOUL", saleRate, 0, supply, 0, UnitConversion.ToBigInteger(1500/saleRate, decimals)).
                    SpendGas(saleUser.Address).EndScript());
            var block = simulator.EndBlock().First();

            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
            var saleHash = resultObj.AsInterop<Hash>();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(saleUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(saleUser.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.AddToWhitelist), saleHash, saleBuyer.Address)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.AddToWhitelist), saleHash, otherSaleBuyer.Address).
                    SpendGas(saleUser.Address).EndScript());
            simulator.EndBlock().First();

            var purchaseAmount = UnitConversion.ToBigInteger(50, DomainSettings.StakingTokenDecimals);
            BigInteger expectedAmount = 0;

            var baseToken = nexus.GetTokenInfo(nexus.RootStorage, "SOUL");
            var quoteToken = nexus.GetTokenInfo(nexus.RootStorage, saleSymbol);

            for (int i=1; i<=3; i++)
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, 1, 9999)
                        .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), saleBuyer.Address, saleHash, "SOUL", purchaseAmount).
                        SpendGas(saleBuyer.Address).EndScript());
                simulator.EndBlock().First();

                expectedAmount += saleRate * DomainExtensions.ConvertBaseToQuote(null, purchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);

                resultObj = nexus.RootChain.InvokeContract(nexus.RootStorage, NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
                var raisedAmount = resultObj.AsNumber();

                Assert.IsTrue(raisedAmount == expectedAmount);

                resultObj = nexus.RootChain.InvokeContract(nexus.RootStorage, NativeContractKind.Sale, nameof(SaleContract.GetPurchasedAmount), saleHash, saleBuyer.Address);
                var purchasedAmount = resultObj.AsNumber();

                Assert.IsTrue(purchasedAmount == expectedAmount);
            }

            Assert.ThrowsException<ChainException>(() =>
           {
               simulator.BeginBlock();
               simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
                   ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, 1, 9999)
                       .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), saleBuyer.Address, saleHash, "SOUL", purchaseAmount).
                       SpendGas(saleBuyer.Address).EndScript());
               simulator.EndBlock().First();
           });

            var otherPurchaseAmount = UnitConversion.ToBigInteger(150, DomainSettings.StakingTokenDecimals);

            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(otherSaleBuyer, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(otherSaleBuyer.Address, Address.Null, 1, 9999)
                        .CallContract(NativeContractKind.Sale, nameof(SaleContract.Purchase), otherSaleBuyer.Address, saleHash, "SOUL", otherPurchaseAmount).
                        SpendGas(otherSaleBuyer.Address).EndScript());
                simulator.EndBlock().First();

                expectedAmount += saleRate * DomainExtensions.ConvertBaseToQuote(null, otherPurchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);

                resultObj = nexus.RootChain.InvokeContract(nexus.RootStorage, NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
                var raisedAmount = resultObj.AsNumber();

                Assert.IsTrue(raisedAmount == expectedAmount);
            }

            simulator.TimeSkipDays(4);

            resultObj = nexus.RootChain.InvokeContract(nexus.RootStorage, NativeContractKind.Sale, nameof(SaleContract.GetSoldAmount), saleHash);
            var totalSoldAmount = resultObj.AsNumber();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(saleBuyer, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(saleBuyer.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Sale, nameof(SaleContract.CloseSale), saleBuyer.Address, saleHash).
                    SpendGas(saleBuyer.Address).EndScript());
            simulator.EndBlock().First();

            var buyerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, saleSymbol, saleBuyer.Address);
            BigInteger expectedBalance = 3 * saleRate * DomainExtensions.ConvertBaseToQuote(null, purchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);
            //Assert.IsTrue(buyerBalance == expectedBalance);

            var otherBuyerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, saleSymbol, saleBuyer.Address);
            expectedBalance = saleRate * DomainExtensions.ConvertBaseToQuote(null, otherPurchaseAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken);
            //Assert.IsTrue(otherBuyerBalance == expectedBalance);

            var newSellerBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, "SOUL", saleUser.Address);

            var totalRaisedAmount = DomainExtensions.ConvertQuoteToBase(null, totalSoldAmount, UnitConversion.GetUnitValue(decimals), baseToken, quoteToken) / saleRate;

            expectedBalance = oldSellerBalance + totalRaisedAmount;
            Assert.IsTrue(newSellerBalance == expectedBalance);
        }

        [TestMethod]
        public void TestEnergyRatioDecimals()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

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

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == DefaultEnergyRatioDivisor);
        }

        [TestMethod]
        public void TestGetUnclaimed()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            var expectedUnclaimedAmount = StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();

            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            }

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == expectedUnclaimedAmount);
        }

        [TestMethod]
        public void TestUnstake()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

            //-----------
            //Try to reduce the staked amount via Unstake function call: should fail, not enough time passed
            var initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
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

            var finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();

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
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
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

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount == finalStakedAmount);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try a partial unstake: should pass
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
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

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);

            //-----------
            //Time skip 1 day
            simulator.TimeSkipDays(1);

            //-----------
            //Try a full unstake: should pass
            initialStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
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

            finalStakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(initialStakedAmount - finalStakedAmount == stakeReduction);
        }

        [TestMethod]
        public void TestFreshAddressStakeClaim()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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
                simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            var kcalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Assert.IsTrue(kcalBalance == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) - txCost);
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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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
                simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();

            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

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

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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
            var previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var addedStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 2nd time
            previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            addedStake = MinimumValidStake * 3;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor);
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

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip 5 days
            var days = 5;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for accumulated days
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) * days);

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

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Increase the staked amount a 3rd time
            previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            addedStake = MinimumValidStake * 2;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

            //-----------
            //Time skip 1 day
            days = 1;
            simulator.TimeSkipDays(days);

            //Perform another claim call: should get reward for 1 day of full stake and 1 day of partial stake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) + StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
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

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //----------
            //Increase stake by X
            previousStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            addedStake = MinimumValidStake;

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, addedStake).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == previousStake + addedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(addedStake, DefaultEnergyRatioDivisor));

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

            var finalStake = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(finalStake == 0);

            //Claim -> should get StakeToFuel(X) for same day reward and StakeToFuel(X + previous stake) due to full 1 day staking reward before unstake
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            expectedUnclaimed = StakeToFuel(addedStake, DefaultEnergyRatioDivisor);
            Console.WriteLine($"unclaimed: {unclaimedAmount} - expected {expectedUnclaimed}");
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Console.WriteLine("TESTSTATATATAT");
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Claim", testUser.Address, testUser.Address).SpendGas(testUser.Address)
                        .EndScript());
                simulator.EndBlock();
            });

            finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);

            Console.WriteLine("final: " + finalFuelBalance);
            Assert.IsTrue(finalFuelBalance == startingFuelBalance);
        }

        [TestMethod]
        public void TestUnclaimedAccumulation()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 100;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            simulator.TimeSkipToDate(DateTime.UtcNow);

            var stakeUnit = MinimumValidStake;
            var rewardPerStakeUnit = StakeToFuel(stakeUnit, DefaultEnergyRatioDivisor);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeUnit).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == stakeUnit);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == rewardPerStakeUnit);

            //-----------
            //Time skip 4 days: make sure appropriate stake reward accumulation 
            var days = 4;
            simulator.TimeSkipDays(days);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            var expectedUnclaimed = rewardPerStakeUnit * (days + 1);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);

            //Perform another stake call
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeUnit).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            expectedUnclaimed = rewardPerStakeUnit * (days + 2);
            Assert.IsTrue(unclaimedAmount == expectedUnclaimed);
        }

        [TestMethod]
        public void TestHalving()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == desiredStakeAmount);

            var finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(desiredStakeAmount == startingSoulBalance - finalSoulBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

            //Time skip over 4 years and 8 days
            var startDate = simulator.CurrentTime;
            var firstBlockHash = nexus.RootChain.GetBlockHashAtHeight(1);
            var firstBlock = nexus.RootChain.GetBlockByHash(firstBlockHash);

            simulator.CurrentTime = ((DateTime)firstBlock.Timestamp).AddYears(2);
            var firstHalvingDate = simulator.CurrentTime;
            var firstHalvingDayCount = (firstHalvingDate - startDate).Days;

            simulator.CurrentTime = simulator.CurrentTime.AddYears(2);
            var secondHalvingDate = simulator.CurrentTime;
            var secondHalvingDayCount = (secondHalvingDate - firstHalvingDate).Days;

            var thirdHalvingDayCount = 8;
            simulator.TimeSkipDays(thirdHalvingDayCount);

            //Validate halving
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();

            var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor) * (1 + firstHalvingDayCount + (secondHalvingDayCount) + (thirdHalvingDayCount));
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

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);
        }


/*        [TestMethod]
        public void TestProxies()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

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
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

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
            var desiredFuelClaim = StakeToFuel(initialStake, DefaultEnergyRatioDivisor);
            unclaimedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetUnclaimed", simulator.CurrentTime, testUser.Address).AsNumber();
            stakedAmount = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetStake", simulator.CurrentTime, testUser.Address).AsNumber();

            var expectedUnclaimed = StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor);
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

            Assert.IsTrue(unclaimedAmount == days * StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

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
        }*/

        [TestMethod]
        public void TestVotingPower()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            var accountBalance = MinimumValidStake * 5000;

            Transaction tx = null;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            var actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();
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

            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();
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

            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();
            Assert.IsTrue(actualVotingPower == initialStake + addedStake);

            //-----------
            //Skip 10 days
            var firstWait = 10;
            simulator.TimeSkipDays(firstWait);

            //-----------
            //Check current voting power
            BigInteger expectedVotingPower = ((initialStake + addedStake) * (100 + firstWait));
            expectedVotingPower = expectedVotingPower / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();

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
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();

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
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();

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
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);

            //-----------
            //Test max voting power bonus cap

            simulator.TimeSkipDays(1500);

            expectedVotingPower = ((initialStake + addedStake) * (100 + MaxVotingPowerBonus)) / 100;
            actualVotingPower = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetAddressVotingPower", testUser.Address).AsNumber();

            Assert.IsTrue(actualVotingPower == expectedVotingPower);
        }

        [TestMethod]
        public void TestStaking()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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
            var initialStake = FuelToStake(1, DefaultEnergyRatioDivisor) - 1;

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, initialStake).
                        SpendGas(testUser.Address).EndScript());
                simulator.EndBlock();
            });

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
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

            BigInteger stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == initialStake);


            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(stakedAmount, DefaultEnergyRatioDivisor));

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

            stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUser.Address).AsNumber();
            Assert.IsTrue(stakedAmount == totalExpectedStake);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == StakeToFuel(totalExpectedStake, DefaultEnergyRatioDivisor));

            finalSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(totalExpectedStake == startingSoulBalance - finalSoulBalance);
        }

        [TestMethod]
        public void TestClaimWithCrown()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            Transaction tx = null;

            BigInteger accountBalance = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, accountBalance).
                    SpendGas(testUserA.Address).EndScript());
            simulator.EndBlock();

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "IsMaster", testUserA.Address).AsBool();
            Assert.IsTrue(isMaster == true);

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var rewardToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.RewardTokenSymbol);

            var stakeTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var rewardTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken, testUserA.Address);
            simulator.TimeSkipDays(90, false);

            simulator.TimeSkipDays(90, false);

            rewardTokenBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, rewardToken, testUserA.Address);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUserA.Address).AsNumber();
            var fuelAmount = StakeContract.StakeToFuel(stakedAmount, 500)*181;

            Assert.IsTrue(fuelAmount == unclaimedAmount);
            simulator.TimeSkipDays(1, false);

            fuelAmount = StakeContract.StakeToFuel(stakedAmount, 500)*182;
            var bonus = (fuelAmount * 5) / 100; 
            var dailyBonus = bonus / 182;

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
            Assert.IsTrue((fuelAmount + dailyBonus) == unclaimedAmount);
        }

        [TestMethod]
        public void TestSoulMaster()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
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

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "IsMaster", testUserA.Address).AsBool();
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

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "IsMaster", testUserA.Address).AsBool();
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
            Console.WriteLine("before test: sim current: " + simulator.CurrentTime + " missing days: " + missingDays);
            simulator.TimeSkipDays(missingDays, true);
            //simulator.TimeSkipHours(1);
            Console.WriteLine("after test: sim current: " + simulator.CurrentTime);

            startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetClaimMasterCount", (Timestamp)simulator.CurrentTime).AsNumber();

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
            claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetClaimMasterCount", (Timestamp)simulator.CurrentTime).AsNumber();

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

            Console.WriteLine("before should throw");
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
                        SpendGas(testUserA.Address).EndScript());
                simulator.EndBlock();
            });
            Console.WriteLine("after should throw");

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

            isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "IsMaster", testUserA.Address).AsBool();
            Assert.IsTrue(isMaster == false);

            ////-----------
            ////A restakes to the master threshold -> verify won master status again
            //missingStake = masterAccountThreshold - initialStake;

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "Stake", testUserA.Address, missingStake).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserA.Address).AsBool();
            //Assert.IsTrue(isMaster);

            ////-----------
            ////Time skip to the next possible claim date
            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            ////-----------
            ////A attempts master claim -> verify failure, because he lost master status once during this reward period
            //startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.ThrowsException<ChainException>(() =>
            //{
            //    simulator.BeginBlock();
            //    simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //        ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
            //            .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
            //            SpendGas(testUserA.Address).EndScript());
            //    simulator.EndBlock();
            //});

            //finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.IsTrue(finalBalance == startingBalance);

            ////-----------
            ////Time skip to the next possible claim date
            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            ////-----------
            ////A attempts master claim -> verify success
            //startingBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //expectedBalance = startingBalance + (MasterClaimGlobalAmount / claimMasterCount) + (MasterClaimGlobalAmount % claimMasterCount);
            //finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);

            //Assert.IsTrue(finalBalance == expectedBalance);

            ////Let B and C be other addresses
            //var testUserB = PhantasmaKeys.Generate();
            //var testUserC = PhantasmaKeys.Generate();

            //simulator.BeginBlock();
            //simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            //simulator.GenerateTransfer(owner, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            //simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            //simulator.GenerateTransfer(owner, testUserC.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            //simulator.EndBlock();

            ////----------
            ////B and C stake the master threshold -> verify both become masters

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserB.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "Stake", testUserB.Address, accountBalance).
            //        SpendGas(testUserB.Address).EndScript());
            //simulator.EndBlock();

            //simulator.BeginBlock();
            //tx = simulator.GenerateCustomTransaction(testUserC, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserC.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "Stake", testUserC.Address, accountBalance).
            //        SpendGas(testUserC.Address).EndScript());
            //simulator.EndBlock();

            //isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserB.Address).AsBool();
            //Assert.IsTrue(isMaster);

            //isMaster = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "IsMaster", simulator.CurrentTime, testUserC.Address).AsBool();
            //Assert.IsTrue(isMaster);

            ////----------
            ////Confirm that B and C should only receive master claim rewards on the 2nd closest claim date

            //var closeClaimDate = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetMasterClaimDate", simulator.CurrentTime, 1).AsTimestamp();
            //var farClaimDate = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetMasterClaimDate", simulator.CurrentTime, 2).AsTimestamp();

            //var closeClaimMasters = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, closeClaimDate).AsNumber();
            //var farClaimMasters = simulator.Nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, Nexus.StakeContractName, "GetClaimMasterCount", simulator.CurrentTime, farClaimDate).AsNumber();
            //Assert.IsTrue(closeClaimMasters == 1 && farClaimMasters == 3);

            ////----------
            ////Confirm in fact that only A receives rewards on the closeClaimDate

            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            //var startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //var startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //var startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //expectedBalance = startingBalanceA + (MasterClaimGlobalAmount / closeClaimMasters) + (MasterClaimGlobalAmount % closeClaimMasters);
            //var finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //var finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //var finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //Assert.IsTrue(finalBalanceA == expectedBalance);
            //Assert.IsTrue(finalBalanceB == startingBalanceB);
            //Assert.IsTrue(finalBalanceC == startingBalanceC);

            ////----------
            ////Confirm in fact that A, B and C receive rewards on the farClaimDate

            //missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month, 1).AddMonths(1) - simulator.CurrentTime).Days + 1;
            //simulator.TimeSkipDays(missingDays, true);

            //startingBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //startingBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //startingBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //simulator.BeginBlock();
            //simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            //    ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
            //        .CallContract(Nexus.StakeContractName, "MasterClaim", testUserA.Address).
            //        SpendGas(testUserA.Address).EndScript());
            //simulator.EndBlock();

            //var expectedBalanceA = startingBalanceA + (MasterClaimGlobalAmount / farClaimMasters) + (MasterClaimGlobalAmount % farClaimMasters);
            //var expectedBalanceB = startingBalanceB + (MasterClaimGlobalAmount / farClaimMasters);
            //var expectedBalanceC = startingBalanceC + (MasterClaimGlobalAmount / farClaimMasters);


            //finalBalanceA = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            //finalBalanceB = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            //finalBalanceC = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserC.Address);

            //Assert.IsTrue(finalBalanceA == expectedBalanceA);
            //Assert.IsTrue(finalBalanceB == expectedBalanceB);
            //Assert.IsTrue(finalBalanceC == expectedBalanceC);
        }

        [TestMethod]
        public void TestBigStakes()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            //Let A be an address
            var testUserA = PhantasmaKeys.Generate();
            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
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

            var isMaster = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "IsMaster", testUserA.Address).AsBool();
            Assert.IsTrue(isMaster);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);

            //-----------
            //Perform a claim call: should pass
            var startingFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
            var expectedUnclaimed = StakeToFuel(accountBalance, DefaultEnergyRatioDivisor);
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

            var stakedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetStake", testUserA.Address).AsNumber();
            Assert.IsTrue(stakedAmount == accountBalance);

            unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUserA.Address).AsNumber();
            Assert.IsTrue(unclaimedAmount == 0);

            //-----------
            //Time skip to the next possible claim date
            var missingDays = (new DateTime(simulator.CurrentTime.Year, simulator.CurrentTime.Month + 1, 1) - simulator.CurrentTime).Days + 1;
            simulator.TimeSkipDays(missingDays, true);

            //-----------
            //A attempts master claim -> verify success
            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);

            var startingSoulBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserA.Address);
            var claimMasterCount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetClaimMasterCount", (Timestamp)simulator.CurrentTime).AsNumber();

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
            var fuel = StakeToFuel(stake, DefaultEnergyRatioDivisor);
            var stake2 = FuelToStake(fuel, DefaultEnergyRatioDivisor);
            //20, 10, 8
            Console.WriteLine($"{stake2} - {fuel}: {UnitConversion.ConvertDecimals(fuel, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals)}");
            Assert.IsTrue(stake == stake2);
        }


        [TestMethod]
        public void TestSwaping()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();
            var stakeAmount = MinimumValidStake;
            double realStakeAmount = ((double)stakeAmount) * Math.Pow(10, -DomainSettings.StakingTokenDecimals);
            double realExpectedUnclaimedAmount = ((double)(StakeToFuel(stakeAmount, DefaultEnergyRatioDivisor))) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

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

            var unclaimedAmount = simulator.Nexus.RootChain.InvokeContractAtTimestamp(simulator.Nexus.RootStorage, simulator.CurrentTime, Nexus.StakeContractName, "GetUnclaimed", testUser.Address).AsNumber();
            double realUnclaimedAmount = ((double)unclaimedAmount) * Math.Pow(10, -DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(realUnclaimedAmount == realExpectedUnclaimedAmount);

            BigInteger actualEnergyRatio = (BigInteger)(realStakeAmount / realUnclaimedAmount);
            Assert.IsTrue(actualEnergyRatio == DefaultEnergyRatioDivisor);
        }

        private struct FriendTestStruct
        {
            public string name;
            public Address address;
        }

        private byte[] GetScriptForFriends(Address target)
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

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

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

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
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, "INFI", "infinity token", BigInteger.Zero, 8,
            TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible);
            simulator.EndBlock();

            var user = PhantasmaKeys.Generate();

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

