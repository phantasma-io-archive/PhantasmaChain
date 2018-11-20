using System.Linq;
using Phantasma.Blockchain;
using LunarLabs.Parser;
using Phantasma.Blockchain.Plugins;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using LunarLabs.Parser.JSON;
using System;

namespace Phantasma.API
{
    public class NexusAPI
    {
        public Nexus Nexus { get; private set; }

        public Mempool Mempool { get; private set; }

        public NexusAPI(Nexus nexus, Mempool mempool = null)
        {
            Throw.IfNull(nexus, nameof(nexus));

            this.Nexus = nexus;
            this.Mempool = mempool;
        }

        #region UTILS
        private DataNode FillTransaction(Transaction tx)
        {
            var block = Nexus.FindBlockForTransaction(tx);
            var chain = Nexus.FindChainForBlock(block.Hash);

            var result = DataNode.CreateObject();
            result.AddField("txid", tx.Hash.ToString());
            result.AddField("chainAddress", chain.Address);
            result.AddField("chainName", chain.Name);
            result.AddField("timestamp", block.Timestamp.Value);
            result.AddField("blockHeight", block.Height);
            result.AddField("gasLimit", tx.GasLimit.ToString());
            result.AddField("gasPrice", tx.GasPrice.ToString());
            result.AddField("script", Base16.Encode(tx.Script));

            var eventsNode = DataNode.CreateArray("events");

            var evts = block.GetEventsForTransaction(tx.Hash);
            foreach (var evt in evts)
            {
                var eventNode = DataNode.CreateObject();
                eventNode.AddField("address", evt.Address);
                eventNode.AddField("data", Base16.Encode(evt.Data));
                eventNode.AddField("kind", evt.Kind);
                eventsNode.AddNode(eventNode);
            }

            result.AddNode(eventsNode);

            return result;
        }

        private DataNode FillBlock(Block block)
        {
            var chain = Nexus.FindChainForBlock(block.Hash);

            var result = DataNode.CreateObject();

            result.AddField("hash", block.Hash.ToString());
            result.AddField("timestamp", block.Timestamp);
            result.AddField("height", block.Height);
            result.AddField("chainAddress", chain.Address);
            result.AddField("chainName", chain.Name);
            result.AddField("previousHash", block.PreviousHash);
            result.AddField("nonce", block.Nonce);
            result.AddField("minerAddress", block.MinerAddress.Text);

            return result;
        }
        #endregion

        public DataNode GetAccount(Address address)
        {
            var result = DataNode.CreateObject();

            result.AddField("address", address.Text);

            var tokenNode = DataNode.CreateArray("tokens");
            result.AddNode(tokenNode);

            foreach (var token in Nexus.Tokens)
            {
                DataNode chainNode = null;

                foreach (var chain in Nexus.Chains)
                {
                    var balance = chain.GetTokenBalance(token, address);
                    if (balance > 0)
                    {
                        if (chainNode == null)
                        {
                            chainNode = DataNode.CreateArray("chains");
                        }

                        var balanceNode = DataNode.CreateObject();
                        chainNode.AddNode(balanceNode);

                        balanceNode.AddField("chain", chain.Name);
                        balanceNode.AddField("balance", balance);
                    }
                }

                if (chainNode != null)
                {
                    var entryNode = DataNode.CreateObject();
                    tokenNode.AddNode(entryNode);
                    entryNode.AddField("symbol", token.Symbol);
                    entryNode.AddField("name", token.Name);
                    entryNode.AddNode(chainNode);
                }
            }

            return result;
        }

        public DataNode GetBlockByHash(Hash hash)
        {
            foreach (var chain in Nexus.Chains)
            {
                var block = chain.FindBlockByHash(hash);
                if (block != null)
                {
                    return FillBlock(block);
                }
            }

            return null;
        }

        public DataNode GetBlockByHeight(string chainName, uint height)
        {
            var chain = Nexus.FindChainByName(chainName);
            var block = chain.FindBlockByHeight(height);
            if (block != null)
            {
                return FillBlock(block);
            }

            return null;
        }

        public DataNode GetAddressTransactions(Address address, int amountTx)
        {
            var result = DataNode.CreateObject();
            var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
            var txsNode = DataNode.CreateArray("txs");

            result.AddField("address", address.Text);
            result.AddField("amount", amountTx);
            result.AddNode(txsNode);
            var txs = plugin?.GetAddressTransactions(address).OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value).Take(amountTx);
            if (txs != null)
            {
                foreach (var transaction in txs)
                {
                    var entryNode = FillTransaction(transaction);
                    txsNode.AddNode(entryNode);
                }
            }

            return result;
        }

        public DataNode GetConfirmations(Hash hash)
        {
            var result = DataNode.CreateObject();

            int confirmations = Nexus.GetConfirmationsOfHash(hash);

            result.AddField("confirmations", confirmations);
            result.AddField("hash", hash.ToString());
            return result;
        }

        public DataNode SendRawTransaction(string chainName, string txData)
        {
            var result = DataNode.CreateObject();

            if (Mempool != null)
            {
                var bytes = Base16.Decode(txData);
                var tx = Transaction.Unserialize(bytes);

                var chain = Nexus.FindChainByName(chainName);

                Mempool.Submit(chain, tx);

                result.AddField("hash", tx.Hash);
            }
            else
            {
                result.AddField("error", "No mempool");
            }

            return result;
        }

        public DataNode GetChains()
        {
            var result = DataNode.CreateObject();

            var arrayNode = DataNode.CreateArray("chains");

            foreach (var chain in Nexus.Chains)
            {
                var single = DataNode.CreateObject();
                single.AddField("name", chain.Name);
                single.AddField("address", chain.Address.Text);
                arrayNode.AddNode(single);
            }

            result.AddNode(arrayNode);

            var test = JSONWriter.WriteToString(result);
            System.Console.WriteLine(test);
            return result;
        }

        public DataNode GetTransaction(Hash hash)
        {
            var tx = Nexus.FindTransactionByHash(hash);

            var result = FillTransaction(tx);
            return result;
        }

        /*
               public DataNode GetTokens()
               {

               }

               public DataNode GetApps()
               {

               }*/
    }
}
