using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Utils;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Tests
{
    [TestClass]
    public class RelayTests
    {
        private static readonly string testWIF = "Kx9Kr8MwQ9nAJbHEYNAjw5n99B2GpU6HQFf75BGsC3hqB1ZoZm5W";
        private static readonly string nodeWIF = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";

        private ApiTests.TestData CreateAPI(bool useMempool = false)
        {
            var owner = KeyPair.FromWIF(testWIF);
            var sim = new ChainSimulator(owner, 1234);
            var mempool = useMempool ? new Mempool(owner, sim.Nexus, 2) : null;
            var node = useMempool ? new Node(sim.Nexus, mempool, owner, 7073, new List<string>() { "192.168.0.1:7073" }, null) : null;
            var api = useMempool ? new NexusAPI(sim.Nexus, mempool, node) : null;

            var data = new ApiTests.TestData()
            {
                owner = owner,
                simulator = sim,
                nexus = sim.Nexus,
                api = api
            };

            mempool?.Start();

            return data;
        }

        private void TopUpChannel(ChainSimulator simulator, KeyPair from, BigInteger amount)
        {
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(from, () =>
                ScriptUtils.BeginScript().AllowGas(from.Address, Address.Null, 1, 9999)
                    .CallContract("relay", "TopUpChannel", from.Address, amount).
                    SpendGas(from.Address).EndScript());
            simulator.EndBlock();
        }

        [TestMethod]
        public void TestTopup()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var testUser = KeyPair.Generate();
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, testUser, 100);

            var channelBalance = (BigInteger) nexus.RootChain.InvokeContract("relay", "GetBalance", testUser.Address);

            Assert.IsTrue(channelBalance == 100);

            
        }

        [TestMethod]
        public void TestSendReceive()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, 100);

            var channelBalance = (BigInteger)nexus.RootChain.InvokeContract("relay", "GetBalance", sender.Address);

            Assert.IsTrue(channelBalance == 100);

            var messageCount = 5;
            var messages = new RelayMessage[messageCount];

            var random = new Random();

            for (int i = 0; i < messageCount; i++)
            {
                var script = new byte[100];
                random.NextBytes(script);

                var message = new RelayMessage
                {
                    nexus = nexus.Name,
                    index = i,
                    receiver = receiver.Address, //node.Address,
                    script = script,
                    sender = sender.Address,
                    timestamp = Timestamp.Now
                };
                messages[i] = message;

                var receipt = RelayReceipt.FromMessage(message, sender);
                string serializedHex = Base16.Encode(receipt.ToByteArray());

                api.RelaySend(serializedHex);
            }
            
            var receipts = (ArrayResult) api.RelayReceive(receiver.Address.Text);
            
            Assert.IsTrue(receipts.values.Length == messageCount);

            for (int i = 0; i < messageCount; i++)
            {
                var obj = receipts.values[i];
                Assert.IsTrue(obj is ReceiptResult);

                var receiptResult = (ReceiptResult) obj;
                Assert.IsTrue(receiptResult.nexus == messages[i].nexus);
                Assert.IsTrue(new BigInteger(receiptResult.index, 10) == messages[i].index);
                //Assert.IsTrue(receiptResult.receiver == messages[i].receiver);
                //Assert.IsTrue(receiptResult.script == messages[i].script);
                //Assert.IsTrue(receiptResult.sender == messages[i].sender);
                Assert.IsTrue(receiptResult.timestamp == messages[i].timestamp);
            }
        }

        //test claiming just the last receipt and verify that it claims all previous receipts at the same time
        [TestMethod]
        public void TestMultiSendSingleClaim()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, 1000000);

            var messageCount = 5;
            var messages = new RelayMessage[messageCount];

            var random = new Random();

            for (int i = 0; i < messageCount; i++)
            {
                var script = new byte[100];
                random.NextBytes(script);

                var message = new RelayMessage
                {
                    nexus = nexus.Name,
                    index = i,
                    receiver = receiver.Address, //node.Address,
                    script = script,
                    sender = sender.Address,
                    timestamp = Timestamp.Now
                };
                messages[i] = message;

                var receipt = RelayReceipt.FromMessage(message, sender);
                string serializedHex = Base16.Encode(receipt.ToByteArray());

                api.RelaySend(serializedHex);
            }

            var receipts = (ArrayResult)api.RelayReceive(receiver.Address.Text);

            Assert.IsTrue(receipts.values.Length == messageCount);

            for (int i = 0; i < messageCount; i++)
            {
                var obj = receipts.values[i];
                Assert.IsTrue(obj is ReceiptResult);

                var receiptResult = (ReceiptResult)obj;
                Assert.IsTrue(receiptResult.nexus == messages[i].nexus);
                Assert.IsTrue(new BigInteger(receiptResult.index, 10) == messages[i].index);
                //Assert.IsTrue(receiptResult.receiver == messages[i].receiver);
                //Assert.IsTrue(receiptResult.script == messages[i].script);
                //Assert.IsTrue(receiptResult.sender == messages[i].sender);
                Assert.IsTrue(receiptResult.timestamp == messages[i].timestamp);
            }

            var lastMessage = messages[messageCount - 1];
            var lastReceipt = RelayReceipt.FromMessage(lastMessage, sender);

            var senderInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, sender.Address);
            var chainInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, nexus.RootChainAddress);
            var receiverInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, node.Address);

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(sender, () =>
                ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                    .CallContract("relay", "UpdateChannel", lastReceipt).
                    SpendGas(sender.Address).EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            var senderFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, sender.Address);
            var chainFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, simulator.Nexus.RootChainAddress);
            var receiverFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, receiver.Address);

            var expectedFee = RelayContract.RelayFeePerMessage * messageCount;

            Assert.IsTrue(senderFinalBalance == senderInitialBalance - txCost);
            Assert.IsTrue(chainFinalBalance == chainInitialBalance - (expectedFee/2) + txCost);
            Assert.IsTrue(receiverFinalBalance == receiverInitialBalance + (expectedFee / 2));
        }

        //test claiming each receipt in sequence and verify that it claims each receipt successfully
        [TestMethod]
        public void TestMultiSendMultiClaim()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = KeyPair.Generate();
            var receiver = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, 1000000);

            var messageCount = 5;
            var messages = new RelayMessage[messageCount];

            var random = new Random();

            for (int i = 0; i < messageCount; i++)
            {
                var script = new byte[100];
                random.NextBytes(script);

                var message = new RelayMessage
                {
                    nexus = nexus.Name,
                    index = i,
                    receiver = receiver.Address, //node.Address,
                    script = script,
                    sender = sender.Address,
                    timestamp = Timestamp.Now
                };
                messages[i] = message;

                var receipt = RelayReceipt.FromMessage(message, sender);
                string serializedHex = Base16.Encode(receipt.ToByteArray());

                api.RelaySend(serializedHex);
            }

            var receipts = (ArrayResult)api.RelayReceive(receiver.Address.Text);

            Assert.IsTrue(receipts.values.Length == messageCount);

            for (int i = 0; i < messageCount; i++)
            {
                var obj = receipts.values[i];
                Assert.IsTrue(obj is ReceiptResult);

                var receiptResult = (ReceiptResult)obj;
                Assert.IsTrue(receiptResult.nexus == messages[i].nexus);
                Assert.IsTrue(new BigInteger(receiptResult.index, 10) == messages[i].index);
                //Assert.IsTrue(receiptResult.receiver == messages[i].receiver);
                //Assert.IsTrue(receiptResult.script == messages[i].script);
                //Assert.IsTrue(receiptResult.sender == messages[i].sender);
                Assert.IsTrue(receiptResult.timestamp == messages[i].timestamp);

                var lastMessage = messages[i];
                var lastReceipt = RelayReceipt.FromMessage(lastMessage, sender);

                var senderInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, sender.Address);
                var chainInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, nexus.RootChainAddress);
                var receiverInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, receiver.Address);

                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", "UpdateChannel", lastReceipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();

                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                var senderFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, sender.Address);
                var chainFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, nexus.RootChainAddress);
                var receiverFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(Nexus.FuelTokenSymbol, receiver.Address);

                var expectedFee = RelayContract.RelayFeePerMessage;

                Assert.IsTrue(senderFinalBalance == senderInitialBalance - txCost);
                Assert.IsTrue(chainFinalBalance == chainInitialBalance - (expectedFee / 2) + txCost);
                Assert.IsTrue(receiverFinalBalance == receiverInitialBalance + (expectedFee / 2));
            }
        }

        [TestMethod]
        public void TestMalformedReceipts()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, 100);

            var message = new RelayMessage
            {
                nexus = "invalid nexus",
                index = 0,
                receiver = node.Address,
                script = new byte[0],
                sender = sender.Address,
                timestamp = Timestamp.Now
            };

            var receipt = RelayReceipt.FromMessage(message, sender);
            string serializedHex = Base16.Encode(receipt.ToByteArray());

            api.RelaySend(serializedHex);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", "UpdateChannel", receipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();
            }, "should have thrown exception due to wrong nexus name");
            
        }

        /*
        [TestMethod]
        public void TestMessageInterception()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var testUser = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, Nexus.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, testUser, 100);

            var message = new RelayMessage
            {
                nexus = nexus.Name,
                index = 0,
                receiver = node.Address,
                script = new byte[0],
                sender = testUser.Address,
                timestamp = Timestamp.Now
            };

            var receipt = RelayReceipt.FromMessage(message, testUser);
            string serializedHex = Base16.Encode(receipt.ToByteArray());

            api.RelaySend(serializedHex);

            var receipts = (ArrayResult)api.RelayReceive(node.Address.Text);

            Assert.IsTrue(receipts.values.Length == 1);

                var obj = receipts.values[0];
                Assert.IsTrue(obj is ReceiptResult);

                var receiptResult = (ReceiptResult)obj;
                //Assert.IsTrue(receiptResult.nexus == messages[i].nexus);
                //Assert.IsTrue(new BigInteger(receiptResult.index, 10) == messages[i].index);
                //Assert.IsTrue(receiptResult.receiver == messages[i].receiver);
                //Assert.IsTrue(receiptResult.script == messages[i].script);
                //Assert.IsTrue(receiptResult.sender == messages[i].sender);
                //Assert.IsTrue(receiptResult.timestamp == messages[i].timestamp);
        }
        */

        [TestMethod]
        public void TestAutoSendAddress()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var testUser = KeyPair.Generate();
            var autoSender = KeyPair.Generate();
            var receiver = KeyPair.Generate();
            var node = KeyPair.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            var symbol = Nexus.StakingTokenSymbol;

            var senderAddressStr = Base16.Encode(autoSender.Address.PublicKey);
            var receivingAddressStr = Base16.Encode(receiver.Address.PublicKey);

            string[] scriptString = new string[]
            {
                $"alias r1, $triggerReceive",
                $"alias r2, $currentTrigger",
                $"alias r3, $comparisonResult",
                
                $@"load $triggerReceive, ""{AccountContract.TriggerReceive}""",
                $"pop $currentTrigger",

                $"equal $triggerReceive, $currentTrigger, $comparisonResult",
                $"jmpif $comparisonResult, @receiveHandler",

                $"jmp @end",

                $"@receiveHandler: nop",
                
                $"alias r4, $tokenContract",
                $"alias r5, $sourceAddress",
                $"alias r6, $targetAddress",
                $"alias r7, $receivedAmount",
                $"alias r8, $symbol",
                $"alias r9, $methodName",
                
                $"pop $sourceAddress",
                $"pop $receivedAmount",
                $"pop $symbol",

                $"load r11 0x{receivingAddressStr}",
                $"push r11",
                $@"extcall ""Address()""",
                $"pop $targetAddress",

                $@"load $methodName, ""TransferTokens""",

                $"push $receivedAmount",
                $"push $symbol",
                $"push $targetAddress",
                $"push $sourceAddress",
                $@"push $methodName",
                
                //switch to token contract
                $@"load r12, ""token""",
                $"ctx r12, $tokenContract",
                $"switch $tokenContract",
                
                $"@end: ret"
            };

            var script = AssemblerUtils.BuildScript(scriptString);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, autoSender.Address, simulator.Nexus.RootChain, Nexus.FuelTokenSymbol, 100000);
            simulator.GenerateCustomTransaction(autoSender,
                () => ScriptUtils.BeginScript().AllowGas(autoSender.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", autoSender.Address, script).SpendGas(autoSender.Address)
                    .EndScript());
            simulator.EndBlock();


            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, autoSender.Address, simulator.Nexus.RootChain, symbol, 30000);
            simulator.EndBlock();
        
            var balance = simulator.Nexus.RootChain.GetTokenBalance(symbol, receiver.Address);
            Assert.IsTrue(balance == 30000);

        }

    }

}
