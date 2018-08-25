using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

using Phantasma.Blockchain;
using Phantasma.Blockchain.Consensus;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    [TestClass]
    public class ChainTests
    {
        [TestMethod]
        public void TestChain()
        {
            var owner = KeyPair.Generate();
            var chain = new Chain(owner);

            var miner = KeyPair.Generate();
            var third = KeyPair.Generate();

            var nativeToken = Chain.GetNativeContract(NativeContractKind.Token);
            var tx = new Transaction(ScriptUtils.TokenTransferScript(nativeToken.Address, owner.Address, third.Address, 5), 0, 0);
            tx.Sign(owner);

            /*var block = ProofOfWork.MineBlock(chain, miner.Address, new List<Transaction>() { tx });
            chain.AddBlock(block);*/
        }
    }
}
