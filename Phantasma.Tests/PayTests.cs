using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Pay;

namespace Phantasma.Tests
{
    [TestClass]
    public class PayTests
    {
        [TestMethod]
        public void TestEthereumWallet()
        {
            var keys = new KeyPair(Base16.Decode("a95bd75a7b3b1c0a2a14595e8065a95cb06417f6aaedcc3bc45fda52900ab9e8"));
            var wallet = new WalletManager(keys);
            var address = wallet.GetAddress(WalletKind.Ethereum);
            Assert.IsTrue(address.Equals("0xe57a6c074d1db5ed7c98228df71ce5fa35b6bc72", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestEOSWallet()
        {
            var wif = "5KA2AqEoo7jqepqeEqK2FjjjgG5nxQN6vfuiSZqgJM79ej6eo4Q";
            byte[] data = wif.Base58CheckDecode();

            byte[] privateKey = new byte[32];
            ByteArrayUtils.CopyBytes(data, 1, privateKey, 0, privateKey.Length);

            var keys = new KeyPair(privateKey);
            var wallet = new WalletManager(keys);
            var address = wallet.GetAddress(WalletKind.EOS);
            Assert.IsTrue(address.Equals("EOS8dBKtG9fbhC1wi1SscL32iFRsSi4PsZDT2EHJcYXwV5dAMiBcK", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestNeoWallet()
        {
            var keys = KeyPair.FromWIF("L1nuBmNJ2HvLat5xyvpqmHpmXNe6rGGdAzGJgLjDLECaTCVgqjdx");
            var wallet = new WalletManager(keys);
            var address = wallet.GetAddress(WalletKind.Neo);
            Assert.IsTrue(address.Equals("AU2eYJkpZ2nG81RyqnzF5UL2qjdkpPEJqN", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TestBitcoinWallet()
        {
            var keys = new KeyPair(Base16.Decode("60cf347dbc59d31c1358c8e5cf5e45b822ab85b79cb32a9f3d98184779a9efc2"));
            var wallet = new WalletManager(keys);
            var address = wallet.GetAddress(WalletKind.Bitcoin);
            Assert.IsTrue(address.Equals("17JsmEygbbEUEpvt4PFtYaTeSqfb9ki1F1", StringComparison.OrdinalIgnoreCase));
        }
    }
}
