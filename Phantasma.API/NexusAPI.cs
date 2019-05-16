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
using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Contracts;
using Phantasma.VM;
using Phantasma.IO;

namespace Phantasma.API
{
    public class APIException : Exception
    {
        public APIException(string msg) : base(msg)
        {

        }
    }

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
        public readonly bool Paginated;

        public APIInfoAttribute(Type returnType, string description, bool paginated = false) : base(description)
        {
            ReturnType = returnType;
            Paginated = paginated;
        }
    }

    public struct APIValue
    {
        public readonly Type Type;
        public readonly string Name;
        public readonly string Description;
        public readonly string ExampleValue; // example value
        public readonly object DefaultValue;
        public readonly bool HasDefaultValue;

        public APIValue(Type type, string name, string description, string exampleValue, object defaultValue, bool hasDefaultValue)
        {
            Type = type;
            Name = name;
            Description = description;
            ExampleValue = exampleValue;
            DefaultValue = defaultValue;
            HasDefaultValue = hasDefaultValue;
        }
    }

    public struct APIEntry
    {
        public readonly string Name;
        public readonly List<APIValue> Parameters;

        public readonly Type ReturnType;
        public readonly string Description;

        public readonly bool IsPaginated;

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
                string exampleValue;

                var descAttr = entry.GetCustomAttribute<APIParameterAttribute>();
                if (descAttr != null)
                {
                    description = descAttr.Description;
                    exampleValue = descAttr.Value;
                }
                else
                {
                    description = "TODO document me";
                    exampleValue = "TODO document me";
                }

                object defaultValue;

                if (entry.HasDefaultValue)
                {
                    defaultValue = entry.DefaultValue;
                }
                else
                {
                    defaultValue = null;
                }

                Parameters.Add(new APIValue(entry.ParameterType, entry.Name, description, exampleValue, defaultValue, entry.HasDefaultValue));
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
                IsPaginated = attr.Paginated;
            }
            catch
            {
                ReturnType = null;
                Description = "TODO document me";
                IsPaginated = false;
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public IAPIResult Execute(params object[] input)
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
                    args[i] = input[i] == null ? null : input[i].ToString();
                    continue;
                }

                if (Parameters[i].Type == typeof(uint))
                {
                    if (uint.TryParse(input[i].ToString(), out uint val))
                    {
                        args[i] = val;
                        continue;
                    }
                }

                if (Parameters[i].Type == typeof(int))
                {
                    if (int.TryParse(input[i].ToString(), out int val))
                    {
                        args[i] = val;
                        continue;
                    }
                }

                if (Parameters[i].Type == typeof(bool))
                {
                    if (bool.TryParse(input[i].ToString(), out bool val))
                    {
                        args[i] = val;
                        continue;
                    }
                }

                throw new APIException("invalid parameter type: " + Parameters[i].Name);
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

        private const int PaginationMaxResults = 50;

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

                if (entry.Name == nameof(Execute))
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

        public IAPIResult Execute(string methodName, object[] args)
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
        private TokenResult FillToken(Token token)
        {
            var metadata = (TokenMetadata[])Nexus.RootChain.InvokeContract("nexus", "GetTokenMetadataList", token.Symbol);
            var metadataResults = new List<TokenMetadataResult>();

            for (int i = 0; i < metadata.Length; i++)
            {
                metadataResults.Add(new TokenMetadataResult
                {
                    key = metadata[i].key,
                    value = metadata[i].value.Encode()
                });
            }

            return new TokenResult
            {
                symbol = token.Symbol,
                name = token.Name,
                currentSupply = token.CurrentSupply.ToString(),
                maxSupply = token.MaxSupply.ToString(),
                decimals = token.Decimals,
                flags = token.Flags.ToString(),//.Split(',').Select(x => x.Trim()).ToArray(),
                ownerAddress = token.Owner.Text,
                metadataList = metadataResults.ToArray()
            };
        }

        private AuctionResult FillAuction(MarketAuction auction)
        {
            return new AuctionResult
            {
                baseSymbol = auction.BaseSymbol,
                quoteSymbol = auction.QuoteSymbol,
                tokenId = auction.TokenID.ToString(),
                creatorAddress = auction.Creator.Text,
                price = auction.Price.ToString(),
                startDate = auction.StartDate.Value,
                endDate = auction.EndDate.Value
            };
        }

        private TransactionResult FillTransaction(Transaction tx)
        {
            var block = Nexus.FindBlockForTransaction(tx);
            var chain = Nexus.FindChainByAddress(block.ChainAddress);

            var result = new TransactionResult
            {
                hash = tx.Hash.ToString(),
                chainAddress = chain.Address.Text,
                timestamp = block.Timestamp.Value,
                blockHeight = block.Height,
                blockHash = block.Hash.ToString(),
                confirmations = Nexus.GetConfirmationsOfBlock(block),
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

            var txResult = block.GetResultForTransaction(tx.Hash);
            result.result = txResult != null ? Base16.Encode(txResult) : "";

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
        [APIFailCase("address is invalid", "ABCD123")]
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
                            decimals = (uint)token.Decimals,
                            ids = new string[0]
                        };

                        if (!token.IsFungible)
                        {
                            var idList = chain.GetTokenOwnerships(token).Get(chain.Storage, address);
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

        [APIInfo(typeof(string), "Returns the address that owns a given name.")]
        [APIFailCase("address is invalid", "ABCD123")]
        public IAPIResult LookUpName([APIParameter("Name of account", "blabla")] string name)
        {
            if (!AccountContract.ValidateAddressName(name))
            {
                return new ErrorResult { error = "invalid name" };
            }

            var address = Nexus.LookUpName(name);
            if (address == Address.Null)
            {
                return new ErrorResult { error = "name not owned" };
            }

            return new SingleResult() { value = address.Text };
        }

        [APIInfo(typeof(int), "Returns the height of a chain.")]
        [APIFailCase("chain is invalid", "4533")]
        public IAPIResult GetBlockHeight([APIParameter("Address or name of chain", "root")] string chainInput)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                return new ErrorResult { error = "invalid chain" };
            }

            return new SingleResult { value = chain.BlockHeight };
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.")]
        [APIFailCase("block hash is invalid", "asdfsa")]
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
        [APIFailCase("block hash is invalid", "asdfsa")]
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

        [APIInfo(typeof(string), "Returns a serialized string, containing information about a block by hash.")]
        [APIFailCase("block hash is invalid", "asdfsa")]
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
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
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

        [APIInfo(typeof(string), "Returns a serialized string, in hex format, containing information about a block by height and chain.")]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
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
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("index transaction is invalid", "-1")]
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

        [APIInfo(typeof(AccountTransactionsResult), "Returns last X transactions of given address.", true)]
        [APIFailCase("address is invalid", "543533")]
        [APIFailCase("page is invalid", "-1")]
        [APIFailCase("pageSize is invalid", "-1")]
        public IAPIResult GetAddressTransactions([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Index of page to return", "5")] uint page = 1, [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ErrorResult { error = "invalid page/pageSize" };
            }

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            if (Address.IsValidAddress(addressText))
            {
                var paginatedResult = new PaginatedResult();
                var address = Address.FromText(addressText);
                var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();

                // pagination
                uint numberRecords = (uint)plugin.GetAddressTransactions(address).Count();
                uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
                //

                var txs = plugin.GetAddressTransactions(address)
                    .Select(hash => Nexus.FindTransactionByHash(hash))
                    .OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value)
                    .Skip((int)((page - 1) * pageSize))
                    .Take((int)pageSize);

                var result = new AccountTransactionsResult
                {
                    address = address.Text,
                    txs = txs.Select(FillTransaction).ToArray()
                };

                paginatedResult.pageSize = pageSize;
                paginatedResult.totalPages = totalPages;
                paginatedResult.total = numberRecords;
                paginatedResult.page = page;

                paginatedResult.result = result;

                return paginatedResult;
            }
            else
            {
                return new ErrorResult() { error = "invalid address" };
            }
        }

        [APIInfo(typeof(int), "Get number of transactions in a specific address and chain")]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("chain is invalid", "-1")]
        public IAPIResult GetAddressTransactionCount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Name or address of chain, optional", "apps")] string chainInput = "main")
        {
            if (!Address.IsValidAddress(addressText))
            {
                return new ErrorResult() { error = "invalid address" };
            }

            var address = Address.FromText(addressText);
            var plugin = Nexus.GetPlugin<AddressTransactionsPlugin>();
            if (plugin == null)
            {
                return new ErrorResult() { error = "plugin not enabled" };
            }

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

        [APIInfo(typeof(string), "Allows to broadcast a signed operation on the network, but it's required to build it manually.")]
        [APIFailCase("rejected by mempool", "0000")] // TODO not correct
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded transaction", "0000")]
        public IAPIResult SendRawTransaction([APIParameter("Serialized transaction bytes, in hexadecimal format", "0000000000")] string txData)
        {
            if (Mempool == null)
            {
                return new ErrorResult { error = "No mempool" };
            }

            byte[] bytes;
            try
            {
                bytes = Base16.Decode(txData);
            }
            catch
            {
                return new ErrorResult { error = "Failed to decode script" };
            }

            if (bytes.Length == 0)
            {
                return new ErrorResult { error = "Invalid transaction script" };
            }

            var tx = Transaction.Unserialize(bytes);
            if (tx == null)
            {
                return new ErrorResult { error = "Failed to deserialize transaction" };
            }

            try
            {
                Mempool.Submit(tx);
            }
            catch (MempoolSubmissionException e)
            {
                return new ErrorResult { error = "Mempool submission rejected: " + e.Message };
            }
            catch (Exception)
            {
                return new ErrorResult { error = "Mempool submission rejected: internal error" };
            }

            return new SingleResult { value = tx.Hash.ToString() };
        }

        [APIInfo(typeof(ScriptResult), "Allows to invoke script based on network state, without state changes.")]
        [APIFailCase("script is invalid", "")]
        [APIFailCase("failed to decoded script", "0000")]
        public IAPIResult InvokeRawScript([APIParameter("Address or name of chain", "root")] string chainInput, [APIParameter("Serialized script bytes, in hexadecimal format", "0000000000")] string scriptData)
        {
            var chain = FindChainByInput(chainInput);
            if (chain == null)
            {
                return new ErrorResult { error = "invalid chain" };
            }

            byte[] script;
            try
            {
                script = Base16.Decode(scriptData);
            }
            catch
            {
                return new ErrorResult { error = "Failed to decode script" };
            }

            if (script.Length == 0)
            {
                return new ErrorResult { error = "Invalid transaction script" };
            }

            var changeSet = new StorageChangeSetContext(chain.Storage);
            var vm = new RuntimeVM(script, chain, null, null, changeSet, true);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                return new ErrorResult { error = $"Execution failed, state:{state}" };
            }

            string encodedResult;

            if (vm.Stack.Count == 0)
            {
                encodedResult = "";
            }
            else
            {
                var result = vm.Stack.Pop();

                if (result.Type == VMType.Object)
                {
                    result = VMObject.CastTo(result, VMType.Struct);
                }

                var resultBytes = Serialization.Serialize(result);
                encodedResult = Base16.Encode(resultBytes);
            }

            var evts = vm.Events.Select(evt => new EventResult() { address = evt.Address.Text, kind = evt.Kind.ToString(), data = Base16.Encode(evt.Data) });

            return new ScriptResult { result = encodedResult, events = evts.ToArray() };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.")]
        [APIFailCase("hash is invalid", "43242342")]
        public IAPIResult GetTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;
            if (!Hash.TryParse(hashText, out hash))
            {
                return new ErrorResult { error = "Invalid hash" };
            }

            var tx = Nexus.FindTransactionByHash(hash);

            if (tx == null)
            {
                if (Mempool != null)
                {
                    var status = Mempool.GetTransactionStatus(hash, out string reason);
                    switch (status)
                    {
                        case MempoolTransactionStatus.Pending:
                            return new ErrorResult { error = "pending" };

                        case MempoolTransactionStatus.Rejected:
                            return new ErrorResult { error = "rejected: " + reason };
                    }
                }

                return new ErrorResult { error = "Transaction not found" };
            }

            return FillTransaction(tx);
        }

        [APIInfo(typeof(string), "Removes a pending transaction from the mempool.")]
        [APIFailCase("hash is invalid", "43242342")]
        public IAPIResult CancelTransaction([APIParameter("Hash of transaction", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            if (Mempool == null)
            {
                return new ErrorResult { error = "mempool not available" };
            }

            Hash hash;
            if (!Hash.TryParse(hashText, out hash))
            {
                return new ErrorResult { error = "Invalid hash" };
            }

            var tx = Nexus.FindTransactionByHash(hash);

            if (tx != null)
            {
                return new ErrorResult { error = "already in chain" };
            }

            var status = Mempool.GetTransactionStatus(hash, out string reason);
            if (status == MempoolTransactionStatus.Pending)
            {
                if (Mempool.RejectTransaction(hash))
                {
                    return new SingleResult() { value = hash };
                }
            }

            return new ErrorResult { error = "Transaction not found" };
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
                var entry = FillToken(token);
                tokenList.Add(entry);
            }

            return new ArrayResult() { values = tokenList.ToArray() };
        }

        [APIInfo(typeof(TokenResult), "Returns info about a specific token deployed in Phantasma.")]
        public IAPIResult GetToken([APIParameter("Token symbol to obtain info", "SOUL")] string symbol)
        {
            var token = Nexus.FindTokenBySymbol(symbol);
            if (token == null)
            {
                return new ErrorResult() { error = "invalid token" };
            }

            var result = FillToken(token);

            return result;
        }

        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.")]
        public IAPIResult GetTokenData([APIParameter("Symbol of token", "NACHO")]string symbol, [APIParameter("ID of token", "1")]string IDtext)
        {
            var token = Nexus.FindTokenBySymbol(symbol);
            if (token == null)
            {
                return new ErrorResult() { error = "invalid token" };
            }

            BigInteger ID;
            if (!BigInteger.TryParse(IDtext, out ID))
            {
                return new ErrorResult() { error = "invalid ID" };
            }

            var info = Nexus.GetNFT(token, ID);

            var chain = GetMarketChain();
            bool forSale;

            if (chain != null)
            {
                forSale = (bool)chain.InvokeContract("market", "HasAuction", ID);
            }
            else
            {
                forSale = false;
            }


            return new TokenDataResult() { chainAddress = info.CurrentChain.Text, ownerAddress = info.CurrentOwner.Text, ID = ID.ToString(), rom = Base16.Encode(info.ROM), ram = Base16.Encode(info.RAM) };
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

        [APIInfo(typeof(TransactionResult[]), "Returns last X transactions of given token.", true)]
        [APIFailCase("token symbol is invalid", "43242342")]
        [APIFailCase("page is invalid", "-1")]
        [APIFailCase("pageSize is invalid", "-1")]
        public IAPIResult GetTokenTransfers([APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Index of page to return", "5")] uint page = 1, [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ErrorResult { error = "invalid page/pageSize" };
            }

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            var token = Nexus.FindTokenBySymbol(tokenSymbol);
            if (token == null)
            {
                return new ErrorResult { error = "Invalid token" };
            }

            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();

            // pagination
            uint numberRecords = (uint)plugin.GetTokenTransactions(tokenSymbol).Count();
            uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
            //

            var transfers = plugin.GetTokenTransactions(tokenSymbol)
                .Select(hash => Nexus.FindTransactionByHash(hash))
                .OrderByDescending(tx => Nexus.FindBlockForTransaction(tx).Timestamp.Value)
                .Skip((int)((page - 1) * pageSize))
                .Take((int)pageSize);

            var paginatedResult = new PaginatedResult();
            var txList = new List<object>();

            foreach (var transaction in transfers)
            {
                txList.Add(FillTransaction(transaction));
            }

            paginatedResult.pageSize = pageSize;
            paginatedResult.totalPages = totalPages;
            paginatedResult.total = numberRecords;
            paginatedResult.page = page;

            paginatedResult.result = new ArrayResult { values = txList.ToArray() };

            return paginatedResult;
        }

        [APIInfo(typeof(int), "Returns the number of transaction of a given token.")]
        [APIFailCase("token symbol is invalid", "43242342")]
        public IAPIResult GetTokenTransferCount([APIParameter("Token symbol", "SOUL")] string tokenSymbol)
        {
            var plugin = Nexus.GetPlugin<TokenTransactionsPlugin>();
            var txCount = plugin.GetTokenTransactions(tokenSymbol).Count();

            return new SingleResult() { value = txCount };
        }

        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.")]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("token is invalid", "-1")]
        [APIFailCase("chain is invalid", "-1re")]
        public IAPIResult GetTokenBalance([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string addressText, [APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Address or name of chain", "root")] string chainInput)
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
                decimals = (uint)token.Decimals,
                chain = chain.Address.Text
            };

            if (!token.IsFungible)
            {
                var idList = chain.GetTokenOwnerships(token).Get(chain.Storage, address);
                if (idList != null && idList.Any())
                {
                    result.ids = idList.Select(x => x.ToString()).ToArray();
                }
            }

            return result;
        }

        private Chain GetMarketChain()
        {
            return Nexus.RootChain; // TODO change later
        }

        [APIInfo(typeof(int), "Returns the number of active auctions.")]
        public IAPIResult GetAuctionsCount([APIParameter("Token symbol used as filter", "NACHO")]
            string symbol = null)
        {
            var chain = GetMarketChain();
            if (chain == null)
            {
                return new ErrorResult { error = "Market not available" };
            }

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract("market", "GetAuctions");

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            return new SingleResult { value = entries.Count() };
        }

        [APIInfo(typeof(AuctionResult[]), "Returns the auctions available in the market.", true)]
        public IAPIResult GetAuctions([APIParameter("Token symbol used as filter", "NACHO")] string symbol = null,
            [APIParameter("Index of page to return", "5")] uint page = 1,
            [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            var chain = GetMarketChain();
            if (chain == null)
            {
                return new ErrorResult { error = "Market not available" };
            }

            if (page < 1 || pageSize < 1)
            {
                return new ErrorResult { error = "invalid page/pageSize" };
            }

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            var paginatedResult = new PaginatedResult();

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract("market", "GetAuctions");

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            // pagination
            uint numberRecords = (uint)entries.Count();
            uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
            //

            entries = entries.OrderByDescending(p => p.StartDate.Value)
                .Skip((int)((page - 1) * pageSize))
                .Take((int)pageSize);

            paginatedResult.pageSize = pageSize;
            paginatedResult.totalPages = totalPages;
            paginatedResult.total = numberRecords;
            paginatedResult.page = page;

            paginatedResult.result = new ArrayResult { values = entries.Select(x => (object)FillAuction(x)).ToArray() };

            return paginatedResult;
        }

        [APIInfo(typeof(AuctionResult), "Returns the auction for a specific token.", false)]
        public IAPIResult GetAuction([APIParameter("Token symbol", "NACHO")] string symbol, [APIParameter("Token ID", "1")]string IDtext)
        {
            var token = Nexus.FindTokenBySymbol(symbol);
            if (token == null)
            {
                return new ErrorResult() { error = "invalid token" };
            }

            BigInteger ID;
            if (!BigInteger.TryParse(IDtext, out ID))
            {
                return new ErrorResult() { error = "invalid ID" };
            }

            var info = Nexus.GetNFT(token, ID);

            var chain = GetMarketChain();
            if (chain == null)
            {
                return new ErrorResult { error = "Market not available" };
            }

            var forSale = (bool)chain.InvokeContract("market", "HasAuction", ID);
            if (!forSale)
            {
                return new ErrorResult { error = "Token not for sale" };
            }

            var auction = (MarketAuction)chain.InvokeContract("market", "GetAuction", ID);

            return new AuctionResult() {
                baseSymbol = auction.BaseSymbol,
                quoteSymbol = auction.QuoteSymbol,
                creatorAddress = auction.Creator.Text,
                tokenId = auction.TokenID.ToString(),
                price = auction.Price.ToString(),
                startDate = auction.StartDate.Value,
                endDate = auction.EndDate.Value
            };
        }
    }
}