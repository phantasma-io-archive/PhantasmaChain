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
using Phantasma.Contracts.Native;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;
using Phantasma.Core.Types;

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
            var nexus = new Nexus(null, null, (n) => new OracleSimulator(n));

            Assert.IsTrue(nexus.CreateGenesisBlock("simnet", owner, DateTime.Now));

            Assert.IsTrue(nexus.GenesisHash != Hash.Null);

            var rootChain = nexus.RootChain;

            Assert.IsTrue(rootChain.Address.IsSystem);
            Assert.IsFalse(rootChain.Address.IsNull);

            var symbol = DomainSettings.FuelTokenSymbol;
            Assert.IsTrue(nexus.TokenExists(symbol));
            var token = nexus.GetTokenInfo(symbol);
            Assert.IsTrue(token.MaxSupply > 0);

            var supply = nexus.RootChain.GetTokenSupply(rootChain.Storage, symbol);
            Assert.IsTrue(supply > 0);

            var balance = UnitConversion.ToDecimal(nexus.RootChain.GetTokenBalance(rootChain.Storage, symbol, owner.Address), DomainSettings.FuelTokenDecimals);
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
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.GetChainByName("account");
            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var testUserA = PhantasmaKeys.Generate();
            var testUserB = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            // Send from Genesis address to test user A
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUserA.Address);

            Assert.IsTrue(oldBalance == amount);

            // Send from test user A address to test user B
            amount /= 2;
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUserB.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUserA.Address);
            var gasFee = nexus.RootChain.GetTransactionFee(tx);

            var sum = transferBalance + newBalance + gasFee;
            Assert.IsTrue(sum == oldBalance);
        }

        [TestMethod]
        public void CreateToken()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.GetChainByName("account");
            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(10000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", DomainSettings.PlatformName, Hash.FromString(symbol), tokenSupply, 18, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite | TokenFlags.Divisible);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void CreateNonDivisibleToken()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.GetChainByName("account");
            var symbol = "BLA";

            var tokenSupply = UnitConversion.ToBigInteger(100000000, 18);
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "BlaToken", DomainSettings.PlatformName, Hash.FromString(symbol), tokenSupply, 0, TokenFlags.Transferable | TokenFlags.Fungible | TokenFlags.Finite);
            simulator.MintTokens(owner, owner.Address, symbol, tokenSupply);
            simulator.EndBlock();

            var token = nexus.GetTokenInfo(symbol);

            var testUser = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(2, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, owner.Address);

            Assert.IsTrue(oldBalance > amount);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void AccountRegister()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
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

            var token = nexus.GetTokenInfo(symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUser.Address);
            Assert.IsTrue(balance == amount);

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
            Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
            Assert.IsFalse(registerName(testUser, targetName + "!"));
            Assert.IsTrue(registerName(testUser, targetName));

            var currentName = nexus.LookUpAddressName(nexus.RootStorage, testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(nexus.RootStorage, targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            Assert.IsFalse(registerName(testUser, "other"));
        }

        [TestMethod]
        public void SimpleTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;

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

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }

        [TestMethod]
        public void CosmicSwapSimple()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;

            var testUserA = PhantasmaKeys.Generate();

            var fuelAmount = UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals);
            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            var blockA = simulator.EndBlock().FirstOrDefault();

            Assert.IsTrue(blockA != null);
            Assert.IsFalse(blockA.OracleData.Any());

            var originalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, testUserA.Address);

            var swapAmount = UnitConversion.ToBigInteger(0.01m, DomainSettings.StakingTokenDecimals);
            simulator.BeginBlock();
            simulator.GenerateSwap(testUserA, nexus.RootChain, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, swapAmount);
            var blockB = simulator.EndBlock().FirstOrDefault();

            Assert.IsTrue(blockB != null);
            Assert.IsFalse(blockB.OracleData.Any());

            var bytes = blockB.ToByteArray();
            var otherBlock = Block.Unserialize(bytes);
            Assert.IsTrue(otherBlock.Hash == blockB.Hash);

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, testUserA.Address);
            Assert.IsTrue(finalBalance > originalBalance);
        }

        [TestMethod]
        public void ChainSwapIn()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;

            var neoKeys = Neo.Core.NeoKeys.Generate();

            var limit = 800;

            // 1 - at this point a real NEO transaction would be done to the NEO address obtained from getPlatforms in the API
            // here we just use a random hardcoded hash and a fake oracle to simulate it
            var swapSymbol = "GAS";
            var neoTxHash = OracleSimulator.SimulateExternalTransaction("neo", Pay.Chains.NeoWallet.NeoID, neoKeys.PublicKey, neoKeys.Address, swapSymbol, 2);

            var tokenInfo = nexus.GetTokenInfo(swapSymbol);

            // 2 - transcode the neo address and settle the Neo transaction on Phantasma
            var transcodedAddress = Address.FromKey(neoKeys);

            var testUser = PhantasmaKeys.Generate();

            var platformName = Pay.Chains.NeoWallet.NeoPlatform;
            var platformChain = Pay.Chains.NeoWallet.NeoPlatform;

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(neoKeys, ProofOfWork.None, () =>
            {
                return new ScriptBuilder()
                .CallContract("interop", "SettleTransaction", transcodedAddress, platformName, platformChain, neoTxHash)
                .CallContract("swap", "SwapFee", transcodedAddress, swapSymbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
                .TransferBalance(swapSymbol, transcodedAddress, testUser.Address)
                .AllowGas(transcodedAddress, Address.Null, simulator.MinimumFee, limit)
                .SpendGas(transcodedAddress).EndScript();
            });
            simulator.EndBlock();

            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapSymbol, transcodedAddress);
            Assert.IsTrue(balance == 0);

            balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, swapSymbol, testUser.Address);
            Assert.IsTrue(balance > 0);

            var settleHash = (Hash)nexus.RootChain.InvokeContract(nexus.RootStorage, "interop", nameof(InteropContract.GetSettlement), "neo", neoTxHash).ToObject();
            Assert.IsTrue(settleHash == tx.Hash);
        }

        [TestMethod]
        public void ChainSwapOut()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;

            var testUser = PhantasmaKeys.Generate();

            var limit = 800;

            // 0 - just send some assets to the 
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals));
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(10, DomainSettings.FuelTokenDecimals));
            simulator.EndBlock();


            // TODO
        }

        [TestMethod]
        public void QuoteConversions()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var context = new StorageChangeSetContext(new MemoryStorageContext());
            var runtime = new RuntimeVM(new byte[0], nexus.RootChain, Timestamp.Now, null, context, new OracleSimulator(nexus), true);

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
            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

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

            Assert.IsTrue(targetRate == 5);
        }

        [TestMethod]
        public void TransferToAccountName()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
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
            var token = nexus.GetTokenInfo(symbol);
            var amount = UnitConversion.ToBigInteger(10, token.Decimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            Assert.IsTrue(registerName(testUser, targetName));

            // Send from Genesis address to test user
            var transferAmount = 1;

            var initialFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUser.Address);

            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(owner, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(owner.Address, Address.Null, 1, 9999)
                    .TransferTokens(token.Symbol, owner.Address, targetName, transferAmount)
                    .SpendGas(owner.Address).EndScript());
            simulator.EndBlock();

            var finalFuelBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, testUser.Address);

            Assert.IsTrue(finalFuelBalance - initialFuelBalance == transferAmount);
        }

        [TestMethod]
        public void SideChainTransferDifferentAccounts()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.GetChainByName("sale");

            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(symbol);
            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, receiver.Address);
            Assert.IsTrue(balance == sideAmount - feeB);

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA + crossFee);

            balance = sourceChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
            Assert.IsTrue(balance == leftoverAmount);
        }

        [TestMethod]
        public void SideChainTransferSameAccount()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;


            var symbol = DomainSettings.FuelTokenSymbol;

            var sender = PhantasmaKeys.Generate();

            var token = nexus.GetTokenInfo(symbol);
            var originalAmount = UnitConversion.ToBigInteger(1, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.GenerateChain(owner, "main", "test");
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName("test");

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
            //Assert.IsTrue(balance == sideAmount - feeB); TODO CHECK THIS BERNARDO

            var feeA = sourceChain.GetTransactionFee(txA);
            var leftoverAmount = originalAmount - (sideAmount + feeA);

            balance = sourceChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var sideChain = nexus.GetChainByName("sale");
            Assert.IsTrue(sideChain != null);

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var originalAmount = UnitConversion.ToBigInteger(10, token.Decimals);
            var sideAmount = originalAmount / 2;

            Assert.IsTrue(sideAmount > 0);

            var newChainName = "testing";

            // Send from Genesis address to "sender" user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, originalAmount);
            simulator.GenerateChain(owner, sideChain.Name, newChainName);
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName(newChainName);

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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
            sideAmount = sideAmount - txCostB;

            balance = sideChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", DomainSettings.PlatformName, Hash.FromString(symbol), 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken to test address
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM);
            simulator.EndBlock();

            // obtain tokenID
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            // verify nft presence on the user post-mint
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");

            //verify that the present nft is the same we actually tried to create
            var tokenId = ownedTokenList.ElementAt(0);
            var nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) && nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            var currentSupply = chain.GetTokenSupply(chain.Storage, symbol);
            Assert.IsTrue(currentSupply == 1, "why supply did not increase?");
        }


        [TestMethod]
        public void NftBurn()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var chain = nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", DomainSettings.PlatformName, Hash.FromString(symbol), 0, 0, TokenFlags.Burnable);
            simulator.EndBlock();

            // Send some SOUL to the test user (required for gas used in "burn" transaction)
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, chain, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.FuelTokenDecimals));
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the user pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken to test address
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, testUser.Address, symbol, tokenROM, tokenRAM);
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
            var nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");

            // burn the token
            simulator.BeginBlock();
            simulator.GenerateNftBurn(testUser, chain, symbol, tokenId);
            simulator.EndBlock();

            //verify the user no longer has the token
            ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the user still have it post-burn?");
        }

        [TestMethod]
        public void NftTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

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
            simulator.GenerateToken(owner, symbol, nftName, DomainSettings.PlatformName, Hash.FromString(symbol), 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM);
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
            var nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
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
            nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void SidechainNftTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

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
            simulator.GenerateChain(owner, "main", "test");
            simulator.EndBlock();

            var targetChain = nexus.GetChainByName("test");

            // Create the token CoolToken as an NFT
            simulator.BeginBlock();
            simulator.GenerateToken(owner, symbol, "CoolToken", DomainSettings.PlatformName, Hash.FromString(symbol), 0, 0, TokenFlags.Transferable);
            simulator.EndBlock();

            var token = simulator.Nexus.GetTokenInfo(symbol);
            Assert.IsTrue(nexus.TokenExists(symbol), "Can't find the token symbol");

            // verify nft presence on the sender pre-mint
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(sourceChain.Storage, sender.Address);
            Assert.IsTrue(!ownedTokenList.Any(), "How does the sender already have a CoolToken?");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(owner, sender.Address, symbol, tokenROM, tokenRAM);
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
            var nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
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
            nft = nexus.RootChain.ReadToken(nexus.RootStorage, symbol, tokenId);
            Assert.IsTrue(nft.ROM.SequenceEqual(tokenROM) || nft.RAM.SequenceEqual(tokenRAM),
                "And why is this NFT different than expected? Not the same data");
        }

        [TestMethod]
        public void NoGasSameChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.GetChainByName("account");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(1, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            var oldBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
            Assert.IsTrue(oldBalance == amount);

            var gasFee = nexus.RootChain.GetTransactionFee(tx);
            Assert.IsTrue(gasFee > 0);

            amount /= 2;
            simulator.BeginBlock();
            simulator.GenerateTransfer(sender, receiver.Address, nexus.RootChain, symbol, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, receiver.Address);

            var newBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);

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
            transferBalance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, receiver.Address);
            Assert.IsTrue(transferBalance == 0, "Transaction failed completely as expected");
        }

        [TestMethod]
        public void NoGasTestSideChainTransfer()
        {
            var owner = PhantasmaKeys.Generate();

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            var sourceChain = nexus.RootChain;
            var targetChain = nexus.GetChainByName("sale");

            var symbol = DomainSettings.FuelTokenSymbol;
            var token = nexus.GetTokenInfo(symbol);

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
            var balance = nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, sender.Address);
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
            balance = targetChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, receiver.Address);
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

            var simulator = new NexusSimulator(owner, 1234);
            var nexus = simulator.Nexus;

            Assert.IsTrue(address.Text == nexus.GenesisAddress.Text);
            Assert.IsTrue(address.ToByteArray().SequenceEqual(nexus.GenesisAddress.ToByteArray()));
            Assert.IsTrue(address == nexus.GenesisAddress);
        }

        [TestMethod]
        public void ChainTransferExploit()
        {
            var owner = PhantasmaKeys.FromWIF("L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25");
            var simulator = new NexusSimulator(owner, 1234);

            var user = PhantasmaKeys.Generate();

            var symbol = DomainSettings.StakingTokenSymbol;

            var chainAddressStr = Base16.Encode(simulator.Nexus.RootChainAddress.ToByteArray());
            var userAddressStr = Base16.Encode(user.Address.ToByteArray());

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.GenerateTransfer(owner, user.Address, simulator.Nexus.RootChain, DomainSettings.StakingTokenSymbol, 100000000);
            simulator.EndBlock();

            var chainAddress = simulator.Nexus.RootChainAddress;
            simulator.BeginBlock();
            var tx = simulator.GenerateTransfer(owner, chainAddress, simulator.Nexus.RootChain, symbol, 100000000);
            var block = simulator.EndBlock().First();

            var evts = block.GetEventsForTransaction(tx.Hash);
            Assert.IsTrue(evts.Any(x => x.Kind == EventKind.TokenReceive && x.Address == chainAddress));

            var initialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, chainAddress);
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

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, simulator.Nexus.RootChainAddress);
            Assert.IsTrue(initialBalance == finalBalance);
        }

        [TestMethod]
        public void TransactionFees()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);
            simulator.MinimumFee = 100000;

            var nexus = simulator.Nexus;

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

            // here we skip the first one as it is always the OpenBlock tx
            var hash = block.TransactionHashes.Skip(1).First();

            var feeValue = nexus.RootChain.GetTransactionFee(hash);
            var feeAmount = UnitConversion.ToDecimal(feeValue, DomainSettings.FuelTokenDecimals);
            Assert.IsTrue(feeAmount >= 0.0009m);

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }

        [TestMethod]
        public void ValidatorSwitch()
        {
            var owner = PhantasmaKeys.Generate();
            var simulator = new NexusSimulator(owner, 1234);
            simulator.blockTimeSkip = TimeSpan.FromSeconds(10);

            var nexus = simulator.Nexus;

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

            var transferAmount = UnitConversion.ToBigInteger(10, DomainSettings.StakingTokenDecimals);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, fuelAmount);
            simulator.GenerateTransfer(owner, testUserA.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            simulator.EndBlock();

            // here we skip to a time where its supposed to be the turn of the second validator
            simulator.CurrentTime = (DateTime)simulator.Nexus.GenesisTime + TimeSpan.FromSeconds(180);

            // Send from user A to user B
            // NOTE this block is baked by the second validator
            simulator.BeginBlock(secondValidator);
            simulator.GenerateTransfer(testUserA, testUserB.Address, nexus.RootChain, DomainSettings.StakingTokenSymbol, transferAmount);
            var lastBlock = simulator.EndBlock().First();

            var firstTxHash = lastBlock.TransactionHashes.First();
            events = lastBlock.GetEventsForTransaction(firstTxHash).ToArray();
            Assert.IsTrue(events.Length > 0);
            Assert.IsTrue(events.Any(x => x.Kind == EventKind.ValidatorSwitch));

            var finalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.StakingTokenSymbol, testUserB.Address);
            Assert.IsTrue(finalBalance == transferAmount);
        }
    }

}
