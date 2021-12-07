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
        BigInteger poolAmount0 = 1000;
        string poolSymbol1 = "KCAL";
        BigInteger poolAmount1 = 16000;

        // Virtual Token
        string virtualPoolSymbol = "COOL";
        BigInteger virtualPoolAmount1 = 10000000;


        
        public void Init()
        {
            owner = PhantasmaKeys.Generate();
            nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulator = new NexusSimulator(nexus, owner, 1234);

            // Migrate Call to setup the Pools to V3
            MigrateCall();

            // Create a Pool
            CreatePools();
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
        
        public void CreatePool()
        {
            owner = PhantasmaKeys.Generate();
            nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            simulator = new NexusSimulator(nexus, owner, 1234);

            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount0 * poolAmount1));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.EndBlock();

            // Get Tokens Info
            //token0
            var token0 = nexus.GetTokenInfo(nexus.RootStorage, poolSymbol0);
            var token0Address = nexus.GetTokenContract(nexus.RootStorage, poolSymbol0);
            Assert.IsTrue(token0.Symbol == poolSymbol0);

            // token1
            var token1 = nexus.GetTokenInfo(nexus.RootStorage, poolSymbol1);
            var token1Address = nexus.GetTokenContract(nexus.RootStorage, poolSymbol1);
            Assert.IsTrue(token1.Symbol == poolSymbol1);

            // Migrate First to Setup the new version
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "MigrateToV3")
                .SpendGas(owner.Address)
                .EndScript());
            var block = simulator.EndBlock().First();

            // Create a Pool
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.Minimal, () =>
                ScriptUtils
                .BeginScript()
                .AllowGas(owner.Address, Address.Null, 1, 9999)
                .CallContract("swap", "CreatePool", owner.Address, poolSymbol0, poolAmount0, poolSymbol1, poolAmount1)
                .SpendGas(owner.Address)
                .EndScript());
            block = simulator.EndBlock().First();

            // Check if the pool was created
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();

            Assert.IsTrue(pool.Symbol0 == poolSymbol0);
            Assert.IsTrue(pool.Amount0 == poolAmount0);
            Assert.IsTrue(pool.Symbol1 == poolSymbol1);
            Assert.IsTrue(pool.Amount1 == poolAmount1);
            Assert.IsTrue(pool.TotalLiquidity == totalLiquidity); 
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
        public void AddLiquidityToPool()
        {
            Init();

            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(poolAmount0/10 * poolAmount1/10));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            // setup Tokens for the user
            simulator.BeginBlock();
            //simulator.GenerateToken(owner, virtualPoolSymbol, "CoolToken", virtualPoolAmount1, 0, TokenFlags.Burnable | TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.EndBlock();

            // Add Liquidity to the pool
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "AddLiquidity", testUserA.Address, poolSymbol0, poolAmount0/10, poolSymbol1, poolAmount1/10)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            var block = simulator.EndBlock().First();

            // Check the Liquidity
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();
            totalLiquidity += (poolAmount0 * pool.TotalLiquidity) / (poolAmount0 + (poolAmount0 / 10));

            Assert.IsTrue(pool.Symbol0 == poolSymbol0, "Symbol is incorrect");
            Assert.IsTrue(pool.Amount0 == poolAmount0 + (poolAmount0 / 10), "Symbol Amount0 is incorrect");
            Assert.IsTrue(pool.Symbol1 == poolSymbol1, "Pair is incorrect");
            Assert.IsTrue(pool.Amount1 == poolAmount1 + (poolAmount1 / 10), "Symbol Amount1 is incorrect");
            Assert.IsTrue(pool.TotalLiquidity == totalLiquidity);
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
        public void RemoveLiquidityToPool()
        {
            Init();

            BigInteger totalAm0 = poolAmount0;
            BigInteger totalAm1 = poolAmount1;
            BigInteger totalLiquidity = (BigInteger)Math.Sqrt((long)(totalAm0 * totalAm1));

            // Setup a test user 
            var testUserA = PhantasmaKeys.Generate();

            // setup Tokens for the user
            simulator.BeginBlock();
            //simulator.GenerateToken(owner, LPTokenSymbol, "LP", 1000, 0, TokenFlags.Burnable | TokenFlags.Transferable);
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

            var scriptBefore = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultBefore = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptBefore);
            var nftRAMBefore = resultBefore.AsStruct<LPTokenContentRAM>();

            // Remove Liquidity
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUserA, ProofOfWork.Minimal, () =>
                    ScriptUtils
                    .BeginScript()
                    .AllowGas(testUserA.Address, Address.Null, 1, 9999)
                    .CallContract("swap", "RemoveLiquidity", testUserA.Address.Text, poolSymbol0, poolAmount0/10, poolSymbol1, poolAmount1/10)
                    .SpendGas(testUserA.Address)
                    .EndScript()
                );
            block = simulator.EndBlock().First();

            // Get Pool
            var script = new ScriptBuilder().CallContract("swap", "GetPool", poolSymbol0, poolSymbol1).EndScript();
            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);
            var pool = result.AsStruct<Pool>();
            var lpRemoved = ((poolAmount0 / 10) * totalLiquidity) / totalAm0;
            totalLiquidity -= lpRemoved;
            totalAm0 -= (poolAmount0 / 10);

            // Get My NFT DATA 
            var scriptAfter = new ScriptBuilder().CallContract("swap", "GetMyPoolRAM", testUserA.Address.Text, poolSymbol0, poolSymbol1).EndScript();
            var resultAfter = nexus.RootChain.InvokeScript(nexus.RootStorage, scriptAfter);
            var nftRAMAfter = resultAfter.AsStruct<LPTokenContentRAM>();
            
            // Validation
            Assert.IsFalse(nftRAMBefore.Amount0 == nftRAMAfter.Amount0, "Amount0 does not differ.");
            Assert.IsFalse(nftRAMBefore.Amount1 == nftRAMAfter.Amount1, "Amount1 does not differ.");
            Assert.IsFalse(nftRAMBefore.Liquidity == nftRAMAfter.Liquidity, "Liquidity does not differ.");

            Assert.IsTrue(nftRAMBefore.Amount0 - (poolAmount0 / 10) == nftRAMAfter.Amount0, "Amount0 not true.");
            Assert.IsTrue(nftRAMBefore.Amount1 - (poolAmount1 / 10) == nftRAMAfter.Amount1, "Amount1 not true.");

            // Get Amount by Liquidity
            // Liqudity Formula  Liquidity = (amount0 * pool.TotalLiquidity) / pool.Amount0;
            // Amount Formula  amount = Liquidity  * pool.Amount0 / pool.TotalLiquidity;
            var amount0 = nftRAMAfter.Liquidity * pool.Amount0 /  pool.TotalLiquidity;
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
        public void GetRatesForSwap()
        {
            Init();

            var script = new ScriptBuilder().CallContract("swap", "GetRates", poolSymbol0, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)).EndScript();

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
            Init();

            var script = new ScriptBuilder().CallContract("swap", "GetRate", poolSymbol0, poolSymbol1, UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals)).EndScript();

            var result = nexus.RootChain.InvokeScript(nexus.RootStorage, script);

            var temp = result.ToObject();
            var rate = (BigInteger)temp;

            decimal targetRate = 0;

            targetRate = UnitConversion.ToDecimal(rate, DomainSettings.FuelTokenDecimals);
            Console.WriteLine($"Rate:{rate} |-> {targetRate}");

            Assert.IsTrue(targetRate == 5m);
        }

        [TestMethod]
        [Ignore]
        public void SwapTokens()
        {
            Init();

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.MintTokens(owner, testUserA.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserA.Address, poolSymbol1, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol0, virtualPoolAmount1);
            simulator.MintTokens(owner, testUserB.Address, poolSymbol1, virtualPoolAmount1);
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

            // First get the amount of tokens for a determinated amount

            // TODO
            Assert.IsFalse(true, "need Implementation");

        }

        [TestMethod]
        [Ignore]
        public void SwapTokensReverse()
        {
            Init();

            // TODO
            Assert.IsFalse(true, "need Implementation");
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
