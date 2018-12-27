using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Consensus;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Tests
{
    [TestClass]
    public class NodeTests
    {
        ConsoleLogger log = new ConsoleLogger();
        Nexus nexus;
        private Mempool mempool;
        private string nexusWif = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";
        private string host = "127.0.0.1";

        [TestMethod]
        public void TestTransactions()
        {
            InitMainNode();

            var currentKey = KeyPair.Generate();

            var masterKeys = KeyPair.FromWIF(nexusWif);
            log.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var amount = TokenUtils.ToBigInteger(1000000, Nexus.NativeTokenDecimals);
            var hash = SendTransfer(log, host, masterKeys, currentKey.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(log, host, hash);

            int totalTxs = 0;
            int okTxs = 0;

            while (totalTxs < 100)
            {
                KeyPair destKey;

                do
                {
                    destKey = KeyPair.Generate();
                } while (destKey == currentKey);

                amount = 1;

                var txHash = SendTransfer(null, host, currentKey, destKey.Address, amount);
                if (txHash == Hash.Null)
                {
                    log.Error($"Error sending {amount} SOUL from {currentKey.Address} to {destKey.Address}...");
                    amount = TokenUtils.ToBigInteger(1000000, Nexus.NativeTokenDecimals);
                    SendTransfer(log, host, masterKeys, currentKey.Address, amount);
                }

                var confirmation = ConfirmTransaction(log, host, hash);

                okTxs += confirmation ? 1 : 0;

                totalTxs++;
                currentKey = destKey;

                if (totalTxs % 10 == 0)
                {
                    log.Message($"Sent {totalTxs} transactions");
                }
            }
            Assert.IsTrue(okTxs == totalTxs);
        }

        private void InitMainNode()
        {
            var log = new ConsoleLogger();
            var seeds = new List<string>();

            Console.ForegroundColor = ConsoleColor.DarkGray;

            string wif = nexusWif;

            int port = 7077;

            var node_keys = KeyPair.FromWIF(wif);

            var simulator = new ChainSimulator(node_keys, 1234);
            nexus = simulator.Nexus;

            for (int i = 0; i < 100; i++)
            {
                simulator.GenerateRandomBlock();
            }

            // mempool setup
            mempool = new Mempool(node_keys, nexus);
            mempool.Start();

            // node setup
            var node = new Node(nexus, node_keys, port, seeds, log);
            log.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();
        }

        private Hash SendTransfer(Logger log, string host, KeyPair from, Address to, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(from.Address, 1, 9999).TransferTokens("SOUL", from.Address, to, amount).SpendGas(from.Address).EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30), 0);
            tx.Sign(from);

            var response = mempool.Submit(tx);
            if (response == false)
            {
                if (log != null)
                {
                    log.Error("Transfer request failed");
                }
                return Hash.Null;
            }

            return tx.Hash;
        }

        bool ConfirmTransaction(Logger log, string host, Hash hash, int maxTries = 99999)
        {
            int tryCount = 0;
            do
            {
                var confirmations = nexus.GetConfirmationsOfHash(hash);
                if (confirmations > 0)
                {
                    if (log != null)
                    {
                        log.Success("Confirmations: " + confirmations);
                    }
                    return true;
                }

                tryCount--;
                if (tryCount >= maxTries)
                {
                    return false;
                }

                Thread.Sleep(1000);
            } while (true);
        }
    }
}
