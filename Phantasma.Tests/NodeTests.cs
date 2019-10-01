using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Contracts;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Network.P2P;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Simulator;
using System.Linq;
using Phantasma.Domain;

namespace Phantasma.Tests
{
    [TestClass]
    public class NodeTests
    {
        Nexus nexus;
        Node node;
        private Mempool mempool;
        private string nexusWif = "L2LGgkZAdupN2ee8Rs6hpkc65zaGcLbxhbSDGq8oh6umUxxzeW25";
        private string host = "127.0.0.1";

        [TestMethod]
        public void TestTransactions()
        {
            InitMainNode();

            int addressCount = 20;
            LinkedList<PhantasmaKeys> addressList = new LinkedList<PhantasmaKeys>();

            for (int i = 0; i < addressCount; i++)
            {
                var key = PhantasmaKeys.Generate();

                if (addressList.Contains(key) == false)
                    addressList.AddLast(key);
            }

            var currentKey = addressList.First;

            var masterKeys = PhantasmaKeys.FromWIF(nexusWif);
            //Trace.Message($"Connecting to host: {host} with address {masterKeys.Address.Text}");

            var amount = UnitConversion.ToBigInteger(1000000, DomainSettings.StakingTokenDecimals);
            var hash = SendTransfer(host, masterKeys, currentKey.Value.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            ConfirmTransaction(host, hash);

            int totalTxs = 0;
            int okTxs = 0;

            BigInteger currentKeyBalance = GetBalance(currentKey.Value.Address);

            amount = UnitConversion.ToBigInteger(1000000, DomainSettings.FuelTokenDecimals);
            SendTransfer(host, masterKeys, currentKey.Value.Address, amount, "KCAL");

            //while (currentKeyBalance > 9999)
            while (totalTxs < 10)
            {
                var destKey = currentKey.Next != null ? currentKey.Next : addressList.First;

                var txHash = SendTransfer(host, currentKey.Value, destKey.Value.Address, currentKeyBalance - 9999);
                if (txHash == Hash.Null)
                {
                    amount = UnitConversion.ToBigInteger(1000000, DomainSettings.FuelTokenDecimals);
                    SendTransfer(host, masterKeys, currentKey.Value.Address, amount);
                }

                do
                {
                    Thread.Sleep(100);
                } while (mempool.Size > 0);

                var confirmation = ConfirmTransaction(host, hash);

                okTxs += confirmation ? 1 : 0;

                totalTxs++;
                currentKey = destKey;
                currentKeyBalance = GetBalance(currentKey.Value.Address);

                Trace.WriteLine(currentKeyBalance);
            }
            Assert.IsTrue(okTxs == totalTxs);

            CloseNode();
        }

        [TestMethod]
        public void TestMempoolSubmission()
        {
            InitMainNode();

            var masterKeys = PhantasmaKeys.FromWIF(nexusWif);
            
            var currentKey = PhantasmaKeys.Generate();

            var amount = UnitConversion.ToBigInteger(1000000, DomainSettings.StakingTokenDecimals);
            var hash = SendTransfer(host, masterKeys, currentKey.Address, amount);
            if (hash == Hash.Null)
            {
                return;
            }

            var confirm = ConfirmTransaction(host, hash);

            Assert.IsTrue(confirm);

            CloseNode();
        }

        private BigInteger GetBalance(Address address)
        {
            return nexus.RootChain.GetTokenBalance(nexus.RootStorage, DomainSettings.FuelTokenSymbol, address);
        }

        private void InitMainNode()
        {
            var log = new ConsoleLogger();

            string wif = nexusWif;

            int port = 7077;

            var node_keys = PhantasmaKeys.FromWIF(wif);

            var simulator = new NexusSimulator(node_keys, 1234);
            nexus = simulator.Nexus;

            // mempool setup
            mempool = new Mempool(node_keys, nexus, Mempool.MinimumBlockTime, 1);
            mempool.Start();

            // node setup
            node = new Node("test node", nexus, mempool, node_keys, port, PeerCaps.Mempool, Enumerable.Empty<String>(), log);
            log.Message("Phantasma Node address: " + node_keys.Address.Text);
            node.Start();
        }

        private void CloseNode()
        {
            mempool.Stop();
            node.Stop();
        }

        private Hash SendTransfer(string host, PhantasmaKeys from, Address to, BigInteger amount, string tokenSymbol = "SOUL")
        {
            var script = ScriptUtils.BeginScript().AllowGas(from.Address, Address.Null, 1, 9999).TransferTokens(tokenSymbol, from.Address, to, amount).SpendGas(from.Address).EndScript();
            return SendTransaction(host, from, script);
        }

        private Hash SendTransaction(string host, PhantasmaKeys from, byte[] script)
        { 
            var tx = new Transaction("simnet", "main", script, Timestamp.Now + TimeSpan.FromMinutes(30));
            tx.Sign(from);

            try
            {
                mempool.Submit(tx);
            }
            catch (Exception)
            {
                return Hash.Null;
            }

            return tx.Hash;
        }

        bool ConfirmTransaction(string host, Hash hash, int maxTries = 99999)
        {
            int tryCount = 0;
            do
            {
                var confirmations = nexus.GetConfirmationsOfHash(hash);
                if (confirmations > 0)
                {
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
