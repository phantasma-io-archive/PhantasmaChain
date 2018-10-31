using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System;
using System.Linq;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void TestDecimals()
        {
            var places = 8;
            decimal d = 93000000;
            BigInteger n = 9300000000000000;

            Assert.IsTrue(n == TokenUtils.ToBigInteger(TokenUtils.ToDecimal(n, places), places));
            Assert.IsTrue(d == TokenUtils.ToDecimal(TokenUtils.ToBigInteger(d, places), places));

            Assert.IsTrue(d == TokenUtils.ToDecimal(n, places));
            Assert.IsTrue(n == TokenUtils.ToBigInteger(d, places));
        }

        [TestMethod]
        public void TestNexus()
        {
            var owner = KeyPair.Generate();
            var nexus = new Nexus(owner);

            var rootChain = nexus.RootChain;
            var token = nexus.NativeToken;

            Assert.IsTrue(token != null);
            Assert.IsTrue(token.CurrentSupply > 0);
            Assert.IsTrue(token.MaxSupply > 0);

            Assert.IsTrue(rootChain != null);
            Assert.IsTrue(rootChain.BlockHeight > 0);
            Assert.IsTrue(rootChain.ChildChains.Any());

            var txCount = nexus.GetTotalTransactionCount();
            Assert.IsTrue(txCount > 0);

            /*
            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            var tx = new Transaction(ScriptUtils.TokenTransferScript(chain, token, owner.Address, third.Address, 5), 0, 0);
            tx.Sign(owner);
            */
            /*var block = ProofOfWork.MineBlock(chain, miner.Address, new List<Transaction>() { tx });
            chain.AddBlock(block);*/
        }

        [TestMethod]
        public void TestTokenTransfer()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByKind(Blockchain.Contracts.ContractKind.Account);
            var token = nexus.NativeToken;

            var testUser = KeyPair.Generate();

            var amount = TokenUtils.ToBigInteger(400, token.Decimals);

            var oldBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var transferBalance = nexus.RootChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(transferBalance == amount);

            var newBalance = nexus.RootChain.GetTokenBalance(token, owner.Address);

            Assert.IsTrue(transferBalance + newBalance == oldBalance);
        }

        [TestMethod]
        public void TestAccountRegister()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByKind(Blockchain.Contracts.ContractKind.Account);
            var token = nexus.NativeToken;

            Func<KeyPair, string, bool> registerName = (keypair, name) =>
            {
                bool result = true;

                try
                {
                    simulator.BeginBlock();
                    var tx = simulator.GenerateAccountRegistration(keypair, name);
                    result = simulator.EndBlock();

                    if (result)
                    {
                        Assert.IsTrue(tx != null);
                        Assert.IsTrue(tx.Events.Any(x => x.Kind == Blockchain.Contracts.EventKind.AddressRegister));
                    }
                }
                catch (Exception)
                {
                    result = false;
                }

                return result;
            };

            var testUser = KeyPair.Generate();

            var amount = TokenUtils.ToBigInteger(10, token.Decimals);

            // Send from Genesis address to test user
            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, token, amount);
            simulator.EndBlock();

            // verify test user balance
            var balance = nexus.RootChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(balance == amount);

            // do a side chain send using test user balance from root to account chain
            simulator.BeginBlock();
            var txA = simulator.GenerateSideChainSend(testUser, token, nexus.RootChain, accountChain, TokenUtils.ToBigInteger(10, token.Decimals));
            simulator.EndBlock();

            Assert.IsFalse(registerName(testUser, "hello"));

            // finish the chain transfer
            simulator.BeginBlock();
            simulator.GenerateSideChainSettlement(nexus.RootChain, accountChain, txA.Block.Hash);
            Assert.IsTrue(simulator.EndBlock());

            // verify balances
            balance = accountChain.GetTokenBalance(token, testUser.Address);
            Assert.IsTrue(balance > 0);

            var targetName = "hello";
            Assert.IsTrue(targetName == targetName.ToLower());

            Assert.IsFalse(registerName(testUser, targetName.Substring(3)));
            Assert.IsFalse(registerName(testUser, targetName.ToUpper()));
            Assert.IsFalse(registerName(testUser, targetName+"!"));
            Assert.IsTrue(registerName(testUser, targetName));

            var currentName = nexus.LookUpAddress(testUser.Address);
            Assert.IsTrue(currentName == targetName);

            var someAddress = nexus.LookUpName(targetName);
            Assert.IsTrue(someAddress == testUser.Address);

            Assert.IsFalse(registerName(testUser, "other"));
        }

    }
}
