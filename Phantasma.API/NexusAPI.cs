using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using LunarLabs.Parser;
using Phantasma.Blockchain;
using Phantasma.Blockchain.Plugins;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.API
{
    public struct APIEntry
    {
        public readonly string Name;
        public readonly Type[] Parameters;

        private readonly NexusAPI API;
        private readonly MethodInfo Info;

        public APIEntry(NexusAPI api, MethodInfo info)
        {
            API = api;
            Info = info;
            Name = info.Name;
            Parameters = info.GetParameters().Select(x => x.ParameterType).ToArray();
        }

        public DataNode Execute(params string[] input)
        {
            if (input.Length != Parameters.Length)
            {
                throw new Exception("Unexpected number of arguments");
            }

            var args = new object[input.Length];
            for (int i =0; i< args.Length; i++)
            {
                if (Parameters[i] == typeof(string))
                {
                    args[i] = input[i];
                }
                else
                if (Parameters[i] == typeof(uint))
                {
                    args[i] = uint.Parse(input[i]); // TODO error handling
                }
                else
                if (Parameters[i] == typeof(int))
                {
                    args[i] = int.Parse(input[i]); // TODO error handling
                }
                else
                {
                    throw new Exception("API invalid parameter type: " + Parameters[i].FullName);
                }
            }
            return (DataNode) Info.Invoke(API, args);
        }
    }

    public class NexusAPI
    {
        public readonly Nexus Nexus;
        public readonly Mempool Mempool;
        public IEnumerable<APIEntry> Methods => _methods.Values;

        private readonly Dictionary<string, APIEntry> _methods = new Dictionary<string, APIEntry>();

        public NexusAPI(Nexus nexus, Mempool mempool = null)
        {
            Throw.IfNull(nexus, nameof(nexus));

            Nexus = nexus;
            Mempool = mempool;

            var methodInfo = this.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var entry in methodInfo)
            {
                if (entry.ReturnType == typeof(DataNode))
                {
                    _methods[entry.Name.ToLower()] = new APIEntry(this, entry);
                }
            }
        }

        public DataNode Execute(string methodName, string[] args)
        {
            methodName = methodName.ToLower();
            if (_methods.ContainsKey(methodName))
            {
                return _methods[methodName].Execute(args);
            }
            else
            {
                throw new Exception("Unknown method: " + methodName);
            }
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
            result.AddField("script", tx.Script.Encode());

            var eventsNode = DataNode.CreateArray("events");

            var evts = block.GetEventsForTransaction(tx.Hash);
            foreach (var evt in evts)
            {
                var eventNode = DataNode.CreateObject();
                eventNode.AddField("address", evt.Address);
                eventNode.AddField("data", evt.Data.Encode());
                eventNode.AddField("kind", evt.Kind);
                eventsNode.AddNode(eventNode);
            }

            result.AddNode(eventsNode);

            return result;
        }

        private DataNode FillBlock(Block block, Chain chain)
        {
            //var chain = Nexus.FindChainForBlock(block.Hash);
            var result = DataNode.CreateObject();

            result.AddField("hash", block.Hash.ToString());
            result.AddField("previousHash", block.PreviousHash.ToString());
            result.AddField("timestamp", block.Timestamp.Value);
            result.AddField("height", block.Height);
            result.AddField("chainAddress", block.ChainAddress.ToString());

            var minerAddress = Nexus.FindValidatorForBlock(block);
            result.AddField("minerAddress", minerAddress.Text);
            result.AddField("nonce", block.Nonce);
            result.AddField("reward", TokenUtils.ToDecimal(chain.GetBlockReward(block), Nexus.NativeTokenDecimals));
            var payload = block.Payload != null ? block.Payload.Encode() : new byte[0].Encode();
            result.AddField("payload", payload);//todo make sure this is ok

            var txsNode = DataNode.CreateArray("txs");
            result.AddNode(txsNode);
            if (block.TransactionHashes != null && block.TransactionHashes.Any())
            {
                foreach (var transactionHash in block.TransactionHashes)
                {
                    var tx = Nexus.FindTransactionByHash(transactionHash);
                    var entryNode = FillTransaction(tx);
                    txsNode.AddNode(entryNode);
                }
            }
            // todo add block size, gas, txs


            return result;
        }
        #endregion

        public DataNode GetAccount(string addressText)
        {
            var result = DataNode.CreateObject();
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                result.AddField("address", address.Text);
                var name = Nexus.LookUpAddress(address);
                result.AddField("name", name);

                var balancesNode = DataNode.CreateArray("balances");
                result.AddNode(balancesNode);

                foreach (var token in Nexus.Tokens)
                {
                    foreach (var chain in Nexus.Chains)
                    {
                        var balance = chain.GetTokenBalance(token, address);
                        if (balance > 0)
                        {
                            var balanceNode = DataNode.CreateObject();

                            balanceNode.AddField("chain", chain.Name);
                            balanceNode.AddField("amount", balance);
                            balanceNode.AddField("symbol", token.Symbol);
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
                            balancesNode.AddNode(balanceNode);
                        }
                    }
                }
            }
            else
            {
                result.AddField("error", "invalid address");
            }

            return result;
        }

        public DataNode GetBlockHeightFromChainAddress(string chainAddress)
        {
            if (Address.IsValidAddress(chainAddress))
            {
                var chain = Nexus.FindChainByAddress(Address.FromText(chainAddress));
                return GetBlockHeight(chain);
            }

            var result = DataNode.CreateObject();
            result.AddField("error", "invalid address");
            return result;
        }

        public DataNode GetBlockHeightFromChainName(string chainName)
        {
            var chain = Nexus.FindChainByName(chainName);
            if (chain == null) return null;
            return GetBlockHeight(chain);
        }

        private DataNode GetBlockHeight(Chain chain)
        {
            var result = DataNode.CreateValue("");
            if (chain != null)
            {
                result.Value = chain.BlockHeight.ToString();
            }
            else
            {
                result.AddField("error", "chain not found");
            }

            return result;
        }

        public DataNode GetBlockTransactionCountByHash(string blockHash)
        {
            var result = DataNode.CreateValue("");
            if (Hash.TryParse(blockHash, out var hash))
            {
                var count = Nexus.FindBlockByHash(hash)?.TransactionHashes.Count();
                if (count != null)
                {
                    result.Value = count.ToString();
                    return result;
                }
            }
            result.AddField("error", "invalid block hash");
            return result;
        }

        public DataNode GetBlockByHash(string blockHash, int serialized = 0)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                foreach (var chain in Nexus.Chains)
                {
                    var block = chain.FindBlockByHash(hash);
                    if (block != null)
                    {
                        if (serialized == 0)
                        {
                            return FillBlock(block, chain);
                        }

                        return SerializedBlock(block);
                    }
                }
            }
            var result = DataNode.CreateObject();
            result.AddField("error", "invalid block hash");
            return result;
        }

        public DataNode GetBlockByHeight(string chainName, uint height, int serialized = 0)
        {
            var chain = Nexus.FindChainByName(chainName);
            if (chain == null) return null;
            return GetBlockByHeight(chain, height, serialized);
        }

        public DataNode GetBlockByHeight(Address chainAddress, uint height, int serialized = 0)
        {
            var chain = Nexus.FindChainByAddress(chainAddress);
            return GetBlockByHeight(chain, height, serialized);
        }

        private DataNode GetBlockByHeight(Chain chain, uint height, int serialized)
        {
            var block = chain?.FindBlockByHeight(height);
            if (block != null)
            {
                if (serialized == 0)
                {
                    return FillBlock(block, chain);
                }

                return SerializedBlock(block);
            }
            var result = DataNode.CreateObject();
            result.AddField("error", "block not found");
            return result;
        }

        public DataNode GetTransactionByBlockHashAndIndex(string blockHash, int index)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = Nexus.FindBlockByHash(hash);
                if (block == null)
                {
                    var error = DataNode.CreateObject();
                    error.AddField("error", "unknown block hash");
                    return error;
                }
                var txHash = block.TransactionHashes.ElementAt(index);
                if (txHash == null)
                {
                    var error = DataNode.CreateObject();
                    error.AddField("error", "unknown tx index");
                }
                return FillTransaction(Nexus.FindTransactionByHash(txHash));
            }
            var result = DataNode.CreateObject();
            result.AddField("error", "invalid block hash");
            return result;
        }

        public DataNode GetAddressTransactions(string addressText, int amountTx)
        {
            var result = DataNode.CreateObject();
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
                var txsNode = DataNode.CreateArray("txs");

                result.AddField("address", address.Text);
                result.AddField("amount", amountTx);
                result.AddNode(txsNode);
                var txs = plugin?.GetAddressTransactions(address).
                    Select(hash => Nexus.FindTransactionByHash(hash)).
                    OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value).
                    Take(amountTx);
                if (txs != null)
                {
                    foreach (var transaction in txs)
                    {
                        var entryNode = FillTransaction(transaction);
                        txsNode.AddNode(entryNode);
                    }
                }
            }
            else
            {
                result.AddField("error", "invalid address");
            }

            return result;
        }

        public DataNode GetAddressTransactionCount(string addressText, string chainText)
        {
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
                int count = 0;
                if (!string.IsNullOrEmpty(chainText))
                {
                    var chain = Nexus.Chains.SingleOrDefault(p =>
                        p.Name.Equals(chainText) || p.Address.ToString().Equals(chainText));
                    if (chain != null)
                    {
                        count = plugin.GetAddressTransactions(address).Count(tx => Nexus.FindBlockForHash(tx).ChainAddress.Equals(chain.Address));
                    }
                }
                else
                {
                    foreach (var chain in Nexus.Chains)
                    {
                        count += plugin.GetAddressTransactions(address).Count(tx => Nexus.FindBlockForHash(tx).ChainAddress.Equals(chain.Address));
                    }
                }

                var result = DataNode.CreateValue("");
                result.Value = count.ToString();
                return result;
            }

            var error = DataNode.CreateObject();
            error.AddField("error", "invalid address");
            return error;
        }

        public DataNode GetConfirmations(string hashText)
        {
            var result = DataNode.CreateObject();
            if (Hash.TryParse(hashText, out var hash))
            {
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
                    result.AddField("confirmations", 0);
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

            result.AddField("error", "invalid hash");
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
                    return result;
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
            var result = DataNode.CreateArray();
            foreach (var chain in Nexus.Chains)
            {
                var single = DataNode.CreateObject();
                single.AddField("name", chain.Name);
                single.AddField("address", chain.Address.Text);
                single.AddField("height", chain.BlockHeight);
                if (chain.ParentChain != null)
                {
                    single.AddField("parentAddress", chain.ParentChain.Name);
                }
                var children = DataNode.CreateArray("children");
                if (chain.ChildChains != null && chain.ChildChains.Any())
                {
                    foreach (var childChain in chain.ChildChains)
                    {
                        var child = DataNode.CreateObject();
                        child.AddField("name", childChain.Name);
                        child.AddField("address", childChain.Address.Text);
                        children.AddNode(child);
                    }

                    single.AddNode(children);
                }
                result.AddNode(single);
            }

            return result;
        }

        public DataNode GetTransaction(string hashText)
        {
            Hash hash;
            DataNode result;
            
            if (Hash.TryParse(hashText, out hash))
            {
                var tx = Nexus.FindTransactionByHash(hash);

                result = FillTransaction(tx);
            }
            else
            {
                result = DataNode.CreateObject();
                result.AddField("error", "Invalid hash");
            }

            return result;
        }

        public DataNode GetTokens()
        {
            var result = DataNode.CreateObject();
            var node = DataNode.CreateArray("tokens");
            foreach (var token in Nexus.Tokens)
            {
                var tokenNode = DataNode.CreateObject();
                tokenNode.AddField("symbol", token.Symbol);
                tokenNode.AddField("name", token.Name);
                tokenNode.AddField("currentSupply", token.CurrentSupply);
                tokenNode.AddField("maxSupply", token.MaxSupply);
                tokenNode.AddField("decimals", token.Decimals);
                tokenNode.AddField("isFungible", token.IsFungible);
                tokenNode.AddField("flags", token.Flags);
                tokenNode.AddField("owner", token.Owner.ToString());
                node.AddNode(tokenNode);
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

        public DataNode GetRootChain()
        {
            var result = DataNode.CreateObject();
            result.AddField("name", Nexus.RootChain.Name);
            result.AddField("address", Nexus.RootChain.Address.ToString());
            result.AddField("height", Nexus.RootChain.BlockHeight.ToString());
            return result;
        }

        public DataNode GetTokenTransfers(string tokenSymbol, int amount)
        {
            var result = DataNode.CreateArray();
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txsHash = plugin.GetTokenTransactions(tokenSymbol);
            int count = 0;
            foreach (var hash in txsHash)
            {
                var tx = Nexus.FindTransactionByHash(hash);
                if (tx != null)
                {
                    result.AddNode(FillTransaction(tx));
                    count++;
                    if (count == amount) return result;
                }
            }

            return result;
        }
        public DataNode GetTokenTransferCount(string tokenSymbol)
        {
            var result = DataNode.CreateValue("");
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txCount = plugin.GetTokenTransactions(tokenSymbol).Count();
            result.Value = txCount.ToString();

            return result;
        }

        public DataNode GetTokenBalance(string addressText, string tokenSymbol, string chainInput) //todo rest
        {
            var result = DataNode.CreateObject();
            if (!Address.IsValidAddress(addressText))
            {
                result.AddField("error", "invalid address");
                return result;
            }

            Token token = Nexus.FindTokenBySymbol(tokenSymbol);

            if (token == null)
            {
                result.AddField("error", "invalid token");
                return result;
            }

            var chain = Nexus.FindChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    result.AddField("error", "invalid address");
                    return result;
                }

                chain = Nexus.FindChainByAddress(Address.FromText(chainInput));
                if (chain == null)
                {
                    result.AddField("error", "invalid chain");
                    return result;
                }
            }

            var address = Address.FromText(addressText);
            var balance = chain.GetTokenBalance(token, address);
            var balanceNode = DataNode.CreateObject();

            if (balance > 0)
            {
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
            return balanceNode;
        }

        private DataNode SerializedBlock(Block block)
        {
            var serializedBlock = DataNode.CreateValue("");
            serializedBlock.Value = (block.ToByteArray().Encode());
            return serializedBlock;
        }
    }
}
