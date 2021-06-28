using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

using Phantasma.Blockchain;
using Phantasma.Storage.Context;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Simulator;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Tokens;
using Phantasma.CodeGen.Assembler;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Types;
using Phantasma.VM;
using System.Collections.Generic;
using Phantasma.Storage;
using Phantasma.Pay.Chains;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void NullAddress()
        {
            var addr = Address.Null;
            Assert.IsTrue(addr.IsNull);
            Assert.IsTrue(addr.IsSystem);
            Assert.IsFalse(addr.IsUser);
            Assert.IsFalse(addr.IsInterop);

            Assert.IsTrue(Address.IsValidAddress(addr.Text));
        }

        [TestMethod]
        public void Decimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            var tmp1 = UnitConversion.ToBigInteger(UnitConversion.ToDecimal(n, places), places);

            Assert.IsTrue(n == tmp1);
            Assert.IsTrue(d == UnitConversion.ToDecimal(UnitConversion.ToBigInteger(d, places), places));

            Assert.IsTrue(d == UnitConversion.ToDecimal(n, places));
            Assert.IsTrue(n == UnitConversion.ToBigInteger(d, places));

            var tmp2 = UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(tmp2 > 0);

            decimal eos = 1006245120;
            var tmp3 = UnitConversion.ToBigInteger(eos, 18);
            var dec = UnitConversion.ToDecimal(tmp3, 18);
            Assert.IsTrue(dec == eos);

            BigInteger small = 60;
            var tmp4 = UnitConversion.ToDecimal(small, 10);
            var dec2 = 0.000000006m;
            Assert.IsTrue(dec2 == tmp4);
        }

        [TestMethod]
        public void GenesisBlock()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));

            Assert.IsTrue(nexus.CreateGenesisBlock(owner, DateTime.Now, 1));

            var genesisHash = nexus.GetGenesisHash(nexus.RootStorage);
            Assert.IsTrue(genesisHash != Hash.Null);

            var rootChain = nexus.RootChain;

            Assert.IsTrue(rootChain.Address.IsSystem);
            Assert.IsFalse(rootChain.Address.IsNull);

            var symbol = DomainSettings.FuelTokenSymbol;
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol));
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(token.MaxSupply == 0);

            var supply = nexus.RootChain.GetTokenSupply(rootChain.Storage, symbol);
            Assert.IsTrue(supply > 0);

            var balance = UnitConversion.ToDecimal(nexus.RootChain.GetTokenBalance(rootChain.Storage, token, owner.Address), DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(balance > 0);

            Assert.IsTrue(rootChain != null);
            Assert.IsTrue(rootChain.Height > 0);

            /*var children = nexus.GetChildChainsByName(nexus.RootStorage, rootChain.Name);
            Assert.IsTrue(children.Any());*/

            Assert.IsTrue(nexus.IsPrimaryValidator(owner.Address));

            var randomKey = PhantasmaKeys.Generate();
            Assert.IsFalse(nexus.IsPrimaryValidator(randomKey.Address));

            /*var txCount = nexus.GetTotalTransactionCount();
            Assert.IsTrue(txCount > 0);*/
        }

        [TestMethod]
        public void FuelTokenTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var accountChain = nexus.GetChainByName("account");
            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            // Send from Genesis address to test user A
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserA.Address);

            Assert.IsTrue(oldBalance == amount);

            // Send from test user A address to test user B
            amount /= 2;
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserA.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            var expectedFee = oldBalance - (newBalance + transferBalance);
            Assert.IsTrue(expectedFee == gasFee);

            var sum = transferBalance + newBalance + gasFee;
            Assert.IsTrue(sum == oldBalance);
        }

        [TestMethod]
        public void CreateToken()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var accountChain = nexus.GetChainByName("account");
            var symbol = "BLA";

            var tokenAsm = new string[]
            {
                "LOAD r1 42",
                "PUSH r1",
                "RET"
            };

            var tokenScript = AssemblerUtils.BuildScript(tokenAsm);

            var methods = new ContractMethod[]
            {
                new ContractMethod("mycall", VMType.Number, 0, new ContractParameter[0])
            };

            var tokenSupply = UnitConversion.ToBigInteger(10000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, 18, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible, tokenScript, null, methods);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);

            Assert.IsTrue(nexus.RootChain.IsContractDeployed(nexus.RootChain.Storage, symbol));

            // try call token contract method
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
            {
                return new ScriptBuilder()
                .AllowGas(owner.Address, Address.Null, simulator.MinimumFee, 999)
                .CallContract(symbol, "mycall")
                .SpendGas(owner.Address)
                .EndScript();
            });
            var block = simulator.EndBlock().First();

            var callResultBytes = block.GetResultForTransaction(tx.Hash);
            var callResult = Serialization.Unserialize<VMObject>(callResultBytes);
            var num = callResult.AsNumber();

            Assert.IsTrue(num == 42);
        }

        [TestMethod]
        public void CreateNonDivisibleToken()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var accountChain = nexus.GetChainByName("account");
            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(100000000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", tokenSupply, 0, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void AccountRegister()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var symbol = DomainSettings.FuelTokenSymbol;

            Func<PhantasmaKeys, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    var lastBlock = simulator.EndBlock().FirstOrDefault();

                    if (lastBlock != null)
                    {
                        Assert.IsTrue(tx != null);

                        var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                        Assert.IsTrue(evts.Any(x => x.Kind == Domain.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var testUser = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(balance == amount);

            // make user stake enough to register a name
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).
                    EndScript());
            simulator.EndBlock();

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
            Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
            Assert.IsFalse(registerName(testUser, targetName + "!"));
            Assert.IsTrue(registerName(testUser, targetName));

            var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(nexus.RootStorage, targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            Assert.IsFalse(registerName(testUser, "other"));
        }

        [TestMethod]
        public void AccountMigrate()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var symbol = DomainSettings.FuelTokenSymbol;

            var testUser = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            var stakeAmount = UnitConversion.ToBigInteger(3, DomainSettings.StakingTokenDecimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);
            Assert.IsTrue(balance == amount);

            // make user stake enough to register a name
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(testUser.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).
                    EndScript());
            simulator.EndBlock();

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            simulator.BeginBlock();
            var tx = simulator.GenerateAccountRegistration(testUser, targetName);
            var lastBlock = simulator.EndBlock().FirstOrDefault();

            if (lastBlock != null)
            {
                Assert.IsTrue(tx != null);

                var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                Assert.IsTrue(evts.Any(x => x.Kind == Domain.EventKind.AddressRegister));
            }


            var currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(nexus.RootStorage, targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            var migratedUser = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
            {
                return ScriptUtils.BeginScript().
                     AllowGas(testUser.Address, Address.Null, 400, 9999).
                     CallContract(NativeContractKind.Account, nameof(AccountContract.Migrate), testUser.Address, migratedUser.Address).
                     SpendGas(testUser.Address).
                     EndScript();
            });
            simulator.EndBlock().FirstOrDefault();

            currentName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, testUser.Address);
            Assert.IsFalse(currentName == targetName);

            var newName = nexus.RootChain.GetNameFromAddress(nexus.RootStorage, migratedUser.Address);
            Assert.IsTrue(newName == targetName);
        }

        [TestMethod]
        public void SimpleTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            var txB = simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            // Send from user A to user B
            simulator.BeginBlock();
            var txC = simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUserA.Address);
            Assert.IsTrue(hashes.Length == 3);
            Assert.IsTrue(hashes.Any(x => x == txA.Hash));
            Assert.IsTrue(hashes.Any(x => x == txB.Hash));
            Assert.IsTrue(hashes.Any(x => x == txC.Hash));

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }

        [TestMethod]
        public void GenesisMigration()
        {
            var firstOwner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, firstOwner, 1234);

            var secondOwner = PhantasmaKeys.Generate();
            var testUser = PhantasmaKeys.Generate();
            var anotherTestUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(firstOwner, secondOwner.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.GenerateTransfer(firstOwner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, transferAmount);
            simulator.EndBlock();

            var oldToken = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.RewardTokenSymbol);
            Assert.IsTrue(oldToken.Owner == firstOwner.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(firstOwner, ProofOfWork.None, () =>
            {
                return ScriptUtils.BeginScript().
                     AllowGas(firstOwner.Address, Address.Null, 400, 9999).
                     CallContract("account", "Migrate", firstOwner.Address, secondOwner.Address).
                     SpendGas(firstOwner.Address).
                     EndScript();
            });
            simulator.EndBlock();

            var newToken = nexus.GetTokenInfo(nexus.RootStorage, DomainSettings.RewardTokenSymbol);
            Assert.IsTrue(newToken.Owner == secondOwner.Address);

            simulator.BeginBlock(secondOwner); // here we change the validator keys in simulator
            simulator.GenerateTransfer(secondOwner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            // inflation check
            simulator.TimeSkipDays(91);
            simulator.BeginBlock(); 
            simulator.GenerateTransfer(testUser, anotherTestUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            var crownBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.RewardTokenSymbol, firstOwner.Address);
            Assert.IsTrue(crownBalance == 0);

            crownBalance = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.RewardTokenSymbol, secondOwner.Address);
            Assert.IsTrue(crownBalance == 1);

            var thirdOwner = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(secondOwner, ProofOfWork.None, () =>
            {
                return ScriptUtils.BeginScript().
                     AllowGas(secondOwner.Address, Address.Null, 400, 9999).
                     CallContract("account", "Migrate", secondOwner.Address, thirdOwner.Address).
                     SpendGas(secondOwner.Address).
                     EndScript();
            });
            simulator.EndBlock();

            simulator.BeginBlock(thirdOwner); // here we change the validator keys in simulator
            simulator.GenerateTransfer(thirdOwner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();
        }

        [TestMethod]
        public void SystemAddressTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            var txA = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            var txB = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            var hashes = simulator.Nexus.RootChain.GetTransactionHashesForAddress(testUser.Address);
            Assert.IsTrue(hashes.Length == 2);
            Assert.IsTrue(hashes.Any(x => x == txA.Hash));
            Assert.IsTrue(hashes.Any(x => x == txB.Hash));

            var stakeToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, stakeToken, testUser.Address);
            Assert.IsTrue(finalBalance == transferAmount);

            var fuelToken = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, fuelToken, testUser.Address);
            Assert.IsTrue(finalBalance == transferAmount);
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
        public void TransferToAccountName()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            var symbol = DomainSettings.FuelTokenSymbol;

            Func<PhantasmaKeys, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    var lastBlock = simulator.EndBlock().FirstOrDefault();

                    if (lastBlock != null)
                    {
                        Assert.IsTrue(tx != null);

                        var evts = lastBlock.GetEventsForTransaction(tx.Hash);
                        Assert.IsTrue(evts.Any(x => x.Kind == Domain.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var targetName = "hello";
            var testUser = PhantasmaKeys.Generate();
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, amount);
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, amount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            Assert.IsTrue(registerName(testUser, targetName));

            // Send from Genesis address to test user
            var transferAmount = 1;

            var initialFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, 1, 9999)
                    .TransferTokens(token.Symbol, owner.Address, targetName, transferAmount)
                    .SpendGas(owner.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUser.Address);

            Assert.IsTrue(finalFuelBalance - initialFuelBalance == transferAmount);
        }

        [TestMethod]
        public void SideChainTransferDifferentAccounts()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var sourceChain = nexus.RootChain;

            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName("test");

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            var crossFee = UnitConversion.ToBigInteger(0.001m, token.Decimals);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, sideAmount, crossFee);
            simulator.EndBlock();
            var blockAHash = nexus.RootChain.GetLastBlockHash();
            var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, txA);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(targetChain.Storage, token, receiver.Address);
            var expectedAmount = (sideAmount + crossFee) - feeB;
            Assert.IsTrue(balance == expectedAmount);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA + crossFee);

            balance = sourceChain.GetTokenBalance(sourceChain.Storage, token, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);
        }

        [TestMethod]
        public void SideChainTransferSameAccount()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var sourceChain = nexus.RootChain;

            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);
            var originalAmount = UnitConversion.ToBigInteger(1, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName("test");

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, targetChain, sideAmount, 0);
            var blockA = simulator.EndBlock().FirstOrDefault();
            Assert.IsTrue(blockA != null);

            // finish the chain transfer from parent to child
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, sourceChain, targetChain, txA);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify balances
            var feeB = targetChain.GetTransactionFee(txB);
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            //Assert.IsTrue(balance == sideAmount - feeB); TODO CHECK THIS BERNARDO

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA);

            balance = sourceChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);

            sideAmount /= 2;
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, targetChain, sender.Address, sourceChain, sideAmount, 0);
            var blockC = simulator.EndBlock().FirstOrDefault();
            Assert.IsTrue(blockC != null);

            // finish the chain transfer from child to parent
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(sender, targetChain, sourceChain, txC);
            Assert.IsTrue(simulator.EndBlock().Any());
        }

        [TestMethod]
        public void SideChainTransferMultipleSteps()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, nexus.RootChain.Name, "sale");
            simulator.EndBlock();

            var sourceChain = nexus.RootChain;
            var sideChain = nexus.GetChainByName("sale");
            Assert.IsTrue(sideChain != null);

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            var newChainName = "testing";

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, sideChain.Name, newChainName);
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName(newChainName);

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            // do a side chain send using test user balance from root to apps chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, sender.Address, sideChain, sideAmount, 0);
            var blockA = simulator.EndBlock().FirstOrDefault();
            var evtsA = blockA.GetEventsForTransaction(txA.Hash);

            // finish the chain transfer
            simulator.BeginBlock();
            var txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, sideChain, txA);
            Assert.IsTrue(simulator.EndBlock().Any());

            var txCostA = simulator.Nexus.RootChain.GetTransactionFee(txA);
            var txCostB = sideChain.GetTransactionFee(txB);
            sideAmount = sideAmount - txCostA;

            balance = sideChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Console.WriteLine($"{balance}/{sideAmount}");
            Assert.IsTrue(balance == sideAmount);

            var extraFree = UnitConversion.ToBigInteger(0.01m, token.Decimals);

            sideAmount -= extraFree * 10;

            // do another side chain send using test user balance from apps to target chain
            simulator.BeginBlock();
            var txC = simulator.GenerateSideChainSend(sender, symbol, sideChain, receiver.Address, targetChain, sideAmount, extraFree);
            var blockC = simulator.EndBlock().FirstOrDefault();

            var evtsC = blockC.GetEventsForTransaction(txC.Hash);

            var appSupplies = new SupplySheet(symbol, sideChain, nexus);
            var childBalance = appSupplies.GetChildBalance(sideChain.Storage, targetChain.Name);
            var expectedChildBalance = sideAmount + extraFree;

            // finish the chain transfer
            simulator.BeginBlock();
            var txD = simulator.GenerateSideChainSettlement(receiver, sideChain, targetChain, txC);
            Assert.IsTrue(simulator.EndBlock().Any());

            // TODO  verify balances
        }

        [TestMethod]
        public void NftMint()
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
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken to test address
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            // check used storage
            var tokenAddress = TokenUtils.GetContractAddress(symbol);
            var usedStorage = (int)nexus.RootChain.InvokeContract(nexus.RootChain.Storage, "storage", nameof(StorageContract.GetUsedSpace), tokenAddress).AsNumber();
            var minExpectedSize = tokenROM.Length + tokenRAM.Length;
            Assert.IsTrue(usedStorage >= minExpectedSize);

            //verify that the present nft is the same we actually tried to create
            var tokenID = ownedTokenList.First();
            var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenID);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
            Assert.IsTrue(currentSupply == 1, "why supply did not increase?");

            var testScript = new ScriptBuilder().CallNFT(symbol, 0, "getName", tokenID).EndScript();
            var temp  = simulator.Nexus.RootChain.InvokeScript(simulator.Nexus.RootStorage, testScript);
            var testResult = temp.AsString();
            Assert.IsTrue(testResult == "CoolToken");
        }


        [TestMethod]
        public void NftBurn()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            BigInteger seriesID = 123;

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Burnable, null, null, null, (uint)seriesID);
            simulator.EndBlock();

            var series = nexus.GetTokenSeries(nexus.RootStorage, symbol, seriesID);

            Assert.IsTrue(series.MintCount == 0, "nothing should be minted yet");

            // Send some KCAL and SOUL to the test user (required for gas used in "burn" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals));
            simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals));
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken to test address
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, seriesID);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the user not have one now?");

            var ownerAddress = ownerships.GetOwner(chain.Storage, tokenID);
            Assert.IsTrue(ownerAddress == testUser.Address);

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            Assert.IsTrue(nft.Infusion.Length == 0); // nothing should be infused yet

            var infuseSymbol = DomainSettings.StakingTokenSymbol;
            var infuseAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);

            var prevBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);

            // Infuse some KCAL to the CoolToken
            simulator.BeginBlock();
            simulator.InfuseNonFungibleToken(testUser, symbol, tokenId, infuseSymbol, infuseAmount);
            simulator.EndBlock();

            nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.Infusion.Length == 1); // should have something infused now

            var infusedBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, DomainSettings.InfusionAddress);
            Assert.IsTrue(infusedBalance == infuseAmount); // should match

            var curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);
            Assert.IsTrue(curBalance + infusedBalance == prevBalance); // should match

            prevBalance = curBalance;

            // burn the token
            simulator.BeginBlock();
            simulator.GenerateNftBurn(testUser, chain, symbol, tokenId);
            simulator.EndBlock();

            //verify the user no longer has the token
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user still have it post-burn?");

            // verify that the user received the infused assets
            curBalance = nexus.RootChain.GetTokenBalance(nexus.RootChain.Storage, infuseSymbol, testUser.Address);
            Assert.IsTrue(curBalance == prevBalance + infusedBalance); // should match

            var burnedSupply = nexus.GetBurnedTokenSupply(nexus.RootStorage, symbol);
            Assert.IsTrue(burnedSupply == 1);

            var burnedSeriesSupply = nexus.GetBurnedTokenSupplyForSeries(nexus.RootStorage, symbol, seriesID);
            Assert.IsTrue(burnedSeriesSupply == 1);
        }

        [TestMethod]
        public void NftTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var chain = nexus.RootChain;

            var nftKey = PhantasmaKeys.Generate();
            var symbol = "COOL";
            var nftName = "CoolToken";

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals));
            simulator.EndBlock();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, nftName, 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            // verify nft presence on the sender post-mint
            ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            var txA = simulator.GenerateNftTransfer(sender, receiver.Address, chain, symbol, tokenId);
            simulator.EndBlock();

            // verify nft presence on the receiver post-transfer
            ownedTokenList = ownerships.Get(chain.Storage, receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void NftMassMint()
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
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var tokenAddress = TokenUtils.GetContractAddress(symbol);
            var storageStakeAmount = UnitConversion.ToBigInteger(100000, DomainSettings.StakingTokenDecimals);

            // Add some storage to the NFT contract
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, tokenAddress, chain, DomainSettings.StakingTokenSymbol, storageStakeAmount);

            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.StakeContractName, nameof(StakeContract.Stake), tokenAddress, storageStakeAmount).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();


            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have nfts?");

            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            var nftCount = 1000;

            var initialKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

            // Mint several nfts to test limit per tx
            simulator.BeginBlock();
            for (int i=1; i<=nftCount; i++)
            {
                var tokenROM = BitConverter.GetBytes(i);
                simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            }
            var block = simulator.EndBlock().First();

            Assert.IsTrue(block.TransactionCount == nftCount);

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            var ownedTotal = ownedTokenList.Count();
            Assert.IsTrue(ownedTotal == nftCount);

            var currentKCAL = nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, owner.Address);

            var fee = initialKCAL - currentKCAL;

            var convertedFee = UnitConversion.ToDecimal(fee, DomainSettings.FuelTokenDecimals);

            Assert.IsTrue(fee > 0);
        }

        [TestMethod]
        [Ignore] //TODO side chain transfers of NFTs do currently not work, because Storage contract is not deployed on the side chain.
        public void SidechainNftTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var sourceChain = nexus.RootChain;

            var symbol = "COOL";

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var fullAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var smallAmount = fullAmount / 2;
            Assert.IsTrue(smallAmount > 0);

            // Send some SOUL to the test user (required for gas used in "transfer" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, sourceChain, DomainSettings.FuelTokenSymbol, fullAmount);
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, "main", "test");
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName("test");

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(nexus.RootStorage, symbol);
            Assert.IsTrue(nexus.TokenExists(nexus.RootStorage, symbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            // verify nft presence on the sender post-mint
            ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            // verify nft presence on the receiver pre-transfer
            ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the receiver already have a CoolToken?");

            var extraFee = UnitConversion.ToBigInteger(0.001m, DomainSettings.FuelTokenDecimals);

            // transfer that nft from sender to receiver
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain, tokenId, extraFee);
            simulator.EndBlock();

            var blockAHash = nexus.RootChain.GetLastBlockHash();
            var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

            Console.WriteLine("step 1");
            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(receiver, nexus.RootChain, targetChain, txA);
            Assert.IsTrue(simulator.EndBlock().Any());

            // verify the sender no longer has it
            ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender still have one?");

            // verify nft presence on the receiver post-transfer
            ownedTokenList = ownerships.Get(targetChain.Storage, receiver.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the receiver not have one now?");

            //verify that the transfered nft is the same we actually tried to create
            tokenId = ownedTokenList.ElementAt(0);
            nft = nexus.ReadNFT(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void NoGasSameChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var accountChain = nexus.GetChainByName("account");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(1, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(oldBalance == amount);

            var gasFee = nexus.RootChain.GetTransactionFee(tx);
            Assert.IsTrue(gasFee > 0);

            amount /= 2;
            simulator.BeginBlock();
            simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);

            Assert.IsTrue(transferBalance + newBalance + gasFee == oldBalance);

            // create a new receiver
            receiver = PhantasmaKeys.Generate();

            //Try to send the entire balance without affording fees from sender to receiver
            try
            {
                simulator.BeginBlock();
                tx = simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, transferBalance);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            // verify balances, receiver should have 0 balance
            transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);
            Assert.IsTrue(transferBalance == 0, "Transaction failed completely as expected");
        }

        [TestMethod]
        public void NoGasTestSideChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateChain(owner, DomainSettings.ValidatorsOrganizationName, nexus.RootChain.Name, "sale");
            simulator.EndBlock();

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.GetChainByName("sale");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(nexus.RootStorage, symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, sender.Address);
            Assert.IsTrue(balance == originalAmount);

            Transaction txA = null, txB = null;

            try
            {
                // do a side chain send using test user balance from root to account chain
                simulator.BeginBlock();
                txA = simulator.GenerateSideChainSend(sender, symbol, sourceChain, receiver.Address, targetChain,
                    originalAmount, 1);
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            try
            {
                var blockAHash = nexus.RootChain.GetLastBlockHash();
                var blockA = nexus.RootChain.GetBlockByHash(blockAHash);

                // finish the chain transfer
                simulator.BeginBlock();
                txB = simulator.GenerateSideChainSettlement(sender, nexus.RootChain, targetChain, txA);
                Assert.IsTrue(simulator.EndBlock().Any());
            }
            catch (Exception e)
            {
                Assert.IsNotNull(e);
            }

            // verify balances, receiver should have 0 balance
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, token, receiver.Address);
            Assert.IsTrue(balance == 0);
        }


        [TestMethod]
        public void AddressComparison()
        {
            var owner = PhantasmaKeys.FromWIF("Kweyrx8ypkoPfzMsxV4NtgH8vXCWC1s1Dn3c2KJ4WAzC5nkyNt3e");
            var expectedAddress = owner.Address.Text;

            var input = "P2K9LSag1D7EFPBvxMa1fW1c4oNbmAQX7qj6omvo17Fwrg8";
            var address = Address.FromText(input);

            Assert.IsTrue(expectedAddress == input);

            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var genesisAddress = nexus.GetGenesisAddress(nexus.RootStorage);
            Assert.IsTrue(address == genesisAddress);
            Assert.IsTrue(address.Text == genesisAddress.Text);
            Assert.IsTrue(address.ToByteArray().SequenceEqual(genesisAddress.ToByteArray()));
        }

        [TestMethod]
        public void ChainTransferExploit()
        {
            var owner = PhantasmaKeys.FromWIF("L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25");
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var user = PhantasmaKeys.Generate();

            var symbol = DomainSettings.StakingTokenSymbol;

            var chainAddressStr = Base16.Encode(simulator.Nexus.RootChain.Address.ToByteArray());
            var userAddressStr = Base16.Encode(user.Address.ToByteArray());

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
            simulator.EndBlock();

            var chainAddress = simulator.Nexus.RootChain.Address;
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, chainAddress, simulator.Nexus.RootChain, symbol, 100000000);
            var block = simulator.EndBlock().First();

            var evts = block.GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(evts.Any(x => x.Kind == EventKind.TokenReceive && x.Address == chainAddress));

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, symbol);

            var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, chainAddress);
            Assert.IsTrue(initialBalance > 10000);

            string[] scriptString = new string[]
            {
                $"alias r5, $sourceAddress",
                $"alias r6, $targetAddress",
                $"alias r7, $amount",
                $"alias r8, $symbol",

                $"load $amount, 10000",
                $@"load $symbol, ""{symbol}""",

                $"load r11 0x{chainAddressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop $sourceAddress",

                $"load r11 0x{userAddressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop $targetAddress",

                $"push $amount",
                $"push $symbol",
                $"push $targetAddress",
                $"push $sourceAddress",
                "extcall \"Runtime.TransferTokens\"",
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(user, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(user.Address, Address.Null, 1, 9999).
                    EmitRaw(script).
                    SpendGas(user.Address).
                    EndScript());

            try
            {
                simulator.EndBlock();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e is ChainException);
            }

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, simulator.Nexus.RootChain.Address);
            Assert.IsTrue(initialBalance == finalBalance);
        }

        [TestMethod]
        public void TransactionFees()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            simulator.MinimumFee = 100000;


            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            // Send from user A to user B
            simulator.BeginBlock();
            simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            var block = simulator.EndBlock().FirstOrDefault();

            Assert.IsTrue(block != null);

            var hash = block.TransactionHashes.First();

            var feeValue = nexus.RootChain.GetTransactionFee(hash);
            var feeAmount = UnitConversion.ToDecimal(feeValue, DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(feeAmount >= 0.0009m);

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }

        [TestMethod]
        public void ValidatorSwitch()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var secondValidator = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            // make first validator allocate 5 more validator spots       
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, secondValidator.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, secondValidator.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.GovernanceContractName, "SetValue", ValidatorContract.ValidatorCountTag, new BigInteger(5)).
                    SpendGas(owner.Address).
                    EndScript());
            simulator.EndBlock();

            // make second validator candidate stake enough to become a stake master
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(secondValidator, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(secondValidator.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.StakeContractName, "Stake", secondValidator.Address, stakeAmount).
                    SpendGas(secondValidator.Address).
                    EndScript());
            simulator.EndBlock();

            // set a second validator, no election required because theres only one validator for now
            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().
                    AllowGas(owner.Address, Address.Null, 1, 9999).
                    CallContract(Nexus.ValidatorContractName, "SetValidator", secondValidator.Address, 1, ValidatorType.Primary).
                    SpendGas(owner.Address).
                    EndScript());
            var block = simulator.EndBlock().First();

            // verify that we suceed adding a new validator
            var events = block.GetEventsForTransaction(tx.Hash).ToArray();
            Assert.IsTrue(events.Length > 0);
            Assert.IsTrue(events.Any(x => x.Kind == EventKind.ValidatorPropose));

            // make the second validator accept his spot
            simulator.BeginBlock();
            tx = simulator.GenerateCustomTransaction(secondValidator, ProofOfWork.None, () =>
            ScriptUtils.BeginScript().
                AllowGas(secondValidator.Address, Address.Null, 1, 9999).
                CallContract(Nexus.ValidatorContractName, "SetValidator", secondValidator.Address, 1, ValidatorType.Primary).
                SpendGas(secondValidator.Address).
                EndScript());
            block = simulator.EndBlock().First();

            // verify that we suceed electing a new validator
            events = block.GetEventsForTransaction(tx.Hash).ToArray();
            Assert.IsTrue(events.Length > 0);
            Assert.IsTrue(events.Any(x => x.Kind == EventKind.ValidatorElect));

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var validatorSwitchAttempts = 100;
            var transferAmount = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);
            var accountBalance = transferAmount * validatorSwitchAttempts;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, accountBalance);
            simulator.EndBlock();
            
            var currentValidatorIndex = 0;

            var token = simulator.Nexus.GetTokenInfo(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol);
            for (int i = 0; i < validatorSwitchAttempts; i++)
            {
                var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);

                // here we skip to a time where its supposed to be the turn of the given validator index

                SkipToValidatorIndex(simulator, currentValidatorIndex);
                //simulator.CurrentTime = (DateTime)simulator.Nexus.GenesisTime + TimeSpan.FromSeconds(120 * 500 + 130);

                //TODO needs to be checked again
                //var currentValidator = currentValidatorIndex == 0 ? owner : secondValidator;
                var currentValidator = (simulator.Nexus.RootChain.GetValidator(simulator.Nexus.RootStorage, simulator.CurrentTime) == owner.Address) ? owner : secondValidator;

                simulator.BeginBlock(currentValidator);
                simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
                var lastBlock = simulator.EndBlock().First();

                var firstTxHash = lastBlock.TransactionHashes.First();
                events = lastBlock.GetEventsForTransaction(firstTxHash).ToArray();
                Assert.IsTrue(events.Length > 0);
                //Assert.IsTrue(events.Any(x => x.Kind == EventKind.ValidatorSwitch));

                var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, token, testUserB.Address);
                Assert.IsTrue(finalBalance == initialBalance + transferAmount);

                currentValidatorIndex = currentValidatorIndex == 1 ? 0 : 1; //toggle the current validator index
            }

            // Send from user A to user B
            // NOTE this block is baked by the second validator
            
        }

        private void SkipToValidatorIndex(NexusSimulator simulator, int i)
        {
            uint skippedSeconds = 0;
            var genesisBlock = simulator.Nexus.GetGenesisBlock();
            DateTime genesisTime = genesisBlock.Timestamp;
            var diff = (simulator.CurrentTime - genesisTime).Seconds;
            //var index = (int)(diff / 120) % 2;
            skippedSeconds = (uint)(120 - diff);
            //Console.WriteLine("index: " + index);

            //while (index != i)
            //{
            //    skippedSeconds++;
            //    diff++;
            //    index = (int)(diff / 120) % 2;
            //}

            Console.WriteLine("skippedSeconds: " + skippedSeconds);
            simulator.CurrentTime = simulator.CurrentTime.AddSeconds(skippedSeconds);
        }

        [TestMethod]
        public void GasFeeCalculation()
        {
            var limit = 400;
            var testUser = PhantasmaKeys.Generate();
            var transcodedAddress = PhantasmaKeys.Generate().Address;
            var swapSymbol = "SOUL";

            var script = new ScriptBuilder()
            .CallContract("interop", "SettleTransaction", transcodedAddress, "neo", "neo", Hash.Null)
            .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
            .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
            .AllowGas(transcodedAddress, Address.Null, 9999, limit)
            .SpendGas(transcodedAddress).EndScript();

            var vm = new GasMachine(script, 0, null);
            var result = vm.Execute();
            Assert.IsTrue(result == VM.ExecutionState.Halt);
            Assert.IsTrue(vm.UsedGas > 0);
        }

        [TestMethod]
        public void ChainTransferStressTest()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            simulator.BeginBlock();
            for (int i = 0; i < 1000; i++)
            {
                var target = PhantasmaKeys.Generate();
                simulator.GenerateTransfer(owner, target.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
            }
            simulator.EndBlock();

            var x = 0;

            for (int i = 0; i < 1000; i++)
            {
                var target = PhantasmaKeys.Generate();
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
                simulator.EndBlock();
            }

            x = 0;
        }

        [TestMethod]
        public void DeployCustomAccountScript()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            string[] scriptString;

            string message = "customEvent";
            var addressStr = Base16.Encode(testUser.Address.ToByteArray());

            var onMintTrigger = AccountTrigger.OnMint.ToString();
            var onWitnessTrigger = AccountTrigger.OnWitness.ToString();

            scriptString = new string[]
            {
                $"alias r4, $triggerMint",
                $"alias r5, $triggerWitness",
                $"alias r6, $comparisonResult",
                $"alias r8, $currentAddress",
                $"alias r9, $sourceAddress",

                $"@{onWitnessTrigger}: NOP ",
                $"pop $currentAddress",
                $"load r11 0x{addressStr}",
                $"push r11",
                "extcall \"Address()\"",
                $"pop $sourceAddress",
                $"equal $sourceAddress, $currentAddress, $comparisonResult",
                $"jmpif $comparisonResult, @end",
                $"load r0 \"something failed\"",
                $"throw r0",

                $"@{onMintTrigger}: NOP",
                $"pop $currentAddress",
                $"load r11 0x{addressStr}",
                $"push r11",
                $"extcall \"Address()\"",
                $"pop r11",

                $"load r10, {(int)EventKind.Custom}",
                $@"load r12, ""{message}""",

                $"push r10",
                $"push r11",
                $"push r12",
                $"extcall \"Runtime.Event\"",

                $"@end: ret"
            };

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var triggerList = new[] { AccountTrigger.OnWitness, AccountTrigger.OnMint };

            // here we fetch the jump offsets for each trigger
            var triggerMap = new Dictionary<AccountTrigger, int>();
            foreach (var trigger in triggerList)
            {
                var triggerName = trigger.ToString();
                var offset = labels[triggerName];
                triggerMap[trigger] = offset;
            }

            // now with that, we can build an usable contract interface that exposes those triggers as contract calls
            var methods = AccountContract.GetTriggersForABI(triggerMap);
            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", testUser.Address, script, abiBytes)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();
        }

        [TestMethod]
        public void DeployCustomContract()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);
            simulator.blockTimeSkip = TimeSpan.FromSeconds(5);

            var testUser = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10000, DomainSettings.FuelTokenDecimals);
            var stakeAmount = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(Nexus.StakeContractName, "Stake", testUser.Address, stakeAmount).
                    SpendGas(testUser.Address).EndScript());
            simulator.EndBlock();

            string[] scriptString;

            var methodName = "sum";

            scriptString = new string[]
            {
                $"@{methodName}: NOP ",
                $"pop r1",
                $"pop r2",
                $"add r1 r2 r3",
                $"push r3",
                $"@end: ret"
            };

            DebugInfo debugInfo;
            Dictionary<string, int> labels;
            var script = AssemblerUtils.BuildScript(scriptString, "test", out debugInfo, out labels);

            var methods = new[]
            {
                new ContractMethod(methodName , VMType.Number, labels[methodName], new []{ new ContractParameter("a", VMType.Number), new ContractParameter("b", VMType.Number) })
            };
            var abi = new ContractInterface(methods, Enumerable.Empty<ContractEvent>());
            var abiBytes = abi.ToByteArray();

            var contractName = "test";

            // deploy it
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.Minimal,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallInterop("Runtime.DeployContract", testUser.Address, contractName, script, abiBytes)
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();

            // send some funds to contract address
            var contractAddress = SmartContract.GetAddressForName(contractName);
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, contractAddress, nexus.RootChain, DomainSettings.StakingTokenSymbol, stakeAmount);
            simulator.EndBlock();


            // now stake some SOUL on the contract address
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(testUser, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(testUser.Address, Address.Null, 1, 9999)
                    .CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), contractAddress, UnitConversion.ToBigInteger(5, DomainSettings.StakingTokenDecimals))
                    .SpendGas(testUser.Address)
                    .EndScript());
            simulator.EndBlock();
        }

        [TestMethod]
        public void Inflation()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            Block block = null;
            simulator.TimeSkipDays(90, false, x => block = x);

            var inflation = false;
            foreach(var tx in block.TransactionHashes)
            {
                Console.WriteLine("tx: " + tx);
                foreach (var evt in block.GetEventsForTransaction(tx))
                {
                    if (evt.Kind == EventKind.Inflation)
                    {
                        inflation = true;
                    }
                }
            }

            Assert.AreEqual(true, inflation);
        }


        [TestMethod]
        public void PriceOracle()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, 1, 9997)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block = simulator.EndBlock().First();

            foreach (var txHash in block.TransactionHashes)
            {
                var blkResult = block.GetResultForTransaction(txHash);
                var vmObj = VMObject.FromBytes(blkResult);
                Console.WriteLine("price: " + vmObj);
            }

            //TODO finish test
        }

        [TestMethod]
        public void OracleData()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "KCAL")
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block1 = simulator.EndBlock().First();

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "SOUL")
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .SpendGas(owner.Address)
                    .EndScript());
            simulator.GenerateCustomTransaction(owner, ProofOfWork.Moderate,
                () => ScriptUtils.BeginScript()
                    .CallInterop("Oracle.Price", "KCAL")
                    .AllowGas(owner.Address, Address.Null, 1, 9999)
                    .SpendGas(owner.Address)
                    .EndScript());
            var block2 = simulator.EndBlock().First();

            var oData1 = block1.OracleData.Count();
            var oData2 = block2.OracleData.Count();

            Console.WriteLine("odata1: " + oData1);
            Console.WriteLine("odata2: " + oData2);

            Assert.IsTrue(oData1 == oData2);
        }

        [TestMethod]
        public void DuplicateTransferTest()
        {
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            var target = PhantasmaKeys.Generate();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 1);
            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.EndBlock();
            });
        }
    }

}
