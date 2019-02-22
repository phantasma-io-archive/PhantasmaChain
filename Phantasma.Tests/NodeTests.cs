using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Utils;

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

            int addressCount = 20;
            LinkedList<KeyPair> addressList = new LinkedList<KeyPair>();

            for (int i = 0; i < addressCount; i++)
            {
                var key = KeyPair.Generate();

                if (addressList.Contains(key) == false)
                    addressList.AddLast(key);
            }

            var currentKey = addressList.First;

            var masterKeys = KeyPair.FromWIF(nexusWif);
            log.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var amount = UnitConversion.ToBigInteger(1000000, Nexus.FuelTokenDecimals);
            var hash = SendTransfer(log, host, masterKeys, currentKey.Value.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(log, host, hash);

            int totalTxs = 0;
            int okTxs = 0;

            BigInteger currentKeyBalance = GetBalance(currentKey.Value.Address);

            //while (currentKeyBalance > 9999)
            while (totalTxs < 10)
            {
                var destKey = currentKey.Next != null ? currentKey.Next : addressList.First;

                var txHash = SendTransfer(null, host, currentKey.Value, destKey.Value.Address, currentKeyBalance - 9999);
                if (txHash == Hash.Null)
                {
                    log.Error($"Error sending {amount} SOUL from {currentKey.Value.Address} to {destKey.Value.Address}...");
                    amount = UnitConversion.ToBigInteger(1000000, Nexus.FuelTokenDecimals);
                    SendTransfer(log, host, masterKeys, currentKey.Value.Address, amount);
                }

                do
                {
                    Thread.Sleep(100);
                } while (mempool.Size > 0);

                var confirmation = ConfirmTransaction(log, host, hash);

                okTxs += confirmation ? 1 : 0;

                totalTxs++;
                currentKey = destKey;
                currentKeyBalance = GetBalance(currentKey.Value.Address);

                Trace.WriteLine(currentKeyBalance);
            }
            Assert.IsTrue(okTxs == totalTxs);
        }

        private BigInteger GetBalance(Address address)
        {
            return nexus.RootChain.GetTokenBalance(nexus.FuelToken, address);
        }

        private void InitMainNode()
        {
            var log = new ConsoleLogger();
            var seeds = new List<string>();

            Console.ForegroundColor = ConsoleColor.DarkGray;

            string wif = nexusWif;

            int port = 7077;

            var node_keys = KeyPair.FromWIF(wif);

            var simulator = new ChainSimulator(node_keys, 1234, -1);
            nexus = simulator.Nexus;
            /*
            for (int i = 0; i < 100; i++)
            {
                simulator.GenerateRandomBlock();
            }
            */
            // mempool setup
            mempool = new Mempool(node_keys, nexus, Mempool.MinimumBlockTime);
            mempool.Start();

            // node setup
            var node = new Node(nexus, mempool, node_keys, port, seeds, log);
            log.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();
        }

        private Hash SendTransfer(Logger log, string host, KeyPair from, Address to, BigInteger amount)
        {
            var script = ScriptUtils.BeginScript().AllowGas(from.Address, Address.Null, 1, 9999).TransferTokens("SOUL", from.Address, to, amount).SpendGas(from.Address).EndScript();

            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
            tx.Sign(from);

            try
            {
                mempool.Submit(tx);
            }
            catch (Exception)
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

                Thread.Sleep(500);
            } while (true);
        }
    }
}
