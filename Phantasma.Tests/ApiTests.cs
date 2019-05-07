using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Tests
{
    [TestClass]
    public class ApiTests
    {
        public class TestData
        {
            public KeyPair owner;
            public Nexus nexus;
            public ChainSimulator simulator;
            public NexusAPI api;
        }

        private static readonly string testWIF = "Kx9Kr8MwQ9nAJbHEYNAjw5n99B2GpU6HQFf75BGsC3hqB1ZoZm5W";
        private static readonly string testAddress = "P9dKgENhREKbRNsecvVeyPLvrMVJJqDHSWBwFZPyEJjSy";

        private TestData CreateAPI()
        {
            var owner = KeyPair.FromWIF(testWIF);
            var sim = new ChainSimulator(owner, 1234);
            var api = new NexusAPI(sim.Nexus);

            var data = new TestData()
            {
                owner = owner,
                simulator = sim,
                nexus = sim.Nexus,
                api = api
            };

            return data;
        }

        [TestMethod]
        public void TestGetAccountValid()
        {
            var test = CreateAPI();

            var account = (AccountResult)test.api.GetAccount(testAddress);
            Assert.IsTrue(account.address == testAddress);
            Assert.IsTrue(account.name == "genesis");
            Assert.IsTrue(account.balances.Length > 0);
        }

        [TestMethod]
        public void TestGetAccountInvalidAddress()
        {
            var test = CreateAPI();

            var result = (ErrorResult)test.api.GetAccount("blabla");
            Assert.IsTrue(!string.IsNullOrEmpty(result.error));
        }

        [TestMethod]
        public void TestGetAccountNFT()
        {
            var test = CreateAPI();

            var chain = test.nexus.RootChain;

            var nftSymbol = "COOL";

            var testUser = KeyPair.Generate();

            // Create the token CoolToken as an NFT
            test.simulator.BeginBlock();
            test.simulator.GenerateToken(test.owner, nftSymbol, "CoolToken", 0, 0, Blockchain.Tokens.TokenFlags.None);
            test.simulator.EndBlock();

            var token = test.simulator.Nexus.GetTokenInfo(nftSymbol);
            var tokenData = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            Assert.IsTrue(test.simulator.Nexus.TokenExists(nftSymbol), "Can't find the token symbol");

            // Mint a new CoolToken directly on the user
            test.simulator.BeginBlock();
            test.simulator.GenerateNft(test.owner, testUser.Address, chain, nftSymbol, tokenData, new byte[0]);
            test.simulator.EndBlock();

            var account = (AccountResult)test.api.GetAccount(testUser.Address.Text);
            Assert.IsTrue(account.address == testUser.Address.Text);
            Assert.IsTrue(account.name == Blockchain.Contracts.Native.AccountContract.ANONYMOUS);
            Assert.IsTrue(account.balances.Length == 1);

            var balance = account.balances[0];
            Assert.IsTrue(balance.symbol == nftSymbol);
            Assert.IsTrue(balance.ids.Length == 1);

            var info = (TokenDataResult)test.api.GetTokenData(nftSymbol, balance.ids[0]);
            Assert.IsTrue(info.ID == balance.ids[0]);
            var tokenStr = Base16.Encode(tokenData);
            Assert.IsTrue(info.rom == tokenStr);
        }
    }
}
