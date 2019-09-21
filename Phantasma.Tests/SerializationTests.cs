using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;
using System.Collections.Generic;

using Phantasma.Cryptography;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.VM.Utils;
using Phantasma.Numerics;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Storage;

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
                AllowGas(keysA.Address, Address.Null, 1, 9999).
                TransferTokens(Nexus.FuelTokenSymbol, keysA.Address, keysB.Address, UnitConversion.ToBigInteger(25, Nexus.FuelTokenDecimals)).
                SpendGas(keysA.Address).
                EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now);

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
            var symbol = Nexus.FuelTokenSymbol;

            int count = 5;
            var amounts = new BigInteger[count];

            for (int i = 0; i<count; i++)
            {
                var keysB = KeyPair.Generate();
                amounts[i] = UnitConversion.ToBigInteger(20 + (i+1), Nexus.FuelTokenDecimals);

                var script = ScriptUtils.BeginScript().
                    AllowGas(keysA.Address, Address.Null, 1, 9999).
                    TransferTokens(symbol, keysA.Address, keysB.Address, amounts[i]).
                    SpendGas(keysA.Address).
                    EndScript();

                var tx = new Transaction("simnet", "main", script, Timestamp.Now - TimeSpan.FromMinutes(i));

                txs.Add(tx);
            }

            var chainKeys = KeyPair.Generate();
            var hashes = txs.Select(x => x.Hash);
            var block = new Block(1, chainKeys.Address, Timestamp.Now, hashes, Hash.Null);

            int index = 0;
            foreach (var hash in hashes)
            {
                var data = new TokenEventData() { symbol = symbol, chainAddress = keysA.Address, value = amounts[index] };
                var dataBytes = Serialization.Serialize(data);
                block.Notify(hash, new Event(EventKind.TokenSend, keysA.Address, "test", dataBytes));
                index++;
            }

            var bytes = block.ToByteArray();

            Assert.IsTrue(bytes != null);

            var block2 = Block.Unserialize(bytes);
            Assert.IsTrue(block2 != null);

            Assert.IsTrue(block.Hash == block2.Hash);
        }

        [TestMethod]
        public void TransactionMining()
        {
            var keysA = KeyPair.Generate();
            var keysB = KeyPair.Generate();

            var script = ScriptUtils.BeginScript().
                AllowGas(keysA.Address, Address.Null, 1, 9999).
                TransferTokens(Nexus.FuelTokenSymbol, keysA.Address, keysB.Address, UnitConversion.ToBigInteger(25, Nexus.FuelTokenDecimals)).
                SpendGas(keysA.Address).
                EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now);
            var expectedDiff = 14;
            tx.Mine(expectedDiff);

            var diff = tx.Hash.GetDifficulty();
            Assert.IsTrue(diff >= expectedDiff);
        }

    }
}
