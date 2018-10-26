using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.VM.Utils;
using System;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void TestNexus()
        {
            var owner = KeyPair.Generate();
            var nexus = new Nexus(owner);

            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            var chain = nexus.RootChain;
            var token = nexus.NativeToken;

            var tx = new Transaction(ScriptUtils.TokenTransferScript(chain, token, owner.Address, third.Address, 5), 0, 0);
            tx.Sign(owner);

            /*var block = ProofOfWork.MineBlock(chain, miner.Address, new List<Transaction>() { tx });
            chain.AddBlock(block);*/
        }

        [TestMethod]
        public void TestChainDelete()
        {
            var owner = KeyPair.Generate();
            var simulator = new ChainSimulator(owner, 1234);

            var nexus = simulator.Nexus;
            var accountChain = nexus.FindChainByKind(Blockchain.Contracts.ContractKind.Account);
            var token = nexus.NativeToken;

            Action<string> registerName = (name) =>
            {

            };
        }

    }
}
