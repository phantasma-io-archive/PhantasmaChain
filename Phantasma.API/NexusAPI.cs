using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
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

        public IAPIResult Execute(params string[] input)
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

            return (IAPIResult) Info.Invoke(API, args);
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
                if (entry.ReturnType != typeof(IAPIResult))
                {
                    continue;
                }

                _methods[entry.Name.ToLower()] = new APIEntry(this, entry);
            }
        }

        public IAPIResult Execute(string methodName, string[] args)
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
        private TransactionResult FillTransaction(Transaction tx)
        {
            var block = Nexus.FindBlockForTransaction(tx);
            var chain = Nexus.FindChainForBlock(block.Hash);

            var result = new TransactionResult();
            result.Txid = tx.Hash.ToString();
            result.ChainAddress = chain.Address.Text;
            result.ChainName = chain.Name;
            result.Timestamp = block.Timestamp.Value;
            result.BlockHeight = block.Height;
            result.Script = tx.Script.Encode();

            var eventList =new List<EventResult>();

            var evts = block.GetEventsForTransaction(tx.Hash);
            foreach (var evt in evts)
            {
                var eventEntry = new EventResult();
                eventEntry.Address = evt.Address.Text;
                eventEntry.Data = evt.Data.Encode();
                eventEntry.Kind = evt.Kind.ToString();
                eventList.Add(eventEntry);
            }
            result.Events = eventList.ToArray();

            return result;
        }

        private BlockResult FillBlock(Block block, Chain chain)
        {
            //var chain = Nexus.FindChainForBlock(block.Hash);
            var result = new BlockResult();

            result.Hash = block.Hash.ToString();
            result.PreviousHash = block.PreviousHash.ToString();
            result.Timestamp = block.Timestamp.Value;
            result.Height = block.Height;
            result.ChainAddress = block.ChainAddress.ToString();

            var minerAddress = Nexus.FindValidatorForBlock(block);
            result.MinerAddress = minerAddress.Text;
            result.Nonce = block.Nonce;
            result.Reward = TokenUtils.ToDecimal(chain.GetBlockReward(block), Nexus.NativeTokenDecimals);
            result.Payload = block.Payload != null ? block.Payload.Encode() : new byte[0].Encode(); //todo make sure this is ok

            var txs = new List<TransactionResult>();
            if (block.TransactionHashes != null && block.TransactionHashes.Any())
            {
                foreach (var transactionHash in block.TransactionHashes)
                {
                    var tx = Nexus.FindTransactionByHash(transactionHash);
                    var txEntry = FillTransaction(tx);
                    txs.Add(txEntry);
                }
            }
            result.Txs = txs.ToArray();

            // todo add other block info, eg: size, gas, txs

            return result;
        }

        private ChainResult FillChain(Chain chain)
        {
            var result = new ChainResult();

            result.Name = chain.Name;
            result.Address = chain.Address.Text;
            result.Height = chain.BlockHeight;

            result.ParentAddress = (chain.ParentChain != null) ? chain.ParentChain.Name : null;
            
            var children = new List<ChainResult>();
            if (chain.ChildChains != null && chain.ChildChains.Any())
            {
                foreach (var childChain in chain.ChildChains)
                {
                    var child = FillChain(chain);
                    children.Add(child);
                }

                result.Children = children.ToArray();
            }

            return result;
        }
        #endregion

        public IAPIResult GetAccount(string addressText)
        {
            if (!Address.IsValidAddress(addressText))
            {
                return new ErrorResult() { error = "invalid address" };
            }

            var result = new AccountResult();
            var address = Address.FromText(addressText);
            result.Address = address.Text;
            result.Name = Nexus.LookUpAddress(address);

            var balanceList = new List<BalanceSheetResult>();
            foreach (var token in Nexus.Tokens)
            {
                foreach (var chain in Nexus.Chains)
                {
                    var balance = chain.GetTokenBalance(token, address);
                    if (balance > 0)
                    {
                        var balanceEntry = new BalanceSheetResult();

                        balanceEntry.Chain = chain.Name;
                        balanceEntry.Amount = balance.ToString();
                        balanceEntry.Symbol = token.Symbol;
                        balanceEntry.Ids = null;
                        if (!token.IsFungible)
                        {
                            var idList = chain.GetTokenOwnerships(token).Get(address);
                            if (idList != null && idList.Any())
                            {
                                balanceEntry.Ids = idList.Select(x => x.ToString()).ToArray();
                            }
                        }
                        balanceList.Add(balanceEntry);
                    }
                }
            }
            result.Balances = balanceList.ToArray();

            return result;
        }

        public IAPIResult GetBlockHeightFromChainAddress(string chainAddress)
        {
            if (Address.IsValidAddress(chainAddress))
            {
                var chain = Nexus.FindChainByAddress(Address.FromText(chainAddress));
                return GetBlockHeight(chain);
            }

            return new ErrorResult() { error = "invalid address" };
        }

        public IAPIResult GetBlockHeightFromChainName(string chainName)
        {
            var chain = Nexus.FindChainByName(chainName);
            return GetBlockHeight(chain);
        }

        private IAPIResult GetBlockHeight(Chain chain)
        {
            if (chain != null)
            {
                return new SingleResult() { value = chain.BlockHeight.ToString() };
            }
            else
            {
                return new ErrorResult() { error = "chain not found" };
            }
        }

        public IAPIResult GetBlockTransactionCountByHash(string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var temp = Nexus.FindBlockByHash(hash)?.TransactionHashes.Count();
                var count = (temp != null) ? temp.ToString() : "0";
                return new SingleResult() { value = count };
            }
            else
            {
                return new ErrorResult() { error = "invalid block hash" };
            }
        }

        public IAPIResult GetBlockByHash(string blockHash, int serialized = 0)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                foreach (var chain in Nexus.Chains)
                {
                    var block = chain.FindBlockByHash(hash);
                    if (block != null)
                    {
                        if (serialized == 0) // TODO why is this not a bool?
                        {
                            return FillBlock(block, chain);
                        }

                        return new SingleResult() { value = SerializedBlock(block) };
                    }
                }
            }

            return new ErrorResult() { error = "invalid block hash" };
        }

        public IAPIResult GetBlockByHeight(string chainName, uint height, int serialized = 0)
        {
            var chain = Nexus.FindChainByName(chainName);
            if (chain == null) return null;
            return GetBlockByHeight(chain, height, serialized);
        }

        public IAPIResult GetBlockByHeight(Address chainAddress, uint height, int serialized = 0)
        {
            var chain = Nexus.FindChainByAddress(chainAddress);
            return GetBlockByHeight(chain, height, serialized);
        }

        private IAPIResult GetBlockByHeight(Chain chain, uint height, int serialized)
        {
            var block = chain?.FindBlockByHeight(height);
            if (block != null)
            {
                if (serialized == 0)
                {
                    return FillBlock(block, chain);
                }

                return new SingleResult() { value = SerializedBlock(block) };
            }

            return new ErrorResult() { error = "block not found" };
        }

        public IAPIResult GetTransactionByBlockHashAndIndex(string blockHash, int index)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = Nexus.FindBlockByHash(hash);
                if (block == null)
                {
                    return new ErrorResult() { error = "unknown block hash" };
                }

                var txHash = block.TransactionHashes.ElementAt(index);
                if (txHash == null)
                {
                    return new ErrorResult() { error = "unknown tx index" };
                }

                return FillTransaction(Nexus.FindTransactionByHash(txHash));
            }

            return new ErrorResult() { error = "invalid block hash" };
        }

        public IAPIResult GetAddressTransactions(string addressText, int amountTx)
        {
            var result = new AccountTransactionsResult();
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();

                result.Address = address.Text;
                result.Amount = amountTx;
                var txs = plugin?.GetAddressTransactions(address).
                    Select(hash => Nexus.FindTransactionByHash(hash)).
                    OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value).
                    Take(amountTx);

                if (txs != null)
                {
                    result.Txs = txs.Select(tx => FillTransaction(tx)).ToArray();
                }

                return result;
            }
            else
            {
                return new ErrorResult() { error = "invalid address" };
            }
        }

        public IAPIResult GetAddressTransactionCount(string addressText, string chainText)
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

                return new SingleResult() { value = count.ToString() };
            }

            return new ErrorResult() { error = "invalid address" };
        }

        public IAPIResult GetConfirmations(string hashText)
        {
            var result = new TxConfirmationResult();
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
                    return new ErrorResult() { error = "unknown hash" };
                }
                else
                {
                    result.Confirmations = confirmations;
                    result.Hash = block.Hash.ToString();
                    result.Height = block.Height;
                    result.Chain = chain.Address.Text;
                }

                return result;
            }

            return new ErrorResult() { error = "invalid hash" };
        }

        public IAPIResult SendRawTransaction(string txData)
        {
            if (Mempool == null)
            {
                return new ErrorResult() { error = "No mempool" };
            }

            var bytes = Base16.Decode(txData);
            var tx = Transaction.Unserialize(bytes);

            bool submited = Mempool.Submit(tx);
            if (!submited)
            {
                return new ErrorResult() { error = "Mempool submission rejected" };
            }

            return new SingleResult() { value = tx.Hash.ToString() };
        }

        public IAPIResult GetChains()
        {
            var result = new ArrayResult();

            var objs = new List<object>();

            foreach (var chain in Nexus.Chains)
            {
                var single = FillChain(chain);
                objs.Add(single);
            }

            result.values = objs.ToArray();
            return result;
        }

        public IAPIResult GetTransaction(string hashText)
        {
            Hash hash;
            
            if (Hash.TryParse(hashText, out hash))
            {
                var tx = Nexus.FindTransactionByHash(hash);
                return FillTransaction(tx);
            }
            else
            {
                return new ErrorResult() { error = "Invalid hash" };
            }
        }

        public IAPIResult GetTokens()
        {
            var tokenList = new List<object>();

            foreach (var token in Nexus.Tokens)
            {
                var entry = new TokenResult();
                entry.Symbol = token.Symbol;
                entry.Name = token.Name;
                entry.CurrentSupply = token.CurrentSupply.ToString();
                entry.MaxSupply = token.MaxSupply.ToString();
                entry.Decimals = token.Decimals;
                entry.IsFungible = token.IsFungible;
                //tokenNode.AddField("flags", token.Flags);
                entry.Owner = token.Owner.Text;
                tokenList.Add(entry);
            }

            return new ArrayResult() { values = tokenList.ToArray() };
        }


        public IAPIResult GetApps()
        {
            var appList = new List<object>();

            var appChain = Nexus.FindChainByName("apps");
            var apps = (AppInfo[])appChain.InvokeContract("apps", "GetApps", new string[] { });

            foreach (var appInfo in apps)
            {
                var entry = new AppResult();
                entry.Description = appInfo.description;
                entry.Icon = appInfo.icon.ToString();
                entry.Id = appInfo.id;
                entry.Title = appInfo.title;
                entry.Url = appInfo.url;
                appList.Add(entry);
            }

            return new ArrayResult() { values = appList.ToArray() };
        }

        public IAPIResult GetRootChain()
        {
            var result = new RootChainResult();
            result.Name = Nexus.RootChain.Name;
            result.Address = Nexus.RootChain.Address.ToString();
            result.Height = Nexus.RootChain.BlockHeight;
            return result;
        }

        public IAPIResult GetTokenTransfers(string tokenSymbol, int amount)
        {
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txsHash = plugin.GetTokenTransactions(tokenSymbol);
            int count = 0;

            var txList = new List<object>();
            foreach (var hash in txsHash)
            {
                var tx = Nexus.FindTransactionByHash(hash);
                if (tx != null)
                {
                    txList.Add(FillTransaction(tx));
                    count++;

                    if (count == amount) break;
                }
            }

            return new ArrayResult() { values = txList.ToArray() };
        }

        public IAPIResult GetTokenTransferCount(string tokenSymbol)
        {
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txCount = plugin.GetTokenTransactions(tokenSymbol).Count();
            return new SingleResult() { value = txCount };
        }

        public IAPIResult GetTokenBalance(string addressText, string tokenSymbol, string chainInput) //todo rest
        {
            if (!Address.IsValidAddress(addressText))
            {
                return new ErrorResult() { error = "invalid address" };
            }

            Token token = Nexus.FindTokenBySymbol(tokenSymbol);

            if (token == null)
            {
                return new ErrorResult() { error = "invalid token" };
            }

            var chain = Nexus.FindChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    return new ErrorResult() { error = "invalid address" };
                }

                chain = Nexus.FindChainByAddress(Address.FromText(chainInput));
                if (chain == null)
                {
                    return new ErrorResult() { error = "invalid chain" };
                }
            }

            var address = Address.FromText(addressText);
            var balance = chain.GetTokenBalance(token, address);

            var result =  new BalanceSheetResult()
            {
                Amount = balance.ToString(),
                Symbol = tokenSymbol,
                Chain = chain.Address.Text
            };

            if (!token.IsFungible)
            {
                var idList = chain.GetTokenOwnerships(token).Get(address);
                if (idList != null && idList.Any())
                {
                    result.Ids = idList.Select(x => x.ToString()).ToArray();
                }
            }

            return result;
        }

        private string SerializedBlock(Block block)
        {
            return block.ToByteArray().Encode();
        }
    }
}
