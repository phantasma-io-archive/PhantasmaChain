using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.VM.Utils;
using static System.MidpointRounding;
using static Phantasma.Contracts.Extra.Constants;
using static Phantasma.Domain.DomainSettings;
using static Phantasma.Numerics.UnitConversion;
using static Phantasma.Domain.TokenFlags;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class NachoRewardTests
    {
        int stageCount = 10;
        int milestoneCount = 10;
        decimal[] tokenDecreaseFactor = { 0.95m, 0.9m, 0.85m, 0.8m, 0.75m, 0.7m, 0.65m, 0.6m, 0.55m, 0.5m };

        decimal[] stageTokenAmount =
            {35000000, 17500000, 8750000, 4375000, 2187500, 1093750, 546875, 273437.50m, 136718.75m, 68359.375m};
        decimal[] stageTokensPerUsd = { 100m, 50m, 25m, 12.5m, 6.25m, 3.125m, 1.563m, 0.781m, 0.391m, 0.195m };

        [TestMethod]
        public void TestNachoPurchase()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var tokenSupply = ToBigInteger(69931640.63m, NACHO_TOKEN_DECIMALS);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, NACHO_SYMBOL, NACHO_SYMBOL, DomainSettings.PlatformName, Hash.FromString(NACHO_SYMBOL), tokenSupply, NACHO_TOKEN_DECIMALS, Fungible | Transferable | Finite | Divisible);
            simulator.MintTokens(owner, owner.Address, NACHO_SYMBOL, tokenSupply);
            simulator.EndBlock();

            var buyer = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, FuelTokenSymbol, 100000000);
            simulator.MintTokens(owner, owner.Address, FiatTokenSymbol,
                new BigInteger("1000000000000000000000000000000000000000000000000", 10) * GetUnitValue(FiatTokenDecimals));
            //simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, DomainSettings.FiatTokenSymbol, 1000000 * UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals));
            simulator.EndBlock();

            var ownerFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FiatTokenSymbol, owner.Address);

            decimal requiredMoney = 0;

            for (int stage = 0; stage < stageCount; stage++)
            {
                for (int milestone = 1; milestone <= milestoneCount; milestone++)
                {
                    decimal milestoneTokens = stageTokenAmount[stage] * milestone / 10m;
                    milestoneTokens = Math.Round(milestoneTokens, 2, AwayFromZero);

                    decimal milestoneTokensPerUsd = stageTokensPerUsd[stage] * tokenDecreaseFactor[stage];
                    milestoneTokensPerUsd = Math.Round(milestoneTokensPerUsd, 3, AwayFromZero);

                    requiredMoney += milestoneTokens / milestoneTokensPerUsd;

                    Assert.IsTrue(requiredMoney >= 500, "unexpected order size: not enough for 40% bonus");

                    requiredMoney = requiredMoney / 1.4m;

                    var bigintMoney = ToBigInteger(requiredMoney, FiatTokenDecimals);

                    simulator.BeginBlock();
                    simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, FiatTokenSymbol, bigintMoney);
                    simulator.EndBlock();

                    var initialNachos = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, NACHO_SYMBOL, receiver.Address);
                    var initialFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FiatTokenSymbol, receiver.Address);

                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(buyer, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(buyer.Address, Address.Null, 1, 9999)
                            .CallContract("nacho", "BuyInApp", buyer.Address, FiatTokenSymbol, bigintMoney).
                            SpendGas(buyer.Address).EndScript());
                    simulator.EndBlock();

                    var finalNachos = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, NACHO_SYMBOL, receiver.Address);
                    var finalFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FiatTokenSymbol, receiver.Address);

                    var milestoneTokensBigInt = ToBigInteger(milestoneTokens, NACHO_TOKEN_DECIMALS);

                    Assert.IsTrue(finalNachos == initialNachos + milestoneTokensBigInt);
                    Assert.IsTrue(finalFiat == initialFiat - bigintMoney);
                }
            }
        }

        [TestMethod]
        public void TestNachoPurchaseBonus()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var tokenSupply = ToBigInteger(69931640.63m, NACHO_TOKEN_DECIMALS);

            simulator.BeginBlock();
            simulator.GenerateToken(owner, NACHO_SYMBOL, NACHO_SYMBOL, DomainSettings.PlatformName, Hash.FromString(NACHO_SYMBOL), tokenSupply, NACHO_TOKEN_DECIMALS, Fungible | Transferable | Finite | Divisible);
            simulator.MintTokens(owner, owner.Address, NACHO_SYMBOL, tokenSupply);
            simulator.EndBlock();

            var buyer = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var nexus = simulator.Nexus;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, FuelTokenSymbol, 100000000);
            simulator.MintTokens(owner, owner.Address, FiatTokenSymbol,
                new BigInteger("1000000000000000000000000000000000000000000000000", 10) * GetUnitValue(FiatTokenDecimals));
            //simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, DomainSettings.FiatTokenSymbol, 1000000 * UnitConversion.GetUnitValue(DomainSettings.FiatTokenDecimals));
            simulator.EndBlock();

            var ownerFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FiatTokenSymbol, owner.Address);

            decimal milestoneRequiredMoney = 0;
            decimal[] purchaseAmounts = {1, 2, 5, 10, 20, 50, 100, 250, 500};
            decimal[] purchaseBonus = {0, 5, 10, 15, 20, 25, 30, 35, 40};

            for (int stage = 0; stage < stageCount; stage++)
            {
                for (int milestone = 1; milestone <= milestoneCount; milestone++)
                {
                    decimal milestoneTokens = stageTokenAmount[stage] * milestone / 10m;
                    milestoneTokens = Math.Round(milestoneTokens, 2, AwayFromZero);

                    decimal milestoneTokensPerUsd = stageTokensPerUsd[stage] * tokenDecreaseFactor[stage];
                    milestoneTokensPerUsd = Math.Round(milestoneTokensPerUsd, 3, AwayFromZero);

                    for (int purchase = 0; purchase < purchaseAmounts.Length; purchase++)
                    {
                        var purchaseCoef = purchaseBonus[purchase] / 100;

                        var expectedPurchasedTokens =
                            purchaseAmounts[purchase] * milestoneTokensPerUsd * (1 + purchaseCoef);
                        var expectedPurchasedTokensBigint = ToBigInteger(expectedPurchasedTokens, NACHO_TOKEN_DECIMALS);

                        var purchaseAmountsBigint =
                            ToBigInteger(purchaseAmounts[purchase], FiatTokenDecimals);

                        simulator.BeginBlock();
                        simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, FiatTokenSymbol, purchaseAmountsBigint);
                        simulator.EndBlock();

                        var initialNachos = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, NACHO_SYMBOL, receiver.Address);
                        var initialFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, FiatTokenSymbol, receiver.Address);

                        simulator.BeginBlock();
                        simulator.GenerateCustomTransaction(buyer, ProofOfWork.None, () =>
                            ScriptUtils.BeginScript().AllowGas(buyer.Address, Address.Null, 1, 9999)
                                .CallContract("nacho", "BuyInApp", buyer.Address, FiatTokenSymbol, purchaseAmountsBigint).
                                SpendGas(buyer.Address).EndScript());
                        simulator.EndBlock();

                        var finalNachos = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, NACHO_SYMBOL, receiver.Address);
                        var finalFiat = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FiatTokenSymbol, receiver.Address);

                        Assert.IsTrue(finalNachos == initialNachos + expectedPurchasedTokensBigint);
                        Assert.IsTrue(finalFiat == initialFiat - purchaseAmountsBigint);

                        milestoneTokens -= expectedPurchasedTokens;
                    }

                    //do a single order to clean up the rest of the current milestone
                    var requiredMoney = milestoneTokens / milestoneTokensPerUsd;

                    Assert.IsTrue(requiredMoney >= 500, "unexpected order size: not enough for 40% bonus");

                    requiredMoney = requiredMoney / 1.4m;

                    var bigintMoney = ToBigInteger(requiredMoney, FiatTokenDecimals);

                    simulator.BeginBlock();
                    simulator.GenerateTransfer(owner, buyer.Address, nexus.RootChain, FiatTokenSymbol, bigintMoney);
                    simulator.EndBlock();

                    simulator.BeginBlock();
                    simulator.GenerateCustomTransaction(buyer, ProofOfWork.None, () =>
                        ScriptUtils.BeginScript().AllowGas(buyer.Address, Address.Null, 1, 9999)
                            .CallContract("nacho", "BuyInApp", buyer.Address, FiatTokenSymbol, bigintMoney).
                            SpendGas(buyer.Address).EndScript());
                    simulator.EndBlock();
                }

                
            }
        }
    }
}
