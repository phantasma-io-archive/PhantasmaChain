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
        PhantasmaKeys owner;
        Nexus nexus;
        NexusSimulator simulator;

        // Token Values
        string poolSymbol0 = "SOUL";
        BigInteger poolAmount0 = UnitConversion.ToBigInteger(50000, 8);
        string poolSymbol1 = "KCAL";
        BigInteger poolAmount1 = UnitConversion.ToBigInteger(160000, 10);
        string poolSymbol2 = "ETH";
        BigInteger poolAmount2 = UnitConversion.ToBigInteger(50, 18);
        string poolSymbol3 = "BNB";
        BigInteger poolAmount3 = UnitConversion.ToBigInteger(100, 18);
        string poolSymbol4 = "NEO";
        BigInteger poolAmount4 = UnitConversion.ToBigInteger(500, 0);
        string poolSymbol5 = "GAS";
        BigInteger poolAmount5 = UnitConversion.ToBigInteger(600, 8);

        // Virtual Token
        string virtualPoolSymbol = "COOL";
        BigInteger virtualPoolAmount1 = UnitConversion.ToBigInteger(10000000, 10);

        public void Init()
        {
            owner = PhantasmaKeys.Generate();
            nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulator = new NexusSimulator(nexus, owner, 1234);
            var SwapAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);


            simulator.BeginBlock();
            simulator.MintTokens(owner, owner.Address, poolSymbol0, poolAmount0 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol1, poolAmount1 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol2, poolAmount2 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol4, poolAmount4 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol5, poolAmount5 * 100);
            simulator.MintTokens(owner, SwapAddress, poolSymbol0, poolAmount0);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol1, poolAmount1 * 2);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol2, poolAmount2);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol4, poolAmount4);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol5, poolAmount5);
            simulator.EndBlock();

            // Migrate Call to setup the Pools to V3
            MigrateCall();

            // Create a Pool
            //CreatePools();
        }

        private void MigrateCall()
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "MigrateToV3")
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);

        }

        private void SetupNormalPool()
        {
            // SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);

            // SOUL / ETH
           simulator.BeginBlock();
           tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
               ScriptUtils
               .BeginScript()
               .AllowGas(owner.Address, Address.Null, 1, 9999)
               .CallContract("swap", "CreatePool", owner.Address, poolSymbol0, poolAmount0, poolSymbol2, poolAmount2)
               .SpendGas(owner.Address)
               .EndScript());
           block = simulator.EndBlock().First();
           resultBytes = block.GetResultForTransaction(tx.Hash);

            // SOUL / NEO
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol0, poolAmount0, poolSymbol4, poolAmount4)
                .SpendGas(owner.Address)
                .EndScript());
            block = simulator.EndBlock().First();
            resultBytes = block.GetResultForTransaction(tx.Hash);

            // SOUL / GAS
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol0, poolAmount0, poolSymbol5, poolAmount5)
                .SpendGas(owner.Address)
                .EndScript());
            block = simulator.EndBlock().First();
            resultBytes = block.GetResultForTransaction(tx.Hash);
        }

        private void SetupVirtualPool()
        {
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1)
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);
        }

        private void CreatePools()
        {
            SetupNormalPool();
        }

        [TestMethod]
        public void MigrateTest()
        {
            owner = PhantasmaKeys.Generate();
            nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulator = new NexusSimulator(nexus, owner, 1234);

            var SwapAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);

            simulator.BeginBlock();
            simulator.MintTokens(owner, owner.Address, poolSymbol0, poolAmount0 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol1, poolAmount1 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol2, poolAmount2 * 100);
            simulator.MintTokens(owner, owner.Address, poolSymbol4, poolAmount4 * 20);
            simulator.MintTokens(owner, owner.Address, poolSymbol5, poolAmount5 * 100);
            simulator.MintTokens(owner, SwapAddress, poolSymbol0, poolAmount0 );
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol1, poolAmount1 * 5);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol2, poolAmount2);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol4, poolAmount4 * 3);
            simulator.GenerateTransfer(owner, SwapAddress, nexus.RootChain, poolSymbol5, poolAmount5 * 5);
            //simulator.MintTokens(owner, SwapAddress, poolSymbol4, poolAmount4);
            //simulator.MintTokens(owner, SwapAddress, poolSymbol5, poolAmount5);
            simulator.EndBlock();

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "MigrateToV3")
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();
            var resultBytes = block.GetResultForTransaction(tx.Hash);

            // Check pools

        }


        [TestMethod]
        public void CreatePool()
        {
            Init();

            string symbol0 = "SOUL";
            string symbol1 = "COOL";
            string symbol2 = "BADD";
            BigInteger myPoolAmount0 = UnitConversion.ToBigInteger(10000, 8);
            BigInteger myPoolAmount1 = UnitConversion.ToBigInteger(100000, 8);

            double am0 = (double)myPoolAmount0;
            double am1 = (double)myPoolAmount1;
            BigInteger totalLiquidity = (BigInteger)Math.Sqrt(am0 * am0 / 3);

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();
            byte[] tokenScript = null;

            simulator.BeginBlock();
            var communitySupply = 100000;
            simulator.GenerateToken(owner, symbol2, "Mankini Token", UnitConversion.ToBigInteger(communitySupply, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite);
            simulator.MintTokens(owner, owner.Address, symbol2, communitySupply);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol1, "Cool Token", myPoolAmount1 * 10, 8,  TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible, tokenScript);
            simulator.MintTokens(owner, owner.Address, poolSymbol1, poolAmount1 * 10);
            simulator.MintTokens(owner, owner.Address, symbol0, myPoolAmount0 * 10);
            simulator.MintTokens(owner, owner.Address, symbol1, myPoolAmount1 * 10);
            simulator.EndBlock();

            // Get Tokens Info
            //token0
            var token0 = nexus.GetTokenInfo(nexus.RootStorage, symbol0);
            var token0Address = nexus.GetTokenContract(nexus.RootStorage, symbol0);
            Assert.IsTrue(token0.Symbol == symbol0);

            // token1
            var token1 = nexus.GetTokenInfo(nexus.RootStorage, symbol1);
            var token1Address = nexus.GetTokenContract(nexus.RootStorage, symbol1);
            Assert.IsTrue(token1.Symbol == symbol1);
            Assert.IsTrue(token1.Flags.HasFlag(TokenFlags.Transferable), "Not swappable.");

            // Create a Pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, symbol0, myPoolAmount0, symbol1, 0)
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();

            // Check if the pool was created
            var script = new ScriptBuilder().CallContract("swap", "GetPool", symbol0, symbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();

            Assert.IsTrue(pool.Symbol0 == symbol0, "Symbol0 doesn't check");
            Assert.IsTrue(pool.Amount0 == myPoolAmount0, $"Amount0 doesn't check {pool.Amount0}");
            Assert.IsTrue(pool.Symbol1 == symbol1, "Symbol1 doesn't check");
            Assert.IsTrue(pool.Amount1 == myPoolAmount0 / 3, $"Amount1 doesn't check {pool.Amount1}");
            Assert.IsTrue(pool.TotalLiquidity == totalLiquidity, "Liquidity doesn't check"); 
            Assert.IsTrue(pool.Symbol0Address == token0Address.Address.Text);
            Assert.IsTrue(pool.Symbol1Address == token1Address.Address.Text);

            Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
        }

        [TestMethod]
        [Ignore]
        public void CreateVirtualPool()
        {
            Init();

            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount1 * virtualPoolAmount1));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
            simulator.EndBlock();

            // Get Tokens Info
            //token1
            var token1 = nexus.GetTokenInfo(nexus.RootStorage, poolSymbol1);
            var token1Address = nexus.GetTokenContract(nexus.RootStorage, poolSymbol1);
            Assert.IsTrue(token1.Symbol == poolSymbol1, "Symbol1 != Token1");

            // virtual Token
            var virtualToken = nexus.GetTokenInfo(nexus.RootStorage, virtualPoolSymbol);
            var virtualTokenAddress = nexus.GetTokenContract(nexus.RootStorage, virtualPoolSymbol);
            Assert.IsTrue(virtualToken.Symbol == virtualPoolSymbol, $"VirtualSymbol != VirtualToken({virtualToken})");
            
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1)
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();

            // Check if the pool was created
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol1, virtualPoolSymbol).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();

            Assert.IsTrue(pool.Symbol0 == poolSymbol1);
            Assert.IsTrue(pool.Amount0 == poolAmount1);
            Assert.IsTrue(pool.Symbol1 == virtualPoolSymbol); 
            Assert.IsTrue(pool.Amount1 == virtualPoolAmount1);
            Assert.IsTrue(pool.TotalLiquidity == totalLiquidity);
            Assert.IsTrue(pool.Symbol0Address == token1Address.Address.Text);
            Assert.IsTrue(pool.Symbol1Address == virtualTokenAddress.Address.Text);

            Console.WriteLine($"Check Values | {pool.Symbol0}({pool.Symbol0Address}) -> {pool.Amount0} | {pool.Symbol1}({pool.Symbol1Address}) -> {pool.Amount1} || {pool.TotalLiquidity}");
        }

        [TestMethod]
        // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
        public void AddLiquidityToPool()
        {
            Init();

            // Get Initial Pool State the Liquidity
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol2).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            var amount0 = poolAmount0 / 10;
            var amount1 = poolAmount2 / 10;
            var poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount1, 18, DomainSettings.FiatTokenDecimals);
            var amountCalculated = UnitConversion.ConvertDecimals((amount0 / poolRatio), DomainSettings.FiatTokenDecimals, 18);

            // setup Tokens for the user
            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, amount0, poolSymbol2, 0)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Check the Liquidity
            var scriptAfter = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol2).EndScript();
            var resultAfter = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptAfter);
            var poolAfter = resultAfter.AsStruct<Pool>();
            pool.TotalLiquidity += (amount0 * pool.TotalLiquidity) / (pool.Amount0);

            Assert.IsTrue(poolAfter.Symbol0 == poolSymbol0, $"Symbol is incorrect: {poolSymbol0}");
            Assert.IsTrue(poolAfter.Amount0 == pool.Amount0 + amount0, $"Symbol Amount0 is incorrect: {pool.Amount0 + amount0} != {poolAfter.Amount0} {poolSymbol0}");
            Assert.IsTrue(poolAfter.Symbol1 == poolSymbol2, $"Pair is incorrect: {poolSymbol2}");
            Assert.IsTrue(poolAfter.Amount1 == pool.Amount1 + amountCalculated, $"Symbol Amount1 is incorrect: {pool.Amount1 + amountCalculated} != {poolAfter.Amount1} {poolSymbol2}");
            Assert.IsTrue(pool.TotalLiquidity == poolAfter.TotalLiquidity, $"TotalLiquidity doesn't checkout {pool.TotalLiquidity}!={poolAfter.TotalLiquidity}");
        }

        [TestMethod]
        [Ignore]
        public void AddLiquidityToVirtualPool()
        {
            Init();

            SetupVirtualPool();

            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount1 * virtualPoolAmount1));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol1, poolAmount1 / 2, virtualPoolSymbol, virtualPoolAmount1 / 2)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Check the Liquidity
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();
            totalLiquidity += (poolAmount1 * pool.TotalLiquidity) / (poolAmount1 + (poolAmount1 / 2));

            Assert.IsTrue(pool.Symbol0 == poolSymbol0, "Symbol is incorrect");
            Assert.IsTrue(pool.Amount0 == poolAmount1 + (poolAmount1 / 2), "Symbol Amount0 is incorrect");
            Assert.IsTrue(pool.Symbol1 == poolSymbol1, "Pair is incorrect");
            Assert.IsTrue(pool.Amount1 == virtualPoolAmount1 + (virtualPoolAmount1 / 2), "Symbol Amount1 is incorrect");
            Assert.IsTrue(pool.TotalLiquidity == totalLiquidity);
        }

        [TestMethod]
        // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
        public void RemoveLiquidityToPool()
        {
            Init();

            // Get Initial Pool State the Liquidity
            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals);
            var amount0 = poolAmount0 / 2;
            var amount1 = UnitConversion.ConvertDecimals((amount0 * 100 / poolRatio ), DomainSettings.FiatTokenDecimals, 10);
            Console.WriteLine($"ratio:{poolRatio} | amount0:{amount0} | amount1:{amount1}");
            Console.WriteLine($"BeforeTouchingPool: {pool.Amount0} {pool.Symbol0} | {pool.Amount1} {pool.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");

            BigInteger totalAm0 = pool.Amount0;
            BigInteger totalAm1 = pool.Amount1;
            //poolRatio = UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals) / UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals);
            BigInteger totalLiquidity = pool.TotalLiquidity;

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();
            
            // setup Tokens for the user
            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();
            var lpAdded = (poolAmount0 * totalLiquidity) / totalAm0;
            totalLiquidity += lpAdded;
            totalAm0 += poolAmount0;

            var scriptPoolBefore = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPoolBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPoolBefore);
            var poolBefore = resultPoolBefore.AsStruct<Pool>();
            Console.WriteLine($"AfterAdd: {poolBefore.Amount0} {poolBefore.Symbol0} | {poolBefore.Amount1} {poolBefore.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolBefore.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolBefore.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");

            var scriptBefore = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptBefore);
            var nftRAMBefore = resultBefore.AsStruct<LPTokenContentRAM>();

            // Remove Liquidity
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "RemoveLiquidity", testUserA.Address.Text, poolSymbol0, amount0, poolSymbol1, 0)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get Pool
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var poolAfter = result.AsStruct<Pool>();

            Console.WriteLine($"AfterRemove: {poolAfter.Amount0} {poolAfter.Symbol0} | {poolAfter.Amount1} {poolAfter.Symbol1} | PoolRatio:{UnitConversion.ConvertDecimals(poolAfter.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(poolAfter.Amount1, 10, DomainSettings.FiatTokenDecimals)}\n");
            BigInteger newLP = ((nftRAMBefore.Amount0 - amount0) * (totalLiquidity - nftRAMBefore.Liquidity)) / (totalAm0 - nftRAMBefore.Amount0);
            var lpRemoved = ((nftRAMBefore.Amount0 - amount0) * (totalLiquidity- nftRAMBefore.Liquidity)) / (totalAm0- nftRAMBefore.Amount0);
            totalLiquidity = totalLiquidity - nftRAMBefore.Liquidity + newLP;
            totalAm0 -= (amount0);

            // Get My NFT DATA 
            var scriptAfter = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultAfter = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptAfter);
            var nftRAMAfter = resultAfter.AsStruct<LPTokenContentRAM>();

            Console.WriteLine($"TEST: BeforeLP:{nftRAMBefore.Liquidity}  | AfterLP:{nftRAMAfter.Liquidity} | LPRemoved:{lpRemoved}");

            // Validation
            Assert.IsFalse(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
            Assert.IsFalse(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
            Assert.IsFalse(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, $"Liquidity does not differ. | {nftRAMBefore.Liquidity} == {nftRAMAfter.Liquidity}");

            Assert.IsTrue(nftRAMBefore.Amount0 - amount0 == nftRAMAfter.Amount0, "Amount0 not true.");
            Assert.IsTrue(nftRAMBefore.Amount1 - amount1 == nftRAMAfter.Amount1, $"Amount1 not true. {nftRAMBefore.Amount1 - amount1} != {nftRAMAfter.Amount1}");
            Assert.IsTrue(newLP == nftRAMAfter.Liquidity, $"Liquidity does differ. | {nftRAMBefore.Liquidity - lpRemoved} == {nftRAMAfter.Liquidity}");

            // Get Amount by Liquidity
            // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
            // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
            //(amount0 * (pool.TotalLiquidity - nftRAM.Liquidity)) / (pool.Amount0 - nftRAM.Amount0);
            var _amount0 = (nftRAMAfter.Liquidity) * pool.Amount0 / pool.TotalLiquidity;
            var _amount1 = (nftRAMAfter.Liquidity) * pool.Amount1 / pool.TotalLiquidity;
            var _pool_amount0 = (poolBefore.Amount0 - nftRAMBefore.Amount0) + amount0;
            var _pool_amount1 = (poolBefore.Amount1 - nftRAMBefore.Amount1) + amount1;
            var _pool_liquidity = totalLiquidity;
             
            Console.WriteLine($"user Initial = am0:{nftRAMBefore.Amount0} | am1:{nftRAMBefore.Amount1} | lp:{nftRAMBefore.Liquidity}");
            Console.WriteLine($"pool Initial = am0:{poolBefore.Amount0} | am1:{poolBefore.Amount1} | lp:{poolBefore.TotalLiquidity}");
            Console.WriteLine($"user after = am0:{nftRAMAfter.Amount0} | am1:{nftRAMAfter.Amount1} | lp:{nftRAMAfter.Liquidity}");
            Console.WriteLine($"pool after = am0:{poolAfter.Amount0} | am1:{poolAfter.Amount1} | lp:{poolAfter.TotalLiquidity}");
            Console.WriteLine($"am0 = {_amount0} == {nftRAMAfter.Amount0} || am1 = {_amount1} == {nftRAMAfter.Amount1}");
            Assert.IsTrue(_pool_amount0 == poolAfter.Amount0, $"Pool Amount0 not calculated properly | {_pool_amount0} != {poolAfter.Amount0}");
            Assert.IsTrue(_pool_amount1 == poolAfter.Amount1, $"Pool Amount1 not calculated properly | {_pool_amount1} != {poolAfter.Amount1}");
            Assert.IsTrue(_pool_liquidity == poolAfter.TotalLiquidity, $"Pool TotalLiquidity not calculated properly | {_pool_liquidity} != {poolAfter.TotalLiquidity}");
            Assert.IsTrue(_amount0 + UnitConversion.ToBigInteger(0.00000001m, 8) >= nftRAMAfter.Amount0, $"Amount0 not calculated properly | {_amount0+ UnitConversion.ToBigInteger(0.00000001m, 8) } != {nftRAMAfter.Amount0}");
            Assert.IsTrue(_amount1 + UnitConversion.ToBigInteger(0.00000001m, 10) >= nftRAMAfter.Amount1, $"Amount1 not calculated properly | {_amount1+ UnitConversion.ToBigInteger(0.00000001m, 10)} != {nftRAMAfter.Amount1}");

            // Get Liquidity by amount
            var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0;
            var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / poolAfter.Amount1;

            Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity} | ratio:{nftRAMAfter.Amount0*100 / nftRAMAfter.Amount1}");
            Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {totalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * totalLiquidity / poolAfter.Amount0}");
            Console.WriteLine($"LiquidityAm0 = {nftRAMAfter.Amount0} * {poolAfter.TotalLiquidity} / {poolAfter.Amount1} = {nftRAMAfter.Amount0 * poolAfter.TotalLiquidity / poolAfter.Amount0}");

            Assert.IsTrue(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
            Assert.IsTrue(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
            Assert.IsTrue(totalLiquidity == poolAfter.TotalLiquidity, $"Liquidity not true.");
        }

        [TestMethod]
        public void RemoveLiquiditySmall()
        {
            Init();

            // Get Initial Pool State the Liquidity
            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            BigInteger poolRatio = UnitConversion.ConvertDecimals(pool.Amount0, 8, DomainSettings.FiatTokenDecimals) * 100 / UnitConversion.ConvertDecimals(pool.Amount1, 10, DomainSettings.FiatTokenDecimals);
            var amount0 = poolAmount0 / 2;
            var amount1 = UnitConversion.ConvertDecimals((amount0 * 100 / poolRatio), DomainSettings.FiatTokenDecimals, 10);

            BigInteger totalAm0 = pool.Amount0;
            BigInteger totalAm1 = pool.Amount1;
            BigInteger totalLiquidity = pool.TotalLiquidity;

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            // setup Tokens for the user
            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, amount0, poolSymbol1, 0)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();
            var lpAdded = (amount0 * totalLiquidity) / totalAm0;
            totalLiquidity += lpAdded;
            totalAm0 += amount0;

            var scriptPoolBefore = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPoolBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPoolBefore);
            var poolBefore = resultPoolBefore.AsStruct<Pool>();

            var scriptBefore = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptBefore);
            var nftRAMBefore = resultBefore.AsStruct<LPTokenContentRAM>();

            var _amount0 = (nftRAMBefore.Liquidity) * poolBefore.Amount0 / poolBefore.TotalLiquidity;
            var _amount1 = (nftRAMBefore.Liquidity) * poolBefore.Amount1 / poolBefore.TotalLiquidity;

            Assert.IsTrue(_amount0 + UnitConversion.ToBigInteger(0.00000001m, 8)  >= nftRAMBefore.Amount0, $"Amount0 not calculated properly | {_amount0} != {nftRAMBefore.Amount0}");
            Assert.IsTrue(_amount1 + UnitConversion.ToBigInteger(0.00000001m, 10) >= nftRAMBefore.Amount1, $"Amount1 not calculated properly | {_amount1} != {nftRAMBefore.Amount1}");
        }

        [TestMethod]
        [Ignore]
        public void RemoveLiquidityToVirtualPool()
        {
            Init();

            SetupVirtualPool();

            BigInteger totalAm0 = poolAmount1;
            BigInteger totalAm1 = virtualPoolAmount1;
            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(totalAm0 * totalAm1));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateToken(owner, virtualPoolSymbol, virtualPoolSymbol, virtualPoolAmount1 * 100, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, virtualPoolSymbol, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol1, poolAmount1, virtualPoolSymbol, virtualPoolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();
            var lpAdded = (poolAmount1 * totalLiquidity) / totalAm0;
            totalLiquidity += lpAdded;
            totalAm0 += poolAmount1;

            var scriptBefore = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol1, virtualPoolSymbol).EndScript();
            var resultBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptBefore);
            var nftRAMBefore = resultBefore.AsStruct<LPTokenContentRAM>();

            // Remove Liquidity
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "RemoveLiquidity", testUserA.Address.Text, poolSymbol1, poolAmount1 / 2, virtualPoolSymbol, virtualPoolAmount1 / 2)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get Pool
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol1, virtualPoolSymbol).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();
            var lpRemoved = ((poolAmount1 / 2) * totalLiquidity) / totalAm0;
            totalLiquidity -= lpRemoved;
            totalAm0 -= (poolAmount1 / 2);

            // Get My NFT DATA 
            var scriptAfter = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol1, virtualPoolSymbol).EndScript();
            var resultAfter = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptAfter);
            var nftRAMAfter = resultAfter.AsStruct<LPTokenContentRAM>();

            // Validation
            Assert.IsFalse(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
            Assert.IsFalse(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
            Assert.IsFalse(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, "Liquidity does not differ.");

            Assert.IsTrue(nftRAMBefore.Amount0 - (poolAmount1 / 2) == nftRAMAfter.Amount0, "Amount0 not true.");
            Assert.IsTrue(nftRAMBefore.Amount1 - (virtualPoolAmount1 / 2) == nftRAMAfter.Amount1, "Amount1 not true.");

            // Get Amount by Liquidity
            // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
            // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
            var amount0 = nftRAMAfter.Liquidity * pool.Amount0 / pool.TotalLiquidity;
            var amount1 = nftRAMAfter.Liquidity * pool.Amount1 / pool.TotalLiquidity;

            Console.WriteLine($"am0 = {amount0} == {nftRAMAfter.Amount0} || am1 = {amount1} == {nftRAMAfter.Amount1}");
            Assert.IsTrue(amount0 == nftRAMAfter.Amount0, "Amount0 not calculated properly");
            Assert.IsTrue(amount1 == nftRAMAfter.Amount1, "Amount1 not calculated properly");

            // Get Liquidity by amount
            var liquidityAm0 = nftRAMAfter.Amount0 * totalLiquidity / pool.Amount0;
            var liquidityAm1 = nftRAMAfter.Amount1 * totalLiquidity / pool.Amount1;

            Console.WriteLine($"LiquidityAm0 = {liquidityAm0} == {nftRAMAfter.Liquidity} || LiquidityAm1 = {liquidityAm1} == {nftRAMAfter.Liquidity}");

            Assert.IsTrue(liquidityAm0 == nftRAMAfter.Liquidity, "Liquidity Amount0 -> not calculated properly");
            Assert.IsTrue(liquidityAm1 == nftRAMAfter.Liquidity, "Liquidity Amount1 -> not calculated properly");
            Assert.IsTrue(totalLiquidity == pool.TotalLiquidity, "Liquidity not true.");

        }

        [TestMethod]
        // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
        public void GetRatesForSwap()
        {
            Init();

            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            BigInteger amount = UnitConversion.ToBigInteger(5, 8);
            BigInteger targetRate = (pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount));

            var script = new ScriptBuilder().CallContract("swap", "GetRates", poolSymbol0, amount).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rates = (SwapPair[])temp;

            decimal rate = 0;

            foreach (var entry in rates)
            {
                if (entry.Symbol == DomainSettings.FuelTokenSymbol)
                {
                    rate = UnitConversion.ToDecimal(entry.Value, DomainSettings.FuelTokenDecimals);
                    break;
                }
            }

            Assert.IsTrue(rate == UnitConversion.ToDecimal(targetRate, DomainSettings.FuelTokenDecimals), $"{rate} != {targetRate}");
        }

        [TestMethod]
        // TODO: Get the pool initial values and calculate the target rate with those values insted of the static ones.
        public void GetRateForSwap()
        {
            Init();

            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            BigInteger amount = UnitConversion.ToBigInteger(5, 8);
            BigInteger targetRate = pool.Amount1 * (1 - 3 / 100) * amount / (pool.Amount0 + (1 - 3 / 100) * amount);

            var script = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, amount).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rate = (BigInteger)temp;

            Assert.IsTrue(targetRate == rate);
        }

        [TestMethod]
        public void SwapTokens()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            BigInteger swapValue = UnitConversion.ToBigInteger(100, 8);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
            simulator.EndBlock();
            
            var beforeTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);

            // Add Liquidity to the pool SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // SOUL / ETH

            // Get Rate
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol2, swapValue).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{swapValue} {poolSymbol0} for {rate} {poolSymbol2}");

            // Make Swap SOUL / ETH
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol0, poolSymbol2, swapValue)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();
            var afterTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
            Console.WriteLine($"KCAL Fee: {kcalfee}");

            // Check trade
            var originalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol2, testUserB.Address);
            Assert.IsTrue(rate == originalBalance, $"{rate} != {originalBalance}");

            // Make Swap SOUL / KCAL
            scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, swapValue).EndScript();
            resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{swapValue} {poolSymbol0} for {rate} {poolSymbol1}");


            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol0, poolSymbol1, swapValue)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            originalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);

            Assert.IsTrue(rate == originalBalance-afterTXBalanceKCAL+kcalfee, $"{rate} != {originalBalance-afterTXBalanceKCAL+kcalfee}");
        }

        [TestMethod]
        public void SwapTokensReverse()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, 10);
            BigInteger swapValueETH = UnitConversion.ToBigInteger(1, 18);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol2, poolAmount2 * 2);
            simulator.EndBlock();

            var beforeTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserB.Address);
            var beforeTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var beforeTXBalanceETH = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol2, testUserB.Address);

            // Add Liquidity to the pool SOUL / KCAL
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // SOUL / ETH

            // Get Rate
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol2, poolSymbol0, swapValueETH).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(swapValueETH, 18)} {poolSymbol2} for {UnitConversion.ToDecimal(rate, 8)} {poolSymbol0}");
            // Make Swap SOUL / ETH
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol2, poolSymbol0, swapValueETH)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();
            var afterTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL;
            Console.WriteLine($"KCAL Fee: {UnitConversion.ToDecimal(kcalfee, 10)}");

            // Check trade
            var afterTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserB.Address);
            var afterTXBalanceETH = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol2, testUserB.Address);

            Assert.IsTrue(beforeTXBalanceSOUL + rate == afterTXBalanceSOUL, $"{beforeTXBalanceSOUL+rate} != {afterTXBalanceSOUL}");
            Assert.IsTrue(beforeTXBalanceETH - swapValueETH == afterTXBalanceETH, $"{beforeTXBalanceETH - swapValueETH} != {afterTXBalanceETH}");

            // Make Swap SOUL / KCAL
            scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol1, poolSymbol0, swapValueKCAL).EndScript();
            resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, 10)} {poolSymbol1} for {UnitConversion.ToDecimal(rate, 8)} {poolSymbol0}");


            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol1, poolSymbol0, swapValueKCAL)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            var afterTXBalanceSOULEND = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserB.Address);
            var afterTXBalanceKCALEND = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);

            Assert.IsTrue(afterTXBalanceSOUL + rate == afterTXBalanceSOULEND, $"{rate} != {afterTXBalanceSOULEND}");
            Assert.IsTrue(afterTXBalanceKCALEND == afterTXBalanceKCAL - kcalfee - swapValueKCAL, $"{afterTXBalanceKCALEND} != {afterTXBalanceKCAL - kcalfee - swapValueKCAL}");
        }


        [TestMethod]
        public void SwapVirtual()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1000, 10);
            BigInteger swapValueETH = UnitConversion.ToBigInteger(1, 18);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 10);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol2, poolAmount2 * 2);
            simulator.EndBlock();

            var beforeTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var beforeTXBalanceETH = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol2, testUserB.Address);

            // Get Rate
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol1, poolSymbol2, swapValueKCAL).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(swapValueKCAL, 10)} {poolSymbol1} for {UnitConversion.ToDecimal(rate, 18)} {poolSymbol2}");
            // Make Swap SOUL / ETH
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol1, poolSymbol2, swapValueKCAL)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();
            var afterTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var afterTXBalanceETH = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol2, testUserB.Address);
            var kcalfee = beforeTXBalanceKCAL - afterTXBalanceKCAL - swapValueKCAL;


            Assert.IsTrue(afterTXBalanceETH == beforeTXBalanceETH+rate, $"{afterTXBalanceETH} != {beforeTXBalanceETH+rate}");
            Assert.IsTrue(beforeTXBalanceKCAL - kcalfee - swapValueKCAL == afterTXBalanceKCAL, $"{beforeTXBalanceKCAL - kcalfee - swapValueKCAL} != {afterTXBalanceKCAL}");
        }

        [TestMethod]
        // TODO: Finish this
        public void SwapFee()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var initialKcal = 1000000;

            BigInteger swapValueSOUL = UnitConversion.ToBigInteger(1, 8);
            BigInteger swapValueKCAL = UnitConversion.ToBigInteger(1, 10);
            BigInteger swapFee = UnitConversion.ConvertDecimals(swapValueSOUL, 8, 8);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 );
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol2, poolAmount2 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol4, poolAmount4 * 2);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, initialKcal);
            simulator.EndBlock();

            // Get Balances before starting trades
            var beforeTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserB.Address);
            var beforeTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);
            var kcalToSwap = swapValueSOUL;
            kcalToSwap -= UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals, 8);

            // Kcal to trade for
            Console.WriteLine($"Soul amount: {kcalToSwap} | {beforeTXBalanceKCAL} | {swapValueSOUL} - { UnitConversion.ConvertDecimals(beforeTXBalanceKCAL, DomainSettings.FuelTokenDecimals, 8)} = {kcalToSwap}");

            // Get Pool
            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol1, poolSymbol0).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            var rateByPool = pool.Amount0 * (1 - 97 / 100) * kcalToSwap / (pool.Amount1 + (1 - 97 / 100) * kcalToSwap);
            rateByPool = UnitConversion.ConvertDecimals(rateByPool, 10, 8);

            // Get Rate
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, kcalToSwap).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(kcalToSwap, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1} | Swap ->  {UnitConversion.ToDecimal(rateByPool, 10)}");

            // Make Swap SOUL / KCAL (SwapFee)
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 500)
                    .CallContract("swap", "SwapFee", testUserB.Address, poolSymbol0, swapValueSOUL)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Get the balances
            var afterTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserB.Address);
            var afterTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserB.Address);

            var kcalfee = afterTXBalanceKCAL - beforeTXBalanceKCAL - rate;
            Console.WriteLine($"Fee:{UnitConversion.ToDecimal(kcalfee, 10)}");

            Console.WriteLine($"{beforeTXBalanceSOUL} != {afterTXBalanceSOUL} | {afterTXBalanceKCAL}");

            Assert.IsTrue(afterTXBalanceSOUL == beforeTXBalanceSOUL-(kcalToSwap + UnitConversion.ConvertDecimals(500, 10, 8)), $"SOUL {afterTXBalanceSOUL} != {beforeTXBalanceSOUL-(kcalToSwap + UnitConversion.ConvertDecimals(500, 10, 8))}");
            Assert.IsTrue(beforeTXBalanceKCAL + kcalfee + rate == afterTXBalanceKCAL, $"KCAL {beforeTXBalanceKCAL + kcalfee + rate} != {afterTXBalanceKCAL}");
        }

        [TestMethod]
        public void GetUnclaimed()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();

            int swapValue = 1000;

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Get Rate
            //UnitConversion.ConvertDecimals(swapValue, 0, 10)
            var script = new ScriptBuilder().CallContract("swap", "GetUnclaimedFees", testUserA.Address, poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var unclaimed = (BigInteger)result.AsNumber();

            Assert.IsTrue(unclaimed == 0, "Unclaimed Failed");
        }


        [TestMethod]
        public void GetFees()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var swapValueSOUL = UnitConversion.ToBigInteger(10, 8);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, poolAmount1 * 2);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Get Pool
            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            // Get RAM
            var scriptRam = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultRam = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRam);
            var nftRAMBefore = resultRam.AsStruct<LPTokenContentRAM>();

            // Make a Swap
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, swapValueSOUL).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1}");
            // Make Swap SOUL / ETH
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol0, poolSymbol1, swapValueSOUL)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get Rate
            var script = new ScriptBuilder().CallContract("swap", "GetUnclaimedFees", testUserA.Address, poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var unclaimed = (BigInteger)result.AsNumber();
            BigInteger UserPercent = 75;
            BigInteger totalFees = rate * 3 / 100;
            BigInteger feeForUsers = totalFees * 100 / UserPercent;
            BigInteger feeAmount = nftRAMBefore.Liquidity * 1000000000000 / pool.TotalLiquidity;
            var calculatedFees = feeForUsers * feeAmount / 1000000000000;

            Assert.IsTrue(unclaimed == calculatedFees, $"Unclaimed Failed | {unclaimed} != {calculatedFees}");
        }

        [TestMethod]
        public void GetClaimFees()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var swapValueSOUL = UnitConversion.ToBigInteger(10, 8);

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, poolAmount0 * 2);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, poolAmount1 * 3);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, poolAmount0);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, poolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0, poolSymbol1, 10)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Get Pool
            var scriptPool = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var resultPool = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptPool);
            var pool = resultPool.AsStruct<Pool>();

            // Get RAM
            var scriptRam = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultRam = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRam);
            var nftRAMBefore = resultRam.AsStruct<LPTokenContentRAM>();

            // Make a Swap
            var scriptRate = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, swapValueSOUL).EndScript();
            var resultRate = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptRate);
            var rate = (BigInteger)resultRate.AsNumber();

            Console.WriteLine($"{UnitConversion.ToDecimal(swapValueSOUL, 8)} {poolSymbol0} for {UnitConversion.ToDecimal(rate, 10)} {poolSymbol1}");
            // Make Swap SOUL / ETH
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserB, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserB.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "SwapTokens", testUserB.Address, poolSymbol0, poolSymbol1, swapValueSOUL)
                    .SpendGas(testUserB.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get Unclaimed Fees
            var script = new ScriptBuilder().CallContract("swap", "GetUnclaimedFees", testUserA.Address, poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var unclaimed = (BigInteger)result.AsNumber();
            BigInteger UserPercent = 75;
            BigInteger totalFees = rate * 3 / 100;
            BigInteger feeForUsers = totalFees * 100 / UserPercent;
            BigInteger feeAmount = nftRAMBefore.Liquidity * 1000000000000 / pool.TotalLiquidity;
            var calculatedFees = feeForUsers * feeAmount / 1000000000000;

            Console.WriteLine($"{calculatedFees} {poolSymbol1}");
            Assert.IsTrue(unclaimed == calculatedFees, $"Unclaimed Failed | {unclaimed} != {calculatedFees}");

            // Claim Fees
            // Get User Balance Before Claiming Fees
            var beforeTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserA.Address);
            var beforeTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserA.Address);

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "ClaimFees", testUserA.Address, poolSymbol0, poolSymbol1)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get User Balance After Claiming Fees
            var afterTXBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol0, testUserA.Address);
            var afterTXBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, poolSymbol1, testUserA.Address);


            var scriptAfter = new ScriptBuilder().CallContract("swap", "GetUnclaimedFees", testUserA.Address, poolSymbol0, poolSymbol1).EndScript();
            var resultAfter = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptAfter);
            var unclaimedAfter = (BigInteger)resultAfter.AsNumber();

            Assert.IsTrue(afterTXBalanceSOUL == beforeTXBalanceSOUL+calculatedFees, $"Soul Claimed Failed | {afterTXBalanceSOUL} != {beforeTXBalanceSOUL}");
            Assert.IsTrue(beforeTXBalanceKCAL != afterTXBalanceKCAL, $"Kcal for TX Failed | {beforeTXBalanceKCAL} != {afterTXBalanceKCAL}");
            Assert.IsTrue(unclaimedAfter == 0, $"Kcal for TX Failed | {unclaimedAfter} != {0}");


        }

        [TestMethod]
        public void CosmicSwap()
        {
            Init();

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
            Init();

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
            Init();

            var rootChain = nexus.RootChain;

            var testUser = PhantasmaKeys.Generate();

            var potAddress = SmartContract.GetAddressForNative(NativeContractKind.Swap);

            // 0 - just send some assets to the 
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
            simulator.MintTokens(owner, potAddress, "GAS", poolAmount5);
            simulator.MintTokens(owner, testUser.Address, poolSymbol1, poolAmount1);
            simulator.MintTokens(owner, potAddress, poolSymbol1, poolAmount1);
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
            Init();

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
