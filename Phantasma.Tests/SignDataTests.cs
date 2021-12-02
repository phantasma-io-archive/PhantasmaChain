using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Encryption;
using Phantasma.Numerics;
using Phantasma.Simulator;
using System;
using System.Collections.Generic;
using System.Text;
using static Phantasma.Simulator.LinkSimulator;

namespace Phantasma.Tests
{
    [TestClass]
    public class SignDataTests
    {
        [TestMethod]
        public void SignWithPhantasma()
        {
            // setup Nexus
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // Setup Users
            var testUser1 = PhantasmaKeys.Generate();
            var account1 = new MyAccount(testUser1, PlatformKind.Phantasma);
            LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

            var testUser2 = PhantasmaKeys.Generate();
            var account2 = new MyAccount(testUser2, PlatformKind.Ethereum);
            LinkSimulator link2 = new LinkSimulator(nexus, "Ac2", account1);

            var signature = "Ed25519";
            var platform = "phantasma";

            // Encode Data
            var rawData = "SignWithPhantasma";
            var encodedData = Base16.Encode(Encoding.ASCII.GetBytes(rawData));

            Assert.IsTrue(rawData.Equals(Encoding.ASCII.GetString(Base16.Decode(encodedData))));

            // Make a sign Data Call
            link1.forceSignData(platform, SignatureKind.Ed25519, Encoding.ASCII.GetBytes(encodedData), 0, (signed, random, error) =>
            {
                Assert.IsTrue(random != encodedData);
                Assert.IsTrue(signed != encodedData);
                Console.WriteLine($"Error:{error}");
                var result = EncryptionUtils.ValidateSignedData(testUser1.Address.Text, signed, random, encodedData);
                Assert.IsTrue(result, "Not Valid");
            });

            // Make a sign Data Call
            link2.forceSignData(platform, SignatureKind.Ed25519, Encoding.ASCII.GetBytes(encodedData), 1, (signed, random, error) =>
            {
                Assert.IsTrue(random != encodedData);
                Assert.IsTrue(signed != encodedData);
                Console.WriteLine($"Error:{error}");
                var result = EncryptionUtils.ValidateSignedData(testUser2.Address.Text, signed, random, encodedData);
                Assert.IsFalse(result, "Valid, but shouldn't be.");
            });
        }

        [TestMethod]
        public void SignWithEthereum()
        {
            // setup Nexus
            var owner = PhantasmaKeys.Generate();
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var simulator = new NexusSimulator(nexus, owner, 1234);

            // Setup Users
            var testUser1 = PhantasmaKeys.Generate();
            var account1 = new MyAccount(testUser1, PlatformKind.Ethereum);
            LinkSimulator link1 = new LinkSimulator(nexus, "Ac1", account1);

            var testUser2 = PhantasmaKeys.Generate();
            var account2 = new MyAccount(testUser2, PlatformKind.Phantasma);
            LinkSimulator link2 = new LinkSimulator(nexus, "Ac2", account1);

            var signature = "Ed25519";
            var platform = "ethereum";

            // Encode Data
            var rawData = "SignWithEthereum";
            var encodedData = Base16.Encode(Encoding.ASCII.GetBytes(rawData));

            Assert.IsTrue(rawData.Equals(Encoding.ASCII.GetString(Base16.Decode(encodedData))));

            // Make a sign Data Call
            link1.forceSignData(platform, SignatureKind.Ed25519, Encoding.ASCII.GetBytes(encodedData), 0, (signed, random, error) =>
            {
                Assert.IsTrue(random != encodedData);
                Assert.IsTrue(signed != encodedData);
                Console.WriteLine($"Error:{error}");
                var result = EncryptionUtils.ValidateSignedData(testUser1.Address.Text, signed, random, encodedData);
                Assert.IsTrue(result, "Not Valid");
            });

            // Make a sign Data Call
            link2.forceSignData(platform, SignatureKind.Ed25519, Encoding.ASCII.GetBytes(encodedData), 1, (signed, random, error) =>
            {
                Assert.IsTrue(random != encodedData);
                Assert.IsTrue(signed != encodedData);
                Console.WriteLine($"Error:{error}");
                var result = EncryptionUtils.ValidateSignedData(testUser2.Address.Text, signed, random, encodedData);
                Assert.IsFalse(result, "Valid, but shouldn't be.");
            });
        }

        [TestMethod]
        public void SignWithBNB()
        {
            // TODO
        }

        [TestMethod]
        public void SignWithNEO()
        {
           // TODO
        }
    }
}
