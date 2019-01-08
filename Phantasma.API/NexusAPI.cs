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
    public class APIDescriptionAttribute : Attribute
    {
        public readonly string Description;

        public APIDescriptionAttribute(string description)
        {
            Description = description;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class APIFailCaseAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIFailCaseAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
    public class APIParameterAttribute : APIDescriptionAttribute
    {
        public readonly string Value;

        public APIParameterAttribute(string description, string value) : base(description)
        {
            Value = value;
        }
    }

    public class APIInfoAttribute : APIDescriptionAttribute
    {
        public readonly Type ReturnType;

        public APIInfoAttribute(Type returnType, string description): base(description)
        {
            ReturnType = returnType;
        }
    }

    public struct APIValue
    {
        public readonly Type Type;
        public readonly string Name;
        public readonly string Description;
        public readonly string Value; // example value

        public APIValue(Type type, string name, string description, string value)
        {
            Type = type;
            Name = name;
            Description = description;
            Value = value;
        }
    }

    public struct APIEntry
    {
        public readonly string Name;
        public readonly List<APIValue> Parameters;

        public readonly Type ReturnType;
        public readonly string Description;

        public readonly APIFailCaseAttribute[] FailCases;

        private readonly NexusAPI _api;
        private readonly MethodInfo _info;

        public APIEntry(NexusAPI api, MethodInfo info)
        {
            _api = api;
            _info = info;
            Name = info.Name;

            var parameters = info.GetParameters();
            Parameters = new List<APIValue>();
            foreach (var entry in parameters)
            {
                string description;
                string value;
                try
                {
                    var descAttr = entry.GetCustomAttribute<APIParameterAttribute>();
                    description = descAttr.Description;
                    value = descAttr.Value;
                }
                catch
                {
                    description = "TODO document me";
                    value = "TODO document me";
                }

                Parameters.Add(new APIValue(entry.ParameterType, entry.Name, description, value));
            }

            try
            {
                FailCases = info.GetCustomAttributes<APIFailCaseAttribute>().ToArray();
            }
            catch
            {
                FailCases = new APIFailCaseAttribute[0];
            }

            try
            {
                var attr = info.GetCustomAttribute<APIInfoAttribute>();
                ReturnType = attr.ReturnType;
                Description = attr.Description;
            }
            catch
            {
                ReturnType = null;
                Description = "TODO document me";
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public IAPIResult Execute(params string[] input)
        {
            if (input.Length != Parameters.Count)
            {
                throw new Exception("Unexpected number of arguments");
            }

            var args = new object[input.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (Parameters[i].Type == typeof(string))
                {
                    args[i] = input[i];
                }
                else
                if (Parameters[i].Type == typeof(uint))
                {
                    args[i] = uint.Parse(input[i]); // TODO error handling
                }
                else
                if (Parameters[i].Type == typeof(int))
                {
                    args[i] = int.Parse(input[i]); // TODO error handling
                }
                else
                {
                    throw new Exception("API invalid parameter type: " + Parameters[i].Type.FullName);
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
                hash = tx.Hash.ToString(),
                chainAddress = chain.Address.Text,
                timestamp = block.Timestamp.Value,
                blockHeight = block.Height,
                script = tx.Script.Encode()
            };

            var eventList = new List<EventResult>();

            var evts = block.GetEventsForTransaction(tx.Hash);
            foreach (var evt in evts)
            {
                var eventEntry = new EventResult
                {
                    address = evt.Address.Text,
                    data = evt.Data.Encode(),
                    kind = evt.Kind.ToString()
                };
                eventList.Add(eventEntry);
            }
            result.events = eventList.ToArray();

            return result;
        }

        private BlockResult FillBlock(Block block, Chain chain)
        {
            var result = new BlockResult
            {
                hash = block.Hash.ToString(),
                previousHash = block.PreviousHash.ToString(),
                timestamp = block.Timestamp.Value,
                height = block.Height,
                chainAddress = chain.Address.ToString(),
                payload = block.Payload != null ? block.Payload.Encode() : new byte[0].Encode(),
                reward = chain.GetBlockReward(block).ToString(),
                validatorAddress = Nexus.FindValidatorForBlock(block).ToString(),
            };

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
            result.txs = txs.ToArray();

            // todo add other block info, eg: size, gas, txs
            return result;
        }

        private ChainResult FillChain(Chain chain)
        {
            var result = new ChainResult
            {
                name = chain.Name,
                address = chain.Address.Text,
                height = chain.BlockHeight,
                parentAddress = chain.ParentChain != null ? chain.ParentChain.Address.ToString() : ""
            };

            return result;
        }

        private IAPIResult GetBlockHeight(Chain chain)
        {
            if (chain != null)
            {
                return new SingleResult { value = chain.BlockHeight };
            }

            return new ErrorResult { error = "chain not found" };
        }

        private Chain FindChainByInput(string chainInput)
        {
            var chain = Nexus.FindChainByName(chainInput);

            if (chain != null)
            {
                return chain;
            }

            if (Address.IsValidAddress(chainInput))
            {
                return Nexus.FindChainByAddress(Address.FromText(chainInput));
            }

            return null;
        }

        #endregion

        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.")]
        [APIFailCaseAttribute("address is invalid", "ABCD123")]
        public IAPIResult GetAccount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText)
        {
            if (!Address.IsValidAddress(addressText))
            {
                return new ErrorResult { error = "invalid address" };
            }

            var result = new AccountResult();
            var address = Address.FromText(addressText);
            result.address = address.Text;
            result.name = Nexus.LookUpAddress(address);

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
                            chain = chain.Name,
                            amount = balance.ToString(),
                            symbol = token.Symbol,
                            ids = null
                        };

                        if (!token.IsFungible)
                        {
                            var idList = chain.GetTokenOwnerships(token).Get(address);
                            if (idList != null && idList.Any())
                            {
                                balanceEntry.ids = idList.Select(x => x.ToString()).ToArray();
                            }
                        }
                        balanceList.Add(balanceEntry);
                    }
                }
            }
            result.balances = balanceList.ToArray();

            return result;
        }

        [APIInfo(typeof(int), "Returns the height of a chain.")]
        [APIFailCaseAttribute("chain is invalid", "4533")]
        public IAPIResult GetBlockHeightFromChain([APIParameter("Address or name of chain", "root")] string chainInput)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null) return new ErrorResult { error = "invalid name" };

            return GetBlockHeight(chain);
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.")]
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        public IAPIResult GetBlockTransactionCountByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
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
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        public IAPIResult GetBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
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
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        public IAPIResult GetRawBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
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
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        [APIFailCaseAttribute("chain is invalid", "453dsa")]
        public IAPIResult GetBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
        {
            var chain = FindChainByInput(chainInput);

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
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        [APIFailCaseAttribute("chain is invalid", "453dsa")]
        public IAPIResult GetRawBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
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
        [APIFailCaseAttribute("block hash is invalid", "asdfsa")]
        [APIFailCaseAttribute("index transaction is invalid", "-1")]
        public IAPIResult GetTransactionByBlockHashAndIndex([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash, [APIParameter("Index of transaction", "0")] int index)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var block = Nexus.FindBlockByHash(hash);

                if (block == null)
                {
                    return new ErrorResult { error = "unknown block hash" };
                }

                if (index < 0 || index >= block.TransactionCount)
                {
                    return new ErrorResult { error = "invalid transaction index" };
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
        [APIFailCaseAttribute("address is invalid", "543533")]
        [APIFailCaseAttribute("amount to return is invalid", "-1")]
        public IAPIResult GetAddressTransactions([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Amount of transactions to return", "5")] int amountTx)
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

                result.address = address.Text;

                var txs = plugin?.GetAddressTransactions(address).
                    Select(hash => Nexus.FindTransactionByHash(hash)).
                    OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value).
                    Take(amountTx);

                result.amount = (uint)txs.Count();
                result.txs = txs.Select(FillTransaction).ToArray();

                return result;
            }
            else
            {
                return new ErrorResult() { error = "invalid address" };
            }
        }

        [APIInfo(typeof(int), "Get number of transactions in a specific address and chain")]
        [APIFailCaseAttribute("address is invalid", "43242342")]
        [APIFailCaseAttribute("chain is invalid", "-1")]
        public IAPIResult GetAddressTransactionCount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Name or address of chain, optional", "root")] string chainInput)
        {
            if (Address.IsValidAddress(addressText))
            {
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
                int count = 0;

                if (!string.IsNullOrEmpty(chainInput))
                {
                    var chain = FindChainByInput(chainInput);
                    if (chain == null)
                    {
                        return new ErrorResult() { error = "invalid chain" };
                    }

                    count = plugin.GetAddressTransactions(address).Count(tx => Nexus.FindBlockForHash(tx).ChainAddress.Equals(chain.Address));
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
        [APIFailCaseAttribute("hash is invalid", "asdfsa")]
        public IAPIResult GetConfirmations([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
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
                    result.confirmations = confirmations;
                    result.hash = block.Hash.ToString();
                    result.height = block.Height;
                    result.chainAddress = chain.Address.Text;
                }

                return result;
            }

            return new ErrorResult() { error = "invalid hash" };
        }

        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually.")]
        [APIFailCaseAttribute("rejected by mempool", "0000")] // TODO not correct
        [APIFailCaseAttribute("script is invalid", "")]
        [APIFailCaseAttribute("failed to decoded transaction", "0000")]
        public IAPIResult SendRawTransaction([APIParameter("Serialized transaction bytes, in hexadecimal format", "0000000000")] string txData)
        {
            if (Mempool == null)
            {
                return new ErrorResult { error = "No mempool" };
            }

            var bytes = Base16.Decode(txData);
            if (bytes.Length == 0)
            {
                return new ErrorResult { error = "Invalid transaction script" };
            }

            var tx = Transaction.Unserialize(bytes);
            if (tx == null)
            {
                return new ErrorResult { error = "Failed to decode transaction" };
            }

            bool submited = Mempool.Submit(tx);
            if (!submited)
            {
                return new ErrorResult { error = "Mempool submission rejected" };
            }

            return new SingleResult { value = tx.Hash.ToString() };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.")]
        [APIFailCaseAttribute("hash is invalid", "43242342")]
        public IAPIResult GetTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
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

        [APIInfo(typeof(ChainResult[]), "Returns an array of all chains deployed in Phantasma.")]
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

        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.")]
        public IAPIResult GetTokens()
        {
            var tokenList = new List<object>();

            foreach (var token in Nexus.Tokens)
            {
                var entry = new TokenResult
                {
                    symbol = token.Symbol,
                    name = token.Name,
                    currentSupply = token.CurrentSupply.ToString(),
                    maxSupply = token.MaxSupply.ToString(),
                    decimals = token.Decimals,
                    isFungible = token.IsFungible,
                    ownerAddress = token.Owner.Text
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
                    description = appInfo.description,
                    icon = appInfo.icon.ToString(),
                    id = appInfo.id,
                    title = appInfo.title,
                    url = appInfo.url
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
                name = Nexus.RootChain.Name,
                address = Nexus.RootChain.Address.ToString(),
                height = Nexus.RootChain.BlockHeight
            };

            return result;
        }

        [APIInfo(typeof(TransactionResult[]), "Returns last X transactions of given token.")]
        [APIFailCaseAttribute("token symbol is invalid", "43242342")]
        [APIFailCaseAttribute("amount is invalid", "-1")]
        public IAPIResult GetTokenTransfers([APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Amount of transactions to return", "5")] int amount)
        {
            if (amount < 0)
            {
                return new ErrorResult { error = "Invalid amount" };
            }

            var token = Nexus.FindTokenBySymbol(tokenSymbol);
            if (token == null)
            {
                return new ErrorResult { error = "Invalid token" };
            }

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
        [APIFailCaseAttribute("token symbol is invalid", "43242342")]
        public IAPIResult GetTokenTransferCount([APIParameter("Token symbol", "SOUL")] string tokenSymbol)
        {
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txCount = plugin.GetTokenTransactions(tokenSymbol).Count();

            return new SingleResult() { value = txCount };
        }

        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.")]
        [APIFailCaseAttribute("address is invalid", "43242342")]
        [APIFailCaseAttribute("token is invalid", "-1")]
        [APIFailCaseAttribute("chain is invalid", "-1re")]
        public IAPIResult GetTokenBalance([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Address or name of chain", "root")] string chainInput) //todo rest
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

            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                return new ErrorResult { error = "invalid chain" };
            }

            var address = Address.FromText(addressText);
            var balance = chain.GetTokenBalance(token, address);

            var result = new BalanceResult()
            {
                amount = balance.ToString(),
                symbol = tokenSymbol,
                chain = chain.Address.Text
            };

            if (!token.IsFungible)
            {
                var idList = chain.GetTokenOwnerships(token).Get(address);
                if (idList != null && idList.Any())
                {
                    result.ids = idList.Select(x => x.ToString()).ToArray();
                }
            }

            return result;
        }
    }
}
