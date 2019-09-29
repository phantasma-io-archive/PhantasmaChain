using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.API;
using Phantasma.Blockchain;
using Phantasma.Contracts.Native;
using Phantasma.Simulator;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.VM.Utils;
using static Phantasma.Contracts.Native.RelayContract;
using Phantasma.Blockchain.Contracts;
using Phantasma.Domain;
using Phantasma.Contracts;

namespace Phantasma.Tests
{
    [TestClass]
    public class RelayTests
    {
        private static readonly string testWIF = "Kx9Kr8MwQ9nAJbHEYNAjw5n99B2GpU6HQFf75BGsC3hqB1ZoZm5W";
        private static readonly string nodeWIF = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";

        private ApiTests.TestData CreateAPI(bool useMempool = true)
        {
            var owner = PhantasmaKeys.FromWIF(testWIF);
            var sim = new NexusSimulator(owner, 1234);
            var mempool = useMempool ? new Mempool(owner, sim.Nexus, 2, 1) : null;
            var node = useMempool ? new Node(sim.Nexus, mempool, owner, 7073, PeerCaps.None, new List<string>() { "192.168.0.1:7073" }, null) : null;
            var api = useMempool ? new NexusAPI(sim.Nexus) : null;

            if (api != null)
            {
                api.Mempool = mempool;
                api.Node = node;
            }

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

        private void TopUpChannel(NexusSimulator simulator, PhantasmaKeys from, BigInteger amount)
        {
            simulator.BeginBlock();
            simulator.GenerateCustomTransaction(from, ProofOfWork.None, () =>
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
            var testUser = PhantasmaKeys.Generate();
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            var desiredChannelBalance = RelayFeePerMessage;

            TopUpChannel(simulator, testUser, desiredChannelBalance);

            var channelBalance = nexus.RootChain.InvokeContract(nexus.RootStorage, "relay", "GetBalance", testUser.Address).AsNumber();

            Assert.IsTrue(channelBalance == desiredChannelBalance);            
        }

        [TestMethod]
        public void TestSendReceive()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            var desiredChannelBalance = RelayFeePerMessage * 10;

            TopUpChannel(simulator, sender, desiredChannelBalance);

            var channelBalance = nexus.RootChain.InvokeContract(simulator.Nexus.RootStorage, "relay", "GetBalance", sender.Address).AsNumber();

            Assert.IsTrue(channelBalance == desiredChannelBalance);

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
                string serializedHex = Base16.Encode(receipt.Serialize());

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
            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            var contractAddress = SmartContract.GetAddressForNative(NativeContractKind.Relay);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
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
                string serializedHex = Base16.Encode(receipt.Serialize());

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

            var senderInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
            var chainInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
            var receiverInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, node.Address);

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                    .CallContract("relay", nameof(RelayContract.SettleChannel), lastReceipt).
                    SpendGas(sender.Address).EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            var senderFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
            var chainFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
            var receiverFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, receiver.Address);

            var expectedFee = RelayFeePerMessage * messageCount;

            Assert.IsTrue(senderFinalBalance == senderInitialBalance - txCost);
            Assert.IsTrue(receiverFinalBalance == receiverInitialBalance + (expectedFee / 2));
            Assert.IsTrue(chainFinalBalance == chainInitialBalance - (expectedFee / 2));    //the sender's balance is escrowed in the chain address, so the chain just sends the other half of the fee away to the receiver
        }

        //test claiming every 10th receipt 
        [TestMethod]
        public void TestMultiSendMultiClaim()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            var contractAddress = SmartContract.GetAddressForName("relay");

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            

            var messageCount = 100;
            var messages = new RelayMessage[messageCount];

            TopUpChannel(simulator, sender, messageCount * RelayFeePerMessage);

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
                string serializedHex = Base16.Encode(receipt.Serialize());

                api.RelaySend(serializedHex);
            }

            var receipts = (ArrayResult)api.RelayReceive(receiver.Address.Text);

            Assert.IsTrue(receipts.values.Length == messageCount);

            var receiptStep = 10;

            for (int i = 9; i < messageCount; i+=receiptStep)
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

                var senderInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
                var chainInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
                var receiverInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, receiver.Address);

                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", nameof(RelayContract.SettleChannel), lastReceipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();

                var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

                var senderFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
                var chainFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
                var receiverFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, receiver.Address);

                var expectedFee = RelayFeePerMessage * receiptStep;

                Assert.IsTrue(senderFinalBalance == senderInitialBalance - txCost);
                Assert.IsTrue(receiverFinalBalance == receiverInitialBalance + (expectedFee / 2));
                Assert.IsTrue(chainFinalBalance == chainInitialBalance - (expectedFee / 2));    //the sender's balance is escrowed in the chain address, so the chain just sends the other half of the fee away to the receiver
            }
        }

        [TestMethod]
        public void TestWrongNexusName()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;
            var random = new Random();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, RelayFeePerMessage * 100);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, RelayFeePerMessage * 10);

            var script = new byte[20];
            random.NextBytes(script);

            var message = new RelayMessage
            {
                nexus = "invalid nexus",
                index = 0,
                receiver = node.Address,
                script = script,
                sender = sender.Address,
                timestamp = Timestamp.Now
            };

            var receipt = RelayReceipt.FromMessage(message, sender);
            string serializedHex = Base16.Encode(receipt.Serialize());

            api.RelaySend(serializedHex);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", nameof(RelayContract.SettleChannel), receipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();
            }, "should have thrown exception due to wrong nexus name");
            
        }

        [TestMethod]
        public void TestWrongSender()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;
            var random = new Random();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, RelayFeePerMessage * 100);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, RelayFeePerMessage * 10);

            var script = new byte[20];
            random.NextBytes(script);

            var message = new RelayMessage
            {
                nexus = "invalid nexus",
                index = 0,
                receiver = node.Address,
                script = script,
                sender = PhantasmaKeys.Generate().Address,
                timestamp = Timestamp.Now
            };

            var receipt = RelayReceipt.FromMessage(message, sender);
            string serializedHex = Base16.Encode(receipt.Serialize());

            api.RelaySend(serializedHex);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", nameof(RelayContract.SettleChannel), receipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();
            }, "should have thrown exception due to wrong sender");

        }

        [TestMethod]
        public void TestInvalidIndex()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;
            var random = new Random();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, RelayFeePerMessage * 100);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, RelayFeePerMessage * 10);

            var script = new byte[20];
            random.NextBytes(script);

            var message = new RelayMessage
            {
                nexus = "invalid nexus",
                index = -123540,
                receiver = node.Address,
                script = script,
                sender = sender.Address,
                timestamp = Timestamp.Now
            };

            var receipt = RelayReceipt.FromMessage(message, sender);
            string serializedHex = Base16.Encode(receipt.Serialize());

            api.RelaySend(serializedHex);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", nameof(RelayContract.SettleChannel), receipt).
                        SpendGas(sender.Address).EndScript());
                simulator.EndBlock();
            }, "should have thrown exception due to invalid index");

        }

        [TestMethod]
        public void TestInvalidTimestamp()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;
            var random = new Random();

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, RelayFeePerMessage * 100);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, RelayFeePerMessage * 10);

            var script = new byte[20];
            random.NextBytes(script);

            var message = new RelayMessage
            {
                nexus = "invalid nexus",
                index = 0,
                receiver = node.Address,
                script = script,
                sender = sender.Address,
                timestamp = DateTime.Now.AddYears(200)
            };

            var receipt = RelayReceipt.FromMessage(message, sender);
            string serializedHex = Base16.Encode(receipt.Serialize());

            api.RelaySend(serializedHex);

            Assert.ThrowsException<ChainException>(() =>
            {
                simulator.BeginBlock();
                var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                    ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                        .CallContract("relay", nameof(RelayContract.SettleChannel), receipt).
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
            simulator.GenerateTransfer(owner, testUser.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
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
            var testUser = PhantasmaKeys.Generate();
            var autoSender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            var symbol = DomainSettings.StakingTokenSymbol;

            var senderAddressStr = Base16.Encode(autoSender.Address.ToByteArray());
            var receivingAddressStr = Base16.Encode(receiver.Address.ToByteArray());

            string[] scriptString = new string[]
            {
                $"alias r1, $triggerReceive",
                $"alias r2, $currentTrigger",
                $"alias r3, $comparisonResult",
                
                $@"load $triggerReceive, ""{AccountTrigger.OnReceive}""",
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

                $"pop $symbol",
                $"pop $sourceAddress",
                $"pop $receivedAmount",

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
            simulator.GenerateTransfer(owner, autoSender.Address, simulator.Nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000);
            simulator.GenerateCustomTransaction(autoSender, ProofOfWork.None,
                () => ScriptUtils.BeginScript().AllowGas(autoSender.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", autoSender.Address, script).SpendGas(autoSender.Address)
                    .EndScript());
            simulator.EndBlock();


            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, autoSender.Address, simulator.Nexus.RootChain, symbol, 30000);
            simulator.EndBlock();
        
            var balance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, symbol, receiver.Address);
            Assert.IsTrue(balance == 30000);

        }

        //test claiming a receipt with an index gap > 1 over the last onchain receipt and verify that the sender is charged the appropriate amount
        [TestMethod]
        public void TestIndexGap()
        {
            var test = CreateAPI();

            var simulator = test.simulator;
            var owner = test.owner;
            var sender = PhantasmaKeys.Generate();
            var receiver = PhantasmaKeys.Generate();
            var node = PhantasmaKeys.FromWIF(nodeWIF);
            var nexus = simulator.Nexus;
            var api = test.api;

            var contractAddress = SmartContract.GetAddressForName("relay");

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, sender.Address, nexus.RootChain, DomainSettings.FuelTokenSymbol, 100000000);
            simulator.EndBlock();

            TopUpChannel(simulator, sender, 1000000);

            var indexGap = 5;
            var messageCount = 3;
            var messages = new RelayMessage[messageCount];

            var random = new Random();

            for (int i = 0; i < messageCount; i++)
            {
                var script = new byte[100];
                random.NextBytes(script);

                var message = new RelayMessage
                {
                    nexus = nexus.Name,
                    index = i * indexGap,
                    receiver = receiver.Address, //node.Address,
                    script = script,
                    sender = sender.Address,
                    timestamp = Timestamp.Now
                };
                messages[i] = message;

                var receipt = RelayReceipt.FromMessage(message, sender);
                string serializedHex = Base16.Encode(receipt.Serialize());

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

            var senderInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
            var chainInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
            var receiverInitialBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, node.Address);

            simulator.BeginBlock();
            var tx = simulator.GenerateCustomTransaction(sender, ProofOfWork.None, () =>
                ScriptUtils.BeginScript().AllowGas(sender.Address, Address.Null, 1, 9999)
                    .CallContract("relay", nameof(RelayContract.SettleChannel), lastReceipt).
                    SpendGas(sender.Address).EndScript());
            simulator.EndBlock();

            var txCost = simulator.Nexus.RootChain.GetTransactionFee(tx);

            var senderFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, sender.Address);
            var chainFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, contractAddress);
            var receiverFinalBalance = simulator.Nexus.RootChain.GetTokenBalance(simulator.Nexus.RootStorage, DomainSettings.FuelTokenSymbol, receiver.Address);

            var expectedFee = RelayFeePerMessage * (lastReceipt.message.index + 1);

            Assert.IsTrue(senderFinalBalance == senderInitialBalance - txCost);
            Assert.IsTrue(receiverFinalBalance == receiverInitialBalance + (expectedFee / 2));
            Assert.IsTrue(chainFinalBalance == chainInitialBalance - (expectedFee / 2));    //the sender's balance is escrowed in the chain address, so the chain just sends the other half of the fee away to the receiver
        }

    }

}
