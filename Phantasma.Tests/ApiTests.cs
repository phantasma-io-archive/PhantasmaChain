using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using System;
using System.Linq;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Core.Types;

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

        private TestData CreateAPI(bool useMempool = false)
        {
            var owner = KeyPair.FromWIF(testWIF);
            var sim = new ChainSimulator(owner, 1234);
            var mempool = useMempool? new Mempool(owner, sim.Nexus, 2) : null;
            var api = new NexusAPI(sim.Nexus, mempool);

            var data = new TestData()
            {
                owner = owner,
                simulator = sim,
                nexus = sim.Nexus,
                api = api
            };

            mempool?.Start();

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
        public void TestTransactionError()
        {
            var test = CreateAPI(true);

            var contractName = "blabla";
            var script = new ScriptBuilder().CallContract(contractName, "bleble", 123).ToScript();

            var chainName = Nexus.RootChainName;
            test.simulator.CurrentTime = Timestamp.Now;
            var tx = new Transaction("simnet", chainName, script, test.simulator.CurrentTime + TimeSpan.FromHours(1));
            var txBytes = tx.ToByteArray(true);
            var temp = test.api.SendRawTransaction(Base16.Encode(txBytes));
            var result = (SingleResult)temp;
            Assert.IsTrue(result.value != null);
            var hash = result.value.ToString();
            Assert.IsTrue(hash == tx.Hash.ToString());

            var startTime = DateTime.Now;
            do
            {
                var timeDiff = DateTime.Now - startTime;
                if (timeDiff.Seconds > 20)
                {
                    throw new Exception("Test timeout");
                }

                var status = test.api.GetTransaction(hash);
                if (status is ErrorResult)
                {
                    var error = (ErrorResult)status;
                    var msg = error.error.ToLower();
                    if (msg != "pending")
                    {
                        Assert.IsTrue(msg.Contains(contractName));
                        break;
                    }
                }
            } while (true);
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
            test.simulator.MintNonFungibleToken(test.owner, testUser.Address, nftSymbol, tokenData, new byte[0], 0);
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

        [TestMethod]
        public void TestGetABIFunction()
        {
            var test = CreateAPI();

            var result = (ABIContractResult)test.api.GetABI(test.nexus.RootChain.Name, "exchange");

            var methodCount = typeof(ExchangeContract).GetMethods();

            var method = methodCount.FirstOrDefault(x => x.Name == "GetOrderBook");

            Assert.IsTrue(method != null);

            var parameters = method.GetParameters();

            Assert.IsTrue(parameters.Length == 3);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(string)) == 2);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(ExchangeOrderSide)) == 1);

            var returnType = method.ReturnType;

            Assert.IsTrue(returnType == typeof(ExchangeOrder[]));
        }


        [TestMethod]
        public void TestGetABIMethod()
        {
            var test = CreateAPI();

            var result = (ABIContractResult)test.api.GetABI(test.nexus.RootChain.Name, "exchange");

            var methodCount = typeof(ExchangeContract).GetMethods();

            var method = methodCount.FirstOrDefault(x => x.Name == "OpenMarketOrder");

            Assert.IsTrue(method != null);

            var parameters = method.GetParameters();

            Assert.IsTrue(parameters.Length == 5);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(string)) == 2);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(ExchangeOrderSide)) == 1);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(BigInteger)) == 1);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(Address)) == 1);

            var returnType = method.ReturnType;

            Assert.IsTrue(returnType == typeof(ExchangeOrder[]));
        }
    }
}
