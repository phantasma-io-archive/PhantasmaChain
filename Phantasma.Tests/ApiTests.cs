using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;
using Phantasma.Simulator;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using System.Linq;
using System.Numerics;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.VM;
using Phantasma.Core.Log;

namespace Phantasma.Tests
{
    [TestClass]
    public class ApiTests
    {
        public class TestData
        {
            public PhantasmaKeys owner;
            public Nexus nexus;
            public NexusSimulator simulator;
            public NexusAPI api;
        }

        private static readonly string testWIF = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";
        private static readonly string testAddress = "P2K6Sm1bUYGsFkxuzHPhia1AbANZaHBJV54RgtQi5q8oK34";

        private TestData CreateAPI(bool useMempool = false)
        {
            var owner = PhantasmaKeys.FromWIF(testWIF);
            var nexus = new Nexus("simnet", null, null);
            nexus.SetOracleReader(new OracleSimulator(nexus));
            var sim = new NexusSimulator(nexus, owner, 1234);
            var mempool = useMempool? new Mempool(sim.Nexus, 2, 1, System.Text.Encoding.UTF8.GetBytes("TEST"), 0, new DummyLogger()) : null;
            mempool?.SetKeys(owner);

            var api = new NexusAPI(sim.Nexus);
            api.Mempool = mempool;

            var data = new TestData()
            {
                owner = owner,
                simulator = sim,
                nexus = sim.Nexus,
                api = api
            };

            mempool?.StartInThread();

            return data;
        }

        [TestMethod]
        public void TestGetAccountValid()
        {
            var test = CreateAPI();

            var temp = test.api.GetAccount(testAddress);
            var account = (AccountResult)temp;
            Assert.IsTrue(account.address == testAddress);
            Assert.IsTrue(account.name == "genesis");
            Assert.IsTrue(account.balances.Length > 0);
        }

        [TestMethod]
        public void TestGetBlockAndTransaction()
        {
            var test = CreateAPI();

            var genesisHash = test.nexus.GetGenesisHash(test.nexus.RootStorage);

            var genesisBlockHash = genesisHash.ToString();

            var temp = test.api.GetBlockByHash(genesisBlockHash);
            var block = (BlockResult)temp;
            Assert.IsTrue(block.hash == genesisBlockHash);
            Assert.IsTrue(block.height == 1);

            var genesisTxHash = block.txs.FirstOrDefault().hash;

            temp = test.api.GetTransaction(genesisTxHash);
            var tx = (TransactionResult)temp;
            Assert.IsTrue(tx.hash == genesisTxHash);
            Assert.IsTrue(tx.blockHeight == 1);
            Assert.IsTrue(tx.blockHash == genesisBlockHash);
        }

        [TestMethod]
        public void TestMultipleCallsOneRequest()
        {
            var test = CreateAPI();

            var randomKey = PhantasmaKeys.Generate();

            var script = new ScriptBuilder().
                CallContract("account", "LookUpAddress", test.owner.Address).
                CallContract("account", "LookUpAddress", randomKey.Address).
                EndScript();

            var temp = test.api.InvokeRawScript("main", Base16.Encode(script));
            var scriptResult = (ScriptResult)temp;
            Assert.IsTrue(scriptResult.results.Length == 2);

            var names = scriptResult.results.Select(x => Base16.Decode(x)).Select(bytes => Serialization.Unserialize<VMObject>(bytes)).Select(obj => obj.AsString()).ToArray();
            Assert.IsTrue(names.Length == 2);
            Assert.IsTrue(names[0] == "genesis");
            Assert.IsTrue(names[1] == ValidationUtils.ANONYMOUS_NAME);
        }

        [TestMethod]
        public void TestGetAccountInvalidAddress()
        {
            var test = CreateAPI();

            var result = (ErrorResult)test.api.GetAccount("blabla");
            Assert.IsTrue(!string.IsNullOrEmpty(result.error));
        }

        //TODO doesn't really make sense, vm  throws a contract not found, not sure what should be tested here, revisit later
        //[TestMethod]
        //public void TestTransactionError()
        //{
        //    var test = CreateAPI(true);

        //    var contractName = "blabla";
        //    var script = new ScriptBuilder().CallContract(contractName, "bleble", 123).ToScript();

        //    var chainName = DomainSettings.RootChainName;
        //    test.simulator.CurrentTime = Timestamp.Now;
        //    var tx = new Transaction("simnet", chainName, script, test.simulator.CurrentTime + TimeSpan.FromHours(1), "UnitTest");
        //    tx.Sign(PhantasmaKeys.FromWIF(testWIF));
        //    var txBytes = tx.ToByteArray(true);
        //    var temp = test.api.SendRawTransaction(Base16.Encode(txBytes));
        //    var result = (SingleResult)temp;
        //    Assert.IsTrue(result.value != null);
        //    var hash = result.value.ToString();
        //    Assert.IsTrue(hash == tx.Hash.ToString());

        //    var startTime = DateTime.Now;
        //    do
        //    {
        //        var timeDiff = DateTime.Now - startTime;
        //        if (timeDiff.Seconds > 20)
        //        {
        //            throw new Exception("Test timeout");
        //        }

        //        var status = test.api.GetTransaction(hash);
        //        if (status is ErrorResult)
        //        {
        //            var error = (ErrorResult)status;
        //            var msg = error.error.ToLower();
        //            if (msg != "pending")
        //            {
        //                Assert.IsTrue(msg.Contains(contractName));
        //                break;
        //            }
        //        }
        //    } while (true);
        //}

        [TestMethod]
        public void TestGetAccountNFT()
        {
            var test = CreateAPI();

            var chain = test.nexus.RootChain;

            var symbol = "COOL";

            var testUser = PhantasmaKeys.Generate();

            // Create the token CoolToken as an NFT
            test.simulator.BeginBlock();
            test.simulator.GenerateToken(test.owner, symbol, "CoolToken", 0, 0, Domain.TokenFlags.Transferable);
            test.simulator.EndBlock();

            var token = test.simulator.Nexus.GetTokenInfo(test.simulator.Nexus.RootStorage, symbol);
            Assert.IsTrue(test.simulator.Nexus.TokenExists(test.simulator.Nexus.RootStorage, symbol), "Can't find the token symbol");

            var tokenROM = new byte[] { 0x1, 0x3, 0x3, 0x7 };
            var tokenRAM = new byte[] { 0x1, 0x4, 0x4, 0x6 };

            // Mint a new CoolToken 
            var simulator = test.simulator;
            simulator.BeginBlock();
            simulator.MintNonFungibleToken(test.owner, testUser.Address, symbol, tokenROM, tokenRAM, 0);
            simulator.EndBlock();

            // obtain tokenID
            var ownerships = new OwnershipSheet(symbol);
            var ownedTokenList = ownerships.Get(chain.Storage, testUser.Address);
            Assert.IsTrue(ownedTokenList.Count() == 1, "How does the sender not have one now?");
            var tokenID = ownedTokenList.First();

            var account = (AccountResult)test.api.GetAccount(testUser.Address.Text);
            Assert.IsTrue(account.address == testUser.Address.Text);
            Assert.IsTrue(account.name == ValidationUtils.ANONYMOUS_NAME);
            Assert.IsTrue(account.balances.Length == 1);

            var balance = account.balances[0];
            Assert.IsTrue(balance.symbol == symbol);
            Assert.IsTrue(balance.ids.Length == 1);

            var info = (TokenDataResult)test.api.GetNFT(symbol, balance.ids[0], true);
            Assert.IsTrue(info.ID == balance.ids[0]);
            var tokenStr = Base16.Encode(tokenROM);
            Assert.IsTrue(info.rom == tokenStr);
        }

        [TestMethod]
        public void TestGetABIFunction()
        {
            var test = CreateAPI();

            var result = (ContractResult)test.api.GetContract(test.nexus.RootChain.Name, "exchange");

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

            var result = (ContractResult)test.api.GetContract(test.nexus.RootChain.Name, "exchange");

            var methodCount = typeof(ExchangeContract).GetMethods();

            var method = methodCount.FirstOrDefault(x => x.Name == "OpenMarketOrder");

            Assert.IsTrue(method != null);

            var parameters = method.GetParameters();

            Assert.IsTrue(parameters.Length == 6);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(string)) == 2);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(ExchangeOrderSide)) == 1);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(BigInteger)) == 1);
            Assert.IsTrue(parameters.Count(x => x.ParameterType == typeof(Address)) == 2);

            var returnType = method.ReturnType;

            Assert.IsTrue(returnType == typeof(void));
        }
    }
}
