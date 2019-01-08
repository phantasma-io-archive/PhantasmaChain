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
    public class APIInfoAttribute : Attribute
    {
        public Type ReturnType;
        public string Description;

        public APIInfoAttribute(Type returnType, string description)
        {
            ReturnType = returnType;
            Description = description;
        }
    }

    public struct APIEntry
    {
        public readonly string Name;
        public readonly KeyValuePair<Type, string>[] Parameters;

        public readonly Type ReturnType;
        public readonly string Description;

        private readonly NexusAPI _api;
        private readonly MethodInfo _info;

        public APIEntry(NexusAPI api, MethodInfo info)
        {
            _api = api;
            _info = info;
            Name = info.Name;
            Parameters = info.GetParameters().Select(x => new KeyValuePair<Type, string>(x.ParameterType, x.Name)).ToArray();

            try
            {
                var attr = info.GetCustomAttribute<APIInfoAttribute>();
                ReturnType = attr.ReturnType;
                Description = attr.Description;
            }
            catch
            {
                ReturnType = null;
                Description = "not available";
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public IAPIResult Execute(params string[] input)
        {
            if (input.Length != Parameters.Length)
            {
                throw new Exception("Unexpected number of arguments");
            }

            var args = new object[input.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (Parameters[i].Key == typeof(string))
                {
                    args[i] = input[i];
                }
                else
                if (Parameters[i].Key == typeof(uint))
                {
                    args[i] = uint.Parse(input[i]); // TODO error handling
                }
                else
                if (Parameters[i].Key == typeof(int))
                {
                    args[i] = int.Parse(input[i]); // TODO error handling
                }
                else
                {
                    throw new Exception("API invalid parameter type: " + Parameters[i].Key.FullName);
                }
            }

            return (IAPIResult)_info.Invoke(_api, args);
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

            var methodInfo = GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var entry in methodInfo)
            {
                if (entry.ReturnType != typeof(IAPIResult))
                {
                    continue;
                }

                var temp = new APIEntry(this, entry);
                if (temp.ReturnType != null)
                {
                    _methods[entry.Name.ToLower()] = temp;
                }
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

            var result = new TransactionResult
            {
                Txid = tx.Hash.ToString(),
                ChainAddress = chain.Address.Text,
                ChainName = chain.Name,
                Timestamp = block.Timestamp.Value,
                BlockHeight = block.Height,
                Script = tx.Script.Encode()
            };

            var eventList = new List<EventResult>();

            var evts = block.GetEventsForTransaction(tx.Hash);
            foreach (var evt in evts)
            {
                var eventEntry = new EventResult
                {
                    Address = evt.Address.Text,
                    Data = evt.Data.Encode(),
                    Kind = evt.Kind.ToString()
                };
                eventList.Add(eventEntry);
            }
            result.Events = eventList.ToArray();

            return result;
        }

        private BlockResult FillBlock(Block block, Chain chain)
        {
            var result = new BlockResult
            {
                Hash = block.Hash.ToString(),
                PreviousHash = block.PreviousHash.ToString(),
                Timestamp = block.Timestamp.Value,
                Height = block.Height,
                ChainAddress = block.ChainAddress.ToString()
            };

            var minerAddress = Nexus.FindValidatorForBlock(block);
            result.MinerAddress = minerAddress.Text;
            result.Nonce = block.Nonce;
            result.Reward = chain.GetBlockReward(block).ToString();
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

        private ChainResult FillChain(Chain chain, bool fillChildren)
        {
            var result = new ChainResult
            {
                Name = chain.Name,
                Address = chain.Address.Text,
                Height = chain.BlockHeight,
                ParentAddress = chain.ParentChain?.Name
            };

            if (fillChildren)
            {
                var children = new List<ChainResult>();
                if (chain.ChildChains != null && chain.ChildChains.Any())
                {
                    foreach (var childChain in chain.ChildChains)
                    {
                        var child = FillChain(chain, true);
                        children.Add(child);
                    }

                    result.Children = children.ToArray();
                }
            }

            return result;
        }
        #endregion

        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.")]
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

            var balanceList = new List<BalanceResult>();
            foreach (var token in Nexus.Tokens)
            {
                foreach (var chain in Nexus.Chains)
                {
                    var balance = chain.GetTokenBalance(token, address);
                    if (balance > 0)
                    {
                        var balanceEntry = new BalanceResult
                        {
                            Chain = chain.Name,
                            Amount = balance.ToString(),
                            Symbol = token.Symbol,
                            Ids = null
                        };

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

            return new ErrorResult { error = "invalid address" };
        }

        public IAPIResult GetBlockHeightFromChainName(string chainName)
        {
            var chain = Nexus.FindChainByName(chainName);

            if (chain == null) return new ErrorResult { error = "invalid name" };

            return GetBlockHeight(chain);
        }

        [APIInfo(typeof(uint), "Returns the height of most recent block of given chain.")]
        private IAPIResult GetBlockHeight(Chain chain)
        {
            if (chain != null)
            {
                return new SingleResult { value = chain.BlockHeight };
            }

            return new ErrorResult { error = "chain not found" };
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.")]
        public IAPIResult GetBlockTransactionCountByHash(string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = Nexus.FindBlockByHash(hash);

                if (block != null)
                {
                    int count = block.TransactionHashes.Count();

                    return new SingleResult { value = count };
                }
            }

            return new ErrorResult { error = "invalid block hash" };
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by hash.")]
        public IAPIResult GetBlockByHash(string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                foreach (var chain in Nexus.Chains)
                {
                    var block = chain.FindBlockByHash(hash);
                    if (block != null)
                    {
                        return FillBlock(block, chain);
                    }
                }
            }

            return new ErrorResult { error = "invalid block hash" };
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block (encoded) by hash.")]
        public IAPIResult GetRawBlockByHash(string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                foreach (var chain in Nexus.Chains)
                {
                    var block = chain.FindBlockByHash(hash);
                    if (block != null)
                    {
                        return new SingleResult() { value = block.ToByteArray().Encode() };
                    }
                }
            }

            return new ErrorResult() { error = "invalid block hash" };
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by height and chain.")]
        public IAPIResult GetBlockByHeight(string chainInput, uint height)
        {
            var chain = Nexus.FindChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    return new ErrorResult { error = "chain not found" };
                }
                chain = Nexus.FindChainByAddress(Address.FromText(chainInput));
            }

            if (chain == null)
            {
                return new ErrorResult { error = "chain not found" };
            }

            var block = chain.FindBlockByHeight(height);

            if (block != null)
            {
                return FillBlock(block, chain);
            }

            return new ErrorResult { error = "block not found" };
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by height and chain.")]
        public IAPIResult GetRawBlockByHeight(string chainInput, uint height)
        {
            var chain = Nexus.FindChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    return new ErrorResult { error = "chain not found" };
                }
                chain = Nexus.FindChainByAddress(Address.FromText(chainInput));
            }

            if (chain == null)
            {
                return new ErrorResult { error = "chain not found" };
            }

            var block = chain.FindBlockByHeight(height);

            if (block != null)
            {
                return new SingleResult { value = block.ToByteArray().Encode() };
            }

            return new ErrorResult { error = "block not found" };
        }

        [APIInfo(typeof(TransactionResult), "Returns the information about a transaction requested by a block hash and transaction index.")]
        public IAPIResult GetTransactionByBlockHashAndIndex(string blockHash, int index)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = Nexus.FindBlockByHash(hash);

                if (block == null)
                {
                    return new ErrorResult { error = "unknown block hash" };
                }

                var txHash = block.TransactionHashes.ElementAtOrDefault(index);

                if (txHash == null)
                {
                    return new ErrorResult { error = "unknown tx index" };
                }

                return FillTransaction(Nexus.FindTransactionByHash(txHash));
            }

            return new ErrorResult { error = "invalid block hash" };
        }

        [APIInfo(typeof(AccountTransactionsResult), "Returns last X transactions of given address.")]
        public IAPIResult GetAddressTransactions(string addressText, int amountTx)
        {
            if (amountTx < 1)
            {
                return new ErrorResult { error = "invalid amount" };
            }

            var result = new AccountTransactionsResult();
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();

                result.Address = address.Text;

                var txs = plugin?.GetAddressTransactions(address).
                    Select(hash => Nexus.FindTransactionByHash(hash)).
                    OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value).
                    Take(amountTx);

                result.Amount = (uint)txs.Count();
                result.Txs = txs.Select(FillTransaction).ToArray();

                return result;
            }
            else
            {
                return new ErrorResult() { error = "invalid address" };
            }
        }

        [APIInfo(typeof(int), "TODO document me")]
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

                return new SingleResult() { value = count };
            }

            return new ErrorResult() { error = "invalid address" };
        }

        [APIInfo(typeof(int), "Returns the number of confirmations of given transaction hash and other useful info.")]
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

        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually.")]
        public IAPIResult SendRawTransaction(string txData)
        {
            if (Mempool == null)
            {
                return new ErrorResult { error = "No mempool" };
            }

            var bytes = Base16.Decode(txData);
            var tx = Transaction.Unserialize(bytes);

            bool submited = Mempool.Submit(tx);
            if (!submited)
            {
                return new ErrorResult { error = "Mempool submission rejected" };
            }

            return new SingleResult { value = tx.Hash.ToString() };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.")]
        public IAPIResult GetTransaction(string hashText)
        {
            if (Hash.TryParse(hashText, out var hash))
            {
                var tx = Nexus.FindTransactionByHash(hash);

                if (tx != null)
                {
                    return FillTransaction(tx);
                }
            }

            return new ErrorResult { error = "Invalid hash" };
        }

        [APIInfo(typeof(ChainResult[]), "Returns an array of chains with useful information.")]
        public IAPIResult GetChains()
        {
            var result = new ArrayResult();

            var objs = new List<object>();

            foreach (var chain in Nexus.Chains)
            {
                var single = FillChain(chain, false);
                objs.Add(single);
            }

            result.values = objs.ToArray();
            return result;
        }

        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.")]
        public IAPIResult GetTokens()
        {
            var tokenList = new List<object>();

            foreach (var token in Nexus.Tokens)
            {
                var entry = new TokenResult
                {
                    Symbol = token.Symbol,
                    Name = token.Name,
                    CurrentSupply = token.CurrentSupply.ToString(),
                    MaxSupply = token.MaxSupply.ToString(),
                    Decimals = token.Decimals,
                    IsFungible = token.IsFungible,
                    Owner = token.Owner.Text
                };
                //tokenNode.AddField("flags", token.Flags);
                tokenList.Add(entry);
            }

            return new ArrayResult() { values = tokenList.ToArray() };
        }


        [APIInfo(typeof(AppResult[]), "Returns an array of apps deployed in Phantasma.")]
        public IAPIResult GetApps()
        {
            var appList = new List<object>();

            var appChain = Nexus.FindChainByName("apps");
            var apps = (AppInfo[])appChain.InvokeContract("apps", "GetApps", new string[] { });

            foreach (var appInfo in apps)
            {
                var entry = new AppResult
                {
                    Description = appInfo.description,
                    Icon = appInfo.icon.ToString(),
                    Id = appInfo.id,
                    Title = appInfo.title,
                    Url = appInfo.url
                };
                appList.Add(entry);
            }

            return new ArrayResult() { values = appList.ToArray() };
        }

        [APIInfo(typeof(RootChainResult), "Returns information about the root chain.")]
        public IAPIResult GetRootChain()
        {
            var result = new RootChainResult
            {
                Name = Nexus.RootChain.Name,
                Address = Nexus.RootChain.Address.ToString(),
                Height = Nexus.RootChain.BlockHeight
            };

            return result;
        }

        [APIInfo(typeof(TransactionResult[]), "Returns last X transactions of given token.")]
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

            return new ArrayResult { values = txList.ToArray() };
        }

        [APIInfo(typeof(int), "Returns the number of transaction of a given token.")]
        public IAPIResult GetTokenTransferCount(string tokenSymbol)
        {
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txCount = plugin.GetTokenTransactions(tokenSymbol).Count();

            return new SingleResult() { value = txCount };
        }

        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.")]
        public IAPIResult GetTokenBalance(string addressText, string tokenSymbol, string chainInput) //todo rest
        {
            if (!Address.IsValidAddress(addressText))
            {
                return new ErrorResult { error = "invalid address" };
            }

            Token token = Nexus.FindTokenBySymbol(tokenSymbol);

            if (token == null)
            {
                return new ErrorResult { error = "invalid token" };
            }

            var chain = Nexus.FindChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    return new ErrorResult { error = "invalid chain address" };
                }

                chain = Nexus.FindChainByAddress(Address.FromText(chainInput));
                if (chain == null)
                {
                    return new ErrorResult { error = "invalid chain" };
                }
            }

            var address = Address.FromText(addressText);
            var balance = chain.GetTokenBalance(token, address);

            var result = new BalanceResult()
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
    }
}
