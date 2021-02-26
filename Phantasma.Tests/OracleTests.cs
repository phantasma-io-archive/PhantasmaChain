
using System;
using System.Linq;
using System.Numerics;

using Phantasma.Blockchain;
using Phantasma.Storage.Context;
using Phantasma.Cryptography;
using Phantasma.Simulator;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class OracleTests
    {
        [TestMethod]
        public void OracleTestNoData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            //for (var i = 0; i < 65536; i++)
            for (var i = 0; i < 100; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neoEmpty", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain, "SOUL", 100);
            var block = simulator.EndBlock().First();

            Assert.IsTrue(block.OracleData.Count() == 0);
            Console.WriteLine("block oracle data: " + block.OracleData.Count());

        }

        [TestMethod]
        public void OracleTestWithData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            //for (var i = 0; i < 65536; i++)
            for (var i = 0; i < 100; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain, "SOUL", 100);
            var block = simulator.EndBlock().First();

            Console.WriteLine("block oracle data: " + block.OracleData.Count());
            Assert.IsTrue(block.OracleData.Count() == 100);
        }

        [TestMethod]
        public void OracleTestWithTooMuchData()
        {
            var owner = PhantasmaKeys.Generate();
            var wallet = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            for (int i = 0; i < DomainSettings.MaxOracleEntriesPerBlock + 1; i++)
            {
                var url = DomainExtensions.GetOracleBlockURL("neo", "neo", new BigInteger(i));
                var iBlock = nexus.GetOracleReader().Read<InteropBlock>(DateTime.Now, url);
            }

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                simulator.GenerateTransfer(owner, wallet.Address, nexus.RootChain, "SOUL", 100);
                simulator.EndBlock().First();
            });
        }
    }

}
