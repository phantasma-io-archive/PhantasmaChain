using System.Linq;
using Phantasma.Blockchain;
using LunarLabs.Parser;
using Phantasma.Blockchain.Plugins;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using LunarLabs.Parser.JSON;
using System;
using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.API
{
    public class NexusAPI
    {
        public Nexus Nexus { get; }

        public Mempool Mempool { get; }

        public NexusAPI(Nexus nexus, Mempool mempool = null)
        {
            Throw.IfNull(nexus, nameof(nexus));

            Nexus = nexus;
            Mempool = mempool;
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
            // result.AddField("minerAddress", block.MinerAddress.Text); TODO fixme

            return result;
        }
        #endregion

        public DataNode GetAccount(Address address)
        {
            var result = DataNode.CreateObject();

            result.AddField("address", address.Text);
            var name = Nexus.LookUpAddress(address);
            result.AddField("name", name);

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
                        if (!token.IsFungible)
                        {
                            var idList = chain.GetTokenOwnerships(token).Get(address);
                            if (idList != null && idList.Any())
                            {
                                var nodeId = DataNode.CreateArray("ids");
                                idList.ForEach(p => nodeId.AddValue(p.ToString()));
                                balanceNode.AddNode(nodeId);
                            }
                        }
                    }
                }

                if (chainNode != null)
                {
                    var entryNode = DataNode.CreateObject();
                    tokenNode.AddNode(entryNode);
                    entryNode.AddField("symbol", token.Symbol);
                    entryNode.AddField("name", token.Name);
                    entryNode.AddField("decimals", token.Decimals);
                    entryNode.AddField("isFungible", token.IsFungible);
                    entryNode.AddNode(chainNode);
                }
            }

            return result;
        }

        public DataNode GetBlockNumber(Address address)
        {
            var chain = Nexus.FindChainByAddress(address);
            if (chain == null) return null;
            return GetBlockNumber(chain);
        }

        public DataNode GetBlockNumber(string chainName)
        {
            var chain = Nexus.FindChainByName(chainName);
            if (chain == null) return null;
            return GetBlockNumber(chain);
        }

        private DataNode GetBlockNumber(Chain chain)
        {
            var result = DataNode.CreateObject();
            result.AddField("chain", chain.Address.Text);
            result.AddField("height", chain.BlockHeight);
            return result;
        }

        public DataNode GetBlockTransactionCountByHash(Hash blockHash)
        {
            var result = DataNode.CreateObject();
            var count = Nexus.FindBlockForHash(blockHash).TransactionHashes.Count();
            result.AddValue(count);
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
            return GetBlockByHeight(chain, height);
        }

        public DataNode GetBlockByHeight(Address chainAddress, uint height)
        {
            var chain = Nexus.FindChainByAddress(chainAddress);
            return GetBlockByHeight(chain, height);
        }

        private DataNode GetBlockByHeight(Chain chain, uint height)
        {
            var block = chain.FindBlockByHeight(height);
            if (block != null)
            {
                return FillBlock(block);
            }

            return null;
        }

        public DataNode GetTransactionByBlockHashAndIndex(Hash blockHash, int index)
        {
            var block = Nexus.FindBlockForHash(blockHash);
            var txHash = block.TransactionHashes.ElementAt(index);
            return FillTransaction(Nexus.FindTransactionByHash(txHash));
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

            int confirmations = -1;

            var block = Nexus.FindBlockForHash(hash);
            if (block != null)
            {
                confirmations = Nexus.GetConfirmationsOfBlock(block);
            }
            else
            {
                var tx = Nexus.FindTransactionByHash(hash);
                if (tx != null)
                {
                    block = Nexus.FindBlockForTransaction(tx);
                    if (block != null)
                    {
                        confirmations = Nexus.GetConfirmationsOfBlock(block);
                    }
                }
            }

            Chain chain = (block != null) ? Nexus.FindChainForBlock(block) : null;

            if (confirmations == -1 || block == null || chain == null)
            {
                result.AddField("confirmations", (int)0);
                result.AddField("error", "unknown hash");
            }
            else
            {
                result.AddField("confirmations", confirmations);
                result.AddField("hash", block.Hash.ToString());
                result.AddField("height", block.Height);
                result.AddField("chain", chain.Address);
            }

            return result;
        }

        public DataNode SendRawTransaction(string txData)
        {
            var result = DataNode.CreateObject();

            if (Mempool != null)
            {
                var bytes = Base16.Decode(txData);
                var tx = Transaction.Unserialize(bytes);

                bool submited = Mempool.Submit(tx);
                if (!submited)
                {
                    result.AddField("error", "Not submited to mempool");
                }
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
                if (chain.ParentChain != null)
                {
                    single.AddField("parentName", chain.ParentChain.Name);
                    single.AddField("parentAddress", chain.ParentChain.Name);
                }

                if (chain.ChildChains != null && chain.ChildChains.Any())
                {
                    var children = DataNode.CreateArray("children");
                    foreach (var childChain in chain.ChildChains)
                    {
                        var child = DataNode.CreateObject();
                        child.AddField("name", childChain.Name);
                        child.AddField("address", childChain.Address.Text);
                        children.AddNode(child);
                    }

                    single.AddNode(children);
                }
                arrayNode.AddNode(single);
            }

            result.AddNode(arrayNode);

            var test = JSONWriter.WriteToString(result);
            Console.WriteLine(test);
            return result;
        }

        public DataNode GetTransaction(Hash hash)
        {
            var tx = Nexus.FindTransactionByHash(hash);

            var result = FillTransaction(tx);
            return result;
        }

        public DataNode GetTokens()
        {
            var result = DataNode.CreateObject();
            var node = DataNode.CreateArray("tokens");
            foreach (var token in Nexus.Tokens)
            {
                var temp = DataNode.CreateObject();
                temp.AddField("symbol", token.Symbol);
                temp.AddField("name", token.Name);
                temp.AddField("currentSupply", token.CurrentSupply);
                temp.AddField("maxSupply", token.MaxSupply);
                temp.AddField("decimals", token.Decimals);
                temp.AddField("isFungible", token.IsFungible);
                node.AddNode(temp);
                //todo add flags
            }
            result.AddNode(node);
            return result;
        }

        public DataNode GetApps()
        {
            var result = DataNode.CreateObject();
            var node = DataNode.CreateArray("apps");
            var appChain = Nexus.FindChainByName("apps");
            var apps = (AppInfo[])appChain.InvokeContract("apps", "GetApps", new string[] { });
            foreach (var appInfo in apps)
            {
                var temp = DataNode.CreateObject();
                temp.AddField("description", appInfo.description);
                temp.AddField("icon", appInfo.icon);
                temp.AddField("id", appInfo.id);
                temp.AddField("title", appInfo.title);
                temp.AddField("url", appInfo.url);
                node.AddNode(temp);
            }
            result.AddNode(node);
            return result;
        }
    }
}
