using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Pay.Chains;
using Phantasma.Simulator;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.VM;
using Phantasma.VM.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Phantasma.Tests
{
    [TestClass]
    public class SwapTests
    {
        private void CreatePools(NexusSimulator simulator, PhantasmaKeys testUser)
        {
            // Setup Tokens
            string poolSymbol = "SOUL";
            BigInteger poolSymbolAmount = 1000;
            string poolPair = "KCAL";
            BigInteger poolPairAmount = 16000;
            string virtualPoolPair = "COOL";
            BigInteger virtualPoolPairAmount = 10000000;

            string LPTokenSymbol = "LP";

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "CreatePool", testUser.Address, poolSymbol, poolSymbolAmount, poolPair, poolPairAmount).
                    SpendGas(testUser.Address).EndScript());
            var block = simulator.EndBlock().First();

            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
            var saleHash = resultObj.AsInterop<Hash>();
        }

        [TestMethod]
        public void CreatePool()
        {
            // Setup Simulation chain
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // Setup Tokens
            string poolSymbol = "SOUL";
            BigInteger poolSymbolAmount = 1000;
            string poolPair = "KCAL";
            BigInteger poolPairAmount = 16000;
            string virtualPoolPair = "COOL";
            BigInteger virtualPoolPairAmount = 10000000;

            string LPTokenSymbol = "LP";

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolPair, "CoolToken", virtualPoolPairAmount, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.GenerateToken(owner, LPTokenSymbol, "LP", 0, 0, TokenFlags.Burnable | TokenFlags.Transferable  );
            simulator.MintTokens(owner, testUserA.Address, poolSymbol, 10000);
            simulator.MintTokens(owner, testUserA.Address, poolPair, 100000);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolPair, virtualPoolPairAmount);
            simulator.EndBlock();

            // TODO
            //var script = new ScriptBuilder().CallContract("swap", "CreatePool", testUserA.Address, poolSymbol, poolSymbolAmount, poolPair, poolPairAmount).EndScript();

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "CreatePool", testUserA.Address, poolSymbol, poolSymbolAmount, poolPair, poolPairAmount).
                    SpendGas(testUserA.Address).EndScript());
            var block = simulator.EndBlock().First();

            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
            var saleHash = resultObj.AsInterop<Hash>();

            Console.WriteLine($"saleHash:{saleHash}");            

            //var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            //var temp = result.ToObject();
            //var rates = (SwapPair[])temp;
            //
            //decimal targetRate = 0;
            //
            //foreach (var entry in rates)
            //{
            //    if (entry.Symbol == DomainSettings.FuelTokenSymbol)
            //    {
            //        targetRate = UnitConversion.ToDecimal(entry.Value, DomainSettings.FuelTokenDecimals);
            //        break;
            //    }
            //}

            //Assert.IsTrue(targetRate == 5m);

        }

        [TestMethod]
        public void AddLiquidityToThePool()
        {
            // Setup Simulation Blockchain
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // Setup Tokens
            string poolSymbol = "SOUL";
            BigInteger poolSymbolAmount = 1000;
            string poolPair = "KCAL";
            BigInteger poolPairAmount = 16000;
            string virtualPoolPair = "COOL";
            BigInteger virtualPoolPairAmount = 10000000;

            string LPTokenSymbol = "LP";

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            // setup Tokens for the user
            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolPair, "CoolToken", virtualPoolPairAmount, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol, 10000);
            simulator.MintTokens(owner, testUserA.Address, poolPair, 100000);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolPair, virtualPoolPairAmount);
            simulator.EndBlock();

            // Create Pools
            CreatePools(simulator, testUserA);

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol, poolSymbolAmount, poolPair, poolPairAmount)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
            var addLiquidityHash = resultObj.AsInterop<Hash>();

            Console.WriteLine($"addLiquidityHash:{addLiquidityHash}");

        }

        [TestMethod]
        public void RemoveLiquidityToThePool()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // Setup Tokens
            string poolSymbol = "SOUL";
            BigInteger poolSymbolAmount = 1000;
            string poolPair = "KCAL";
            BigInteger poolPairAmount = 16000;
            string virtualPoolPair = "COOL";
            BigInteger virtualPoolPairAmount = 10000000;

            string LPTokenSymbol = "LP";

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            // setup Tokens for the user
            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolPair, "CoolToken", virtualPoolPairAmount, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.GenerateToken(owner, LPTokenSymbol, "LP", 1000, 0, TokenFlags.Burnable | TokenFlags.Transferable);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol, 10000);
            simulator.MintTokens(owner, testUserA.Address, poolPair, 100000);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolPair, virtualPoolPairAmount);
            simulator.EndBlock();

            // Create Pools
            CreatePools(simulator, testUserA);

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "RemoveLiquidity", testUserA.Address, poolSymbol, poolSymbolAmount, poolPair, poolPairAmount)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            var resultBytes = block.GetResultForTransaction(tx.Hash);
            var resultObj = Serialization.Unserialize<VMObject>(resultBytes);
            var removeLiquidityHash = resultObj.AsInterop<Hash>();

            Console.WriteLine($"removeLiquidityHash:{removeLiquidityHash}");
        }

        [TestMethod]
        public void GetRatesForSwap()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var script = new ScriptBuilder().CallContract("swap", "GetRates", "SOUL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rates = (SwapPair[])temp;

            decimal targetRate = 0;

            foreach (var entry in rates)
            {
                if (entry.Symbol == DomainSettings.FuelTokenSymbol)
                {
                    targetRate = UnitConversion.ToDecimal(entry.Value, DomainSettings.FuelTokenDecimals);
                    break;
                }
            }

            Assert.IsTrue(targetRate == 5m);
        }

        [TestMethod]
        public void GetRateForSwap()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var script = new ScriptBuilder().CallContract("swap", "GetRate", "SOUL", "KCAL", UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rate = (BigInteger)temp;

            decimal targetRate = 0;

            targetRate = UnitConversion.ToDecimal(rate, DomainSettings.FuelTokenDecimals);
            Console.WriteLine($"Rate:{rate}");

            Assert.IsTrue(targetRate == 5m);
        }

        [TestMethod]
        public void SwapTokens()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // TODO
        }

        [TestMethod]
        public void SwapTokensReverse()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // TODO
        }


        [TestMethod]
        public void CosmicSwap()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUserA = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            var symbol = "COOL";

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", 1000000, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, symbol, 100000);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            var blockA = simulator.EndBlock().FirstOrDefault();

            Assert.IsTrue(blockA != null);
            Assert.IsFalse(blockA.OracleData.Any());

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var originalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);

            var swapAmount = UnitConversion.ToBigInteger(0.01m, DomainSettings.StakingTokenDecimals);
            simulator.BeginBlock();
            simulator.GenerateSwap(testUserA, nexus.RootChain, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount);
            var blockB = simulator.EndBlock().FirstOrDefault();

            Assert.IsTrue(blockB != null);
            Assert.IsTrue(blockB.OracleData.Any());

            var bytes = blockB.ToByteArray(true);
            var otherBlock = Block.Unserialize(bytes);
            Assert.IsTrue(otherBlock.Hash == blockB.Hash);

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUserA.Address);
            Assert.IsTrue(finalBalance > originalBalance);

            /*
            swapAmount = 10;
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUserA, ProofOfWork.None, () =>
            {
               return ScriptUtils.BeginScript().
                    AllowGas(testUserA.Address, Address.Null, 400, 9999).
                    //CallContract("swap", "SwapFiat", testUserA.Address, symbol, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FiatTokenDecimals)).
                    CallContract("swap", "SwapTokens", testUserA.Address, symbol, DomainSettings.FuelTokenSymbol, new BigInteger(1)).
                    SpendGas(testUserA.Address).
                    EndScript();
            });
            simulator.EndBlock();*/
        }

        [TestMethod]
        public void ChainSwapIn()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var neoKeys = Neo.Core.NeoKeys.Generate();

            var limit = 800;

            // 1 - at this point a real NEO transaction would be done to the NEO address obtained from getPlatforms in the API
            // here we just use a random hardcoded hash and a fake oracle to simulate it
            var swapSymbol = "GAS";
            var neoTxHash = OracleSimulator.SimulateExternalTransaction("neo", Pay.Chains.NeoWallet.NeoID, neoKeys.PublicKey, neoKeys.Address, swapSymbol, 2);

            var tokenInfo = nexus.GetTokenInfo(nexus.RootStorage, swapSymbol);

            // 2 - transcode the neo address and settle the Neo transaction on Phantasma
            var transcodedAddress = Address.FromKey(neoKeys);

            var testUser = PhantasmaKeys.Generate();

            var platformName = Pay.Chains.NeoWallet.NeoPlatform;
            var platformChain = Pay.Chains.NeoWallet.NeoPlatform;

            var gasPrice = simulator.MinimumFee;

            Func<decimal, byte[]> genScript = (fee) =>
            {
                return new ScriptBuilder()
                .CallContract("interop", "SettleTransaction", transcodedAddress, platformName, platformChain, neoTxHash)
                .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(fee, DomainSettings.FuelTokenDecimals))
                .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
                .AllowGas(transcodedAddress, Address.Null, gasPrice, limit)
                .TransferBalance(DomainSettings.FuelTokenSymbol, transcodedAddress, testUser.Address)
                .SpendGas(transcodedAddress).EndScript();
            };

            // note the 0.1m passed here could be anything else. It's just used to calculate the actual fee
            var vm = new GasMachine(genScript(0.1m), 0, null);
            var result = vm.Execute();
            var usedGas = UnitConversion.ToDecimal((int)(vm.UsedGas * gasPrice), DomainSettings.FuelTokenDecimals);

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(neoKeys, ProofOfWork.None, () =>
            {
                return genScript(usedGas);
            });

            simulator.EndBlock();

            var swapToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, swapSymbol);
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, transcodedAddress);
            Assert.IsTrue(balance == 0);

            balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapToken, testUser.Address);
            Assert.IsTrue(balance > 0);

            var settleHash = (Hash)nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), "neo", neoTxHash).ToObject();
            Assert.IsTrue(settleHash == tx.Hash);

            var fuelToken = nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol);
            var leftoverBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, transcodedAddress);
            //Assert.IsTrue(leftoverBalance == 0);
        }

        [TestMethod]
        public void ChainSwapOut()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var rootChain = nexus.RootChain;

            var testUser = PhantasmaKeys.Generate();

            var potAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);

            // 0 - just send some assets to the 
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
            simulator.MintTokens(owner, potAddress, "GAS", UnitConversion.ToBigInteger(1, 8));
            simulator.EndBlock();

            var oldBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
            var oldSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

            // 1 - transfer to an external interop address
            var targetAddress = NeoWallet.EncodeAddress("AG2vKfVpTozPz2MXvye4uDCtYcTnYhGM8F");
            simulator.BeginBlock();
            simulator.GenerateTransfer(testUser, targetAddress, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.EndBlock();

            var currentBalance = rootChain.GetTokenBalance(rootChain.Storage, DomainSettings.StakingTokenSymbol, testUser.Address);
            var currentSupply = rootChain.GetTokenSupply(rootChain.Storage, DomainSettings.StakingTokenSymbol);

            Assert.IsTrue(currentBalance < oldBalance);
            Assert.IsTrue(currentBalance == 0);

            Assert.IsTrue(currentSupply < oldSupply);
        }

        [TestMethod]
        public void QuoteConversions()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            Assert.IsTrue(nexus.PlatformExists(nexus.RootStorage, "neo"));
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, "NEO"));

            var context = new StorageChangeSetContext(nexus.RootStorage);
            var runtime = new RuntimeVM(-1, new byte[0], 0, nexus.RootChain, Address.Null, Timestamp.Now, null, context, new OracleSimulator(nexus), ChainTask.Null, true);

            var temp = runtime.GetTokenQuote("NEO", "KCAL", 1);
            var price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(price == 100);

            temp = runtime.GetTokenQuote("KCAL", "NEO", UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals));
            price = UnitConversion.ToDecimal(temp, 0);
            Assert.IsTrue(price == 1);

            temp = runtime.GetTokenQuote("SOUL", "KCAL", UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            price = UnitConversion.ToDecimal(temp, DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(price == 5);
        }

        
    }
}
