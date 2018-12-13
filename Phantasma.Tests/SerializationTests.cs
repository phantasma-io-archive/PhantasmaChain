using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Text;

using Phantasma.Cryptography;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Cryptography.Ring;
using Phantasma.Cryptography.ECC;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;
using System.Collections.Generic;

namespace Phantasma.Tests
{
    [TestClass]
    public class SerializationTests
    {
        [TestMethod]
        public void TransactionSerialization()
        {
            var keysA = KeyPair.Generate();
            var keysB = KeyPair.Generate();

            var script = ScriptUtils.BeginScript().
                AllowGas(keysA.Address, 1, 9999).
                TransferTokens(Nexus.NativeTokenSymbol, keysA.Address, keysB.Address, TokenUtils.ToBigInteger(25, Nexus.NativeTokenDecimals)).
                SpendGas(keysA.Address).
                EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now, 3421);

            var bytesUnsigned = tx.ToByteArray(false);
            Assert.IsTrue(bytesUnsigned != null);

            tx.Sign(keysA);
            var bytesSigned = tx.ToByteArray(true);

            Assert.IsTrue(bytesSigned != null);
            Assert.IsTrue(bytesSigned.Length != bytesUnsigned.Length);

            var tx2 = Transaction.Unserialize(bytesSigned);
            Assert.IsTrue(tx2 != null);

            Assert.IsTrue(tx.Hash == tx2.Hash);
        }

        [TestMethod]
        public void BlockSerialization()
        {
            var keysA = KeyPair.Generate();

            var txs = new List<Transaction>();

            for (int i = 1; i<=5; i++)
            {
                var keysB = KeyPair.Generate();

                var script = ScriptUtils.BeginScript().
                    AllowGas(keysA.Address, 1, 9999).
                    TransferTokens(Nexus.NativeTokenSymbol, keysA.Address, keysB.Address, TokenUtils.ToBigInteger(20 +i , Nexus.NativeTokenDecimals)).
                    SpendGas(keysA.Address).
                    EndScript();

                var tx = new Transaction("simnet", "main", script, Timestamp.Now - TimeSpan.FromMinutes(i), 3421);

                txs.Add(tx);
            }

            var chainKeys = KeyPair.Generate();
            var minerKeys = KeyPair.Generate();
            var hashes = txs.Select(x => x.Hash);
            var block = new Block(1, chainKeys.Address, minerKeys.Address, Timestamp.Now, hashes, Hash.Null);

            var bytes = block.ToByteArray();

            Assert.IsTrue(bytes != null);

            var block2 = Block.Unserialize(bytes);
            Assert.IsTrue(block2 != null);

            Assert.IsTrue(block.Hash == block2.Hash);
        }
    }
}
