using System;
using System.Text;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Contracts.Native;
using Phantasma.Blockchain.Contracts;
using Phantasma.VM;
using Phantasma.Storage;
using Phantasma.Storage.Context;
using Phantasma.Blockchain.Tokens;
using Phantasma.Network.P2P;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Domain;
using Phantasma.Core.Log;

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
        public readonly int CacheDuration;

        public APIInfoAttribute(Type returnType, string description, bool paginated = false, int cacheDuration = -1) : base(description)
        {
            ReturnType = returnType;
            Paginated = paginated;
            CacheDuration = cacheDuration;
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

        private CacheDictionary<string, IAPIResult> _cache;

        public APIEntry(NexusAPI api, MethodInfo info)
        {
            _api = api;
            _info = info;
            _cache = null;
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

                if (attr.CacheDuration > 0 && api.UseCache)
                {
                    _cache = new CacheDictionary<string, IAPIResult>(32, TimeSpan.FromSeconds(attr.CacheDuration));
                }
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

            string key = null;
            IAPIResult result = null;

            bool cacheHit = false;

            if (_cache != null)
            {
                var sb = new StringBuilder();
                foreach (var arg in input)
                {
                    sb.Append(arg.ToString());
                }

                key = sb.ToString();
                if (_cache.TryGet(key, out result))
                {
                    cacheHit = true;
                }
            }

            if (!cacheHit)
            {
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

                result = (IAPIResult)_info.Invoke(_api, args);

                if (_cache != null)
                {
                    _cache.Add(key, result);
                }
            }

            if (_api.logger != null)
            {
                var sb = new StringBuilder();
                sb.Append("API request");

                if (cacheHit)
                {
                    sb.Append(" [Cached]");
                }

                sb.Append($": {this.Name}(");

                for (int i = 0; i < input.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                        sb.Append(' ');
                    }
                    sb.Append(input[i].ToString());
                }

                sb.Append(')');

                _api.logger?.Message(sb.ToString());
            }

            return result;
        }
    }

    public class NexusAPI
    {
        public readonly bool UseCache;
        public readonly Nexus Nexus;
        public ITokenSwapper TokenSwapper;
        public Mempool Mempool;
        public Node Node;
        public IEnumerable<APIEntry> Methods => _methods.Values;

        private readonly Dictionary<string, APIEntry> _methods = new Dictionary<string, APIEntry>();

        private const int PaginationMaxResults = 50;

        internal readonly Logger logger;

        public NexusAPI(Nexus nexus, bool useCache = false, Logger logger = null)
        {
            Throw.IfNull(nexus, nameof(nexus));

            Nexus = nexus;
            UseCache = useCache;
            this.logger = logger;

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

            logger?.Message($"Phantasma API enabled. {_methods.Count} methods available.");
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
        private TokenResult FillToken(string tokenSymbol)
        {
            var tokenInfo = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);
            var currentSupply = Nexus.RootChain.GetTokenSupply(Nexus.RootChain.Storage, tokenSymbol);

            return new TokenResult
            {
                symbol = tokenInfo.Symbol,
                name = tokenInfo.Name,
                currentSupply = currentSupply.ToString(),
                maxSupply = tokenInfo.MaxSupply.ToString(),
                decimals = tokenInfo.Decimals,
                flags = tokenInfo.Flags.ToString(),//.Split(',').Select(x => x.Trim()).ToArray(),
                platform = tokenInfo.Platform,
                hash = tokenInfo.Hash.ToString(),
            };
        }

        private TokenContent ReadNFT(string symbol, BigInteger tokenID, Chain chain)
        {
            return chain.ReadToken(chain.Storage, symbol, tokenID);
        }

        private AuctionResult FillAuction(MarketAuction auction, Chain chain)
        {
            var nft = ReadNFT(auction.BaseSymbol, auction.TokenID, chain);

            return new AuctionResult
            {
                baseSymbol = auction.BaseSymbol,
                quoteSymbol = auction.QuoteSymbol,
                tokenId = auction.TokenID.ToString(),
                creatorAddress = auction.Creator.Text,
                chainAddress = chain.Address.Text,
                price = auction.Price.ToString(),
                startDate = auction.StartDate.Value,
                endDate = auction.EndDate.Value,
                ram = Base16.Encode(nft.RAM),
                rom = Base16.Encode(nft.ROM),
            };
        }

        private TransactionResult FillTransaction(Transaction tx)
        {
            var block = Nexus.FindBlockByTransaction(tx);
            var chain = block != null ? Nexus.GetChainByAddress(block.ChainAddress) : null;

            var result = new TransactionResult
            {
                hash = tx.Hash.ToString(),
                chainAddress = chain != null ? chain.Address.Text : Address.Null.Text,
                timestamp = block != null ? block.Timestamp.Value : 0,
                blockHeight = block != null ? (int)block.Height : -1,
                blockHash = block != null ? block.Hash.ToString() : Hash.Null.ToString(),
                script = tx.Script.Encode(),
                fee = chain != null ? chain.GetTransactionFee(tx.Hash).ToString() : "0"
            };

            if (block != null)
            {
                var eventList = new List<EventResult>();

                var evts = block.GetEventsForTransaction(tx.Hash);
                foreach (var evt in evts)
                {
                    var eventEntry = FillEvent(evt);
                    eventList.Add(eventEntry);
                }

                var txResult = block.GetResultForTransaction(tx.Hash);
                result.result = txResult != null ? Base16.Encode(txResult) : "";
                result.events = eventList.ToArray();
            }
            else
            {
                result.result = "";
                result.events = new EventResult[0];
            }

            return result;
        }

        private EventResult FillEvent(Event evt)
        {
            return new EventResult
            {
                address = evt.Address.Text,
                contract = evt.Contract,
                data = evt.Data.Encode(),
                kind = evt.Kind.ToString()
            };
        }

        private BlockResult FillBlock(Block block, Chain chain)
        {
            var result = new BlockResult
            {
                hash = block.Hash.ToString(),
                previousHash = block.PreviousHash.ToString(),
                timestamp = block.Timestamp.Value,
                height = (uint)block.Height,
                chainAddress = chain.Address.ToString(),
                protocol = block.Protocol,
                reward = chain.GetBlockReward(block).ToString(),
                validatorAddress = block.Validator.ToString(),
                events = block.Events.Select(x => FillEvent(x)).ToArray(),
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
            Throw.IfNull(chain, nameof(chain));

            var parentName = Nexus.GetParentChainByName(chain.Name);
            var parentChain = Nexus.GetChainByName(parentName);

            var result = new ChainResult
            {
                name = chain.Name,
                address = chain.Address.Text,
                height = (uint)chain.Height,
                parentAddress = parentChain != null ? parentChain.Address.ToString() : "",
                contracts = chain.GetContracts().Select(x => x.Name).ToArray()
            };

            return result;
        }

        private Chain FindChainByInput(string chainInput)
        {
            var chain = Nexus.GetChainByName(chainInput);

            if (chain != null)
            {
                return chain;
            }

            if (Address.IsValidAddress(chainInput))
            {
                return Nexus.GetChainByAddress(Address.FromText(chainInput));
            }

            return null;
        }

        private ABIContractResult FillABI(string name, ContractInterface abi)
        {
            return new ABIContractResult
            {
                name = name,
                methods = abi.Methods.Select(x => new ABIMethodResult()
                {
                    name = x.name,
                    returnType = x.returnType.ToString(),
                    parameters = x.parameters.Select(y => new ABIParameterResult()
                    {
                        name = y.name,
                        type = y.type.ToString()
                    }).ToArray()
                }).ToArray()
            };
        }

        private ReceiptResult FillReceipt(RelayReceipt receipt)
        {
            return new ReceiptResult()
            {
                nexus = receipt.message.nexus,
                index = receipt.message.index.ToString(),
                timestamp = receipt.message.timestamp.Value,
                sender = receipt.message.sender.Text,
                receiver = receipt.message.receiver.Text,
                script = Base16.Encode(receipt.message.script ?? new byte[0])
            };
        }
        #endregion

        [APIInfo(typeof(AccountResult), "Returns the account name and balance of given address.", false, 10)]
        [APIFailCase("address is invalid", "ABCD123")]
        public IAPIResult GetAccount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account)
        {
            if (!Address.IsValidAddress(account))
            {
                return new ErrorResult { error = "invalid address" };
            }

            var result = new AccountResult();
            var address = Address.FromText(account);
            result.address = address.Text;
            result.name = Nexus.LookUpAddressName(Nexus.RootStorage, address);

            var stake = Nexus.GetStakeFromAddress(Nexus.RootStorage, address);

            if (stake > 0)
            {
                var unclaimed = Nexus.GetUnclaimedFuelFromAddress(Nexus.RootStorage, address);
                var time = Nexus.GetStakeTimestampOfAddress(Nexus.RootStorage, address);
                result.stakes = new StakeResult() { amount = stake.ToString(), time = time.Value, unclaimed = unclaimed.ToString() };
            }
            else
            {
                result.stakes = new StakeResult() { amount = "0", time = 0, unclaimed = "0" };
            }

            // deprecated
            result.stake = result.stakes.amount;
            result.unclaimed = result.stakes.unclaimed;

            var validator = Nexus.GetValidatorType(address);

            var balanceList = new List<BalanceResult>();
            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var symbol in symbols)
            {
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var balance = chain.GetTokenBalance(chain.Storage, symbol, address);
                    if (balance > 0)
                    {
                        var token = Nexus.GetTokenInfo(Nexus.RootStorage, symbol);
                        var balanceEntry = new BalanceResult
                        {
                            chain = chain.Name,
                            amount = balance.ToString(),
                            symbol = token.Symbol,
                            decimals = (uint)token.Decimals,
                            ids = new string[0]
                        };

                        if (!token.IsFungible())
                        {
                            var ownerships = new OwnershipSheet(symbol);
                            var idList = ownerships.Get(chain.Storage, address);
                            if (idList != null && idList.Any())
                            {
                                balanceEntry.ids = idList.Select(x => x.ToString()).ToArray();
                            }
                        }
                        balanceList.Add(balanceEntry);
                    }
                }
            }

            result.relay = Nexus.GetRelayBalance(address).ToString();
            result.balances = balanceList.ToArray();
            result.validator = validator.ToString();

            return result;
        }

        [APIInfo(typeof(string), "Returns the address that owns a given name.", false, 30)]
        [APIFailCase("address is invalid", "ABCD123")]
        public IAPIResult LookUpName([APIParameter("Name of account", "blabla")] string name)
        {
            if (!ValidationUtils.IsValidIdentifier(name))
            {
                return new ErrorResult { error = "invalid name" };
            }

            var address = Nexus.LookUpName(Nexus.RootStorage, name);
            if (address.IsNull)
            {
                return new ErrorResult { error = "name not owned" };
            }

            return new SingleResult() { value = address.Text };
        }

        [APIInfo(typeof(int), "Returns the height of a chain.", false, 3)]
        [APIFailCase("chain is invalid", "4533")]
        public IAPIResult GetBlockHeight([APIParameter("Address or name of chain", "root")] string chainInput)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                return new ErrorResult { error = "invalid chain" };
            }

            return new SingleResult { value = chain.Height };
        }

        [APIInfo(typeof(int), "Returns the number of transactions of given block hash or error if given hash is invalid or is not found.", false, 30)]
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

        [APIInfo(typeof(BlockResult), "Returns information about a block by hash.", false, 30)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        public IAPIResult GetBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return FillBlock(block, chain);
                    }
                }
            }

            return new ErrorResult { error = "invalid block hash" };
        }

        [APIInfo(typeof(string), "Returns a serialized string, containing information about a block by hash.", false, 30)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        public IAPIResult GetRawBlockByHash([APIParameter("Hash of block", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockHash)
        {
            if (Hash.TryParse(blockHash, out var hash))
            {
                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var block = chain.GetBlockByHash(hash);
                    if (block != null)
                    {
                        return new SingleResult() { value = block.ToByteArray(true).Encode() };
                    }
                }
            }

            return new ErrorResult() { error = "invalid block hash" };
        }

        [APIInfo(typeof(BlockResult), "Returns information about a block by height and chain.", false, 30)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        public IAPIResult GetBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
        {
            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                return new ErrorResult { error = "chain not found" };
            }

            var blockHash = chain.GetBlockHashAtHeight(height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return FillBlock(block, chain);
            }

            return new ErrorResult { error = "block not found" };
        }

        [APIInfo(typeof(string), "Returns a serialized string, in hex format, containing information about a block by height and chain.", false, 30)]
        [APIFailCase("block hash is invalid", "asdfsa")]
        [APIFailCase("chain is invalid", "453dsa")]
        public IAPIResult GetRawBlockByHeight([APIParameter("Address or name of chain", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string chainInput, [APIParameter("Height of block", "1")] uint height)
        {
            var chain = Nexus.GetChainByName(chainInput);

            if (chain == null)
            {
                if (!Address.IsValidAddress(chainInput))
                {
                    return new ErrorResult { error = "chain not found" };
                }
                chain = Nexus.GetChainByAddress(Address.FromText(chainInput));
            }

            if (chain == null)
            {
                return new ErrorResult { error = "chain not found" };
            }

            var blockHash = chain.GetBlockHashAtHeight(height);
            var block = chain.GetBlockByHash(blockHash);

            if (block != null)
            {
                return new SingleResult { value = block.ToByteArray(true).Encode() };
            }

            return new ErrorResult { error = "block not found" };
        }

        [APIInfo(typeof(TransactionResult), "Returns the information about a transaction requested by a block hash and transaction index.", false, 5)]
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
        public IAPIResult GetAddressTransactions([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Index of page to return", "5")] uint page = 1, [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            if (page < 1 || pageSize < 1)
            {
                return new ErrorResult { error = "invalid page/pageSize" };
            }

            if (pageSize > PaginationMaxResults)
            {
                pageSize = PaginationMaxResults;
            }

            if (Address.IsValidAddress(account))
            {
                var paginatedResult = new PaginatedResult();
                var address = Address.FromText(account);

                var chain = Nexus.RootChain;
                // pagination
                var txHashes = chain.GetTransactionHashesForAddress(address);
                uint numberRecords = (uint)txHashes.Length;
                uint totalPages = (uint)Math.Ceiling(numberRecords / (double)pageSize);
                //

                var txs = txHashes.Select(x => chain.GetTransactionByHash(x))
                    .OrderByDescending(tx => Nexus.FindBlockByTransaction(tx).Timestamp.Value)
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

        [APIInfo(typeof(int), "Get number of transactions in a specific address and chain", false, 5)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("chain is invalid", "-1")]
        public IAPIResult GetAddressTransactionCount([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Name or address of chain, optional", "apps")] string chainInput = "main")
        {
            if (!Address.IsValidAddress(account))
            {
                return new ErrorResult() { error = "invalid address" };
            }

            var address = Address.FromText(account);

            int count = 0;

            if (!string.IsNullOrEmpty(chainInput))
            {
                var chain = FindChainByInput(chainInput);
                if (chain == null)
                {
                    return new ErrorResult() { error = "invalid chain" };
                }

                var txHashes = chain.GetTransactionHashesForAddress(address);
                count = txHashes.Length;
            }
            else
            {
                var chains = Nexus.GetChains(Nexus.RootStorage);
                foreach (var chainName in chains)
                {
                    var chain = Nexus.GetChainByName(chainName);
                    var txHashes = chain.GetTransactionHashesForAddress(address);
                    count += txHashes.Length;
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
                var errorMessage = "Mempool submission rejected: " + e.Message;
                logger?.Warning(errorMessage);
                return new ErrorResult { error = errorMessage };
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

            //System.IO.File.AppendAllLines(@"c:\code\bug_vm.txt", new []{string.Join("\n", new VM.Disassembler(script).Instructions)});

            var changeSet = new StorageChangeSetContext(chain.Storage);
            var oracle = Nexus.CreateOracleReader();
            var vm = new RuntimeVM(script, chain, Timestamp.Now, null, changeSet, oracle, true);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                return new ErrorResult { error = $"Execution failed, state:{state}" };
            }

            var results = new Stack<string>();

            while (vm.Stack.Count > 0)
            {
                var result = vm.Stack.Pop();

                if (result.Type == VMType.Object)
                {
                    result = VMObject.CastTo(result, VMType.Struct);
                }

                var resultBytes = Serialization.Serialize(result);
                results.Push(Base16.Encode(resultBytes));
            }

            var evts = vm.Events.Select(evt => new EventResult() { address = evt.Address.Text, kind = evt.Kind.ToString(), data = Base16.Encode(evt.Data) }).ToArray();

            var oracleReads = oracle.Entries.Select(x => new OracleResult() { url = x.URL, content = Base16.Encode(x.Content) }).ToArray();

            var resultArray = results.ToArray();
            return new ScriptResult { results = resultArray, result = resultArray.FirstOrDefault(), events = evts, oracles = oracleReads };
        }

        [APIInfo(typeof(TransactionResult), "Returns information about a transaction by hash.", false, -1)]
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
                if (Mempool.Discard(hash))
                {
                    return new SingleResult() { value = hash };
                }
            }

            return new ErrorResult { error = "Transaction not found" };
        }

        [APIInfo(typeof(ChainResult[]), "Returns an array of all chains deployed in Phantasma.", false, 30)]
        public IAPIResult GetChains()
        {
            var result = new ArrayResult();

            var objs = new List<object>();

            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = Nexus.GetChainByName(chainName);
                var single = FillChain(chain);
                objs.Add(single);
            }

            result.values = objs.ToArray();
            return result;
        }

        [APIInfo(typeof(NexusResult), "Returns info about the nexus.", false, 30)]
        public IAPIResult GetNexus()
        {
            var tokenList = new List<TokenResult>();

            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = FillToken(token);
                tokenList.Add(entry);
            }

            var platformList = new List<PlatformResult>();

            var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
            foreach (var platform in platforms)
            {
                var info = Nexus.GetPlatformInfo(Nexus.RootStorage, platform);

                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).ToArray();
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => Nexus.GetTokenInfo(Nexus.RootStorage, x).Platform == platform).ToArray();
                platformList.Add(entry);
            }

            var chainList = new List<ChainResult>();

            var chains = Nexus.GetChains(Nexus.RootStorage);
            foreach (var chainName in chains)
            {
                var chain = Nexus.GetChainByName(chainName);
                var single = FillChain(chain);
                chainList.Add(single);
            }

            var governance = (GovernancePair[])Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "governance", nameof(GovernanceContract.GetValues)).ToObject();

            var orgs = Nexus.GetOrganizations(Nexus.RootStorage);

            var ses = ((LeaderboardRow[])Nexus.RootChain.InvokeContract(Nexus.RootChain.Storage, "ranking", nameof(RankingContract.GetRows), BombContract.SESLeaderboardName).ToObject()).ToArray();

            return new NexusResult()
            {
                name = Nexus.GetName(Nexus.RootStorage),
                tokens = tokenList.ToArray(),
                platforms = platformList.ToArray(),
                chains = chainList.ToArray(),
                organizations = orgs,
                ses = ses.Select(x => new LeaderboardResult() { address = x.address.Text, value = x.score.ToString() }).ToArray(),
                governance = governance.Select(x => new GovernanceResult() { name = x.Name, value = x.Value.ToString() }).ToArray()
            };
        }

        [APIInfo(typeof(OrganizationResult), "Returns info about an organization.", false, 30)]
        public IAPIResult GetOrganization(string ID)
        {
            if (!Nexus.OrganizationExists(Nexus.RootStorage, ID))
            {
                return new ErrorResult() { error = "invalid organization" };
            }

            var org = Nexus.GetOrganizationByName(Nexus.RootStorage, ID);
            var members = org.GetMembers();

            return new OrganizationResult()
            {
                id = ID,
                name = org.Name,
                members = members.Select(x => x.Text).ToArray(),
            };
        }

        [APIInfo(typeof(TokenResult[]), "Returns an array of tokens deployed in Phantasma.", false, 30)]
        public IAPIResult GetTokens()
        {
            var tokenList = new List<object>();

            var symbols = Nexus.GetTokens(Nexus.RootStorage);
            foreach (var token in symbols)
            {
                var entry = FillToken(token);
                tokenList.Add(entry);
            }

            return new ArrayResult() { values = tokenList.ToArray() };
        }

        [APIInfo(typeof(TokenResult), "Returns info about a specific token deployed in Phantasma.", false, 30)]
        public IAPIResult GetToken([APIParameter("Token symbol to obtain info", "SOUL")] string symbol)
        {
            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
            {
                return new ErrorResult() { error = "invalid token" };
            }

            var token = Nexus.GetTokenInfo(Nexus.RootStorage, symbol);
            var result = FillToken(symbol);

            return result;
        }

        [APIInfo(typeof(TokenDataResult), "Returns data of a non-fungible token, in hexadecimal format.", false, 5)]
        public IAPIResult GetTokenData([APIParameter("Symbol of token", "NACHO")]string symbol, [APIParameter("ID of token", "1")]string IDtext)
        {
            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
            {
                return new ErrorResult() { error = "invalid token" };
            }

            BigInteger ID;
            if (!BigInteger.TryParse(IDtext, out ID))
            {
                return new ErrorResult() { error = "invalid ID" };
            }

            var info = ReadNFT(symbol, ID, Nexus.RootChain); // TODO support other chains

            var chain = Nexus.GetChainByName(info.CurrentChain);
            bool forSale;

            if (chain != null && chain.IsContractDeployed(chain.Storage, "market"))
            {
                forSale = chain.InvokeContract(chain.Storage, "market", "HasAuction", ID).AsBool();
            }
            else
            {
                forSale = false;
            }


            return new TokenDataResult() { chainName = info.CurrentChain, ownerAddress = info.CurrentOwner.Text, ID = ID.ToString(), rom = Base16.Encode(info.ROM), ram = Base16.Encode(info.RAM) };
        }

        [APIInfo(typeof(BalanceResult), "Returns the balance for a specific token and chain, given an address.", false, 5)]
        [APIFailCase("address is invalid", "43242342")]
        [APIFailCase("token is invalid", "-1")]
        [APIFailCase("chain is invalid", "-1re")]
        public IAPIResult GetTokenBalance([APIParameter("Address of account", "PDHcAHq1fZXuwDrtJGDhjemFnj2ZaFc7iu3qD4XjZG9eV")] string account, [APIParameter("Token symbol", "SOUL")] string tokenSymbol, [APIParameter("Address or name of chain", "root")] string chainInput)
        {
            if (!Address.IsValidAddress(account))
            {
                return new ErrorResult { error = "invalid address" };
            }

            if (!Nexus.TokenExists(Nexus.RootStorage, tokenSymbol))
            {
                return new ErrorResult { error = "invalid token" };
            }

            var tokenInfo = Nexus.GetTokenInfo(Nexus.RootStorage, tokenSymbol);

            var chain = FindChainByInput(chainInput);

            if (chain == null)
            {
                return new ErrorResult { error = "invalid chain" };
            }

            var address = Address.FromText(account);
            var balance = chain.GetTokenBalance(chain.Storage, tokenSymbol, address);

            var result = new BalanceResult()
            {
                amount = balance.ToString(),
                symbol = tokenSymbol,
                decimals = (uint)tokenInfo.Decimals,
                chain = chain.Address.Text
            };

            if (!tokenInfo.IsFungible())
            {
                var ownerships = new OwnershipSheet(tokenSymbol);
                var idList = ownerships.Get(chain.Storage, address);
                if (idList != null && idList.Any())
                {
                    result.ids = idList.Select(x => x.ToString()).ToArray();
                }
            }

            return result;
        }

        [APIInfo(typeof(int), "Returns the number of active auctions.", false, 30)]
        public IAPIResult GetAuctionsCount([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName = null, [APIParameter("Token symbol used as filter", "NACHO")]
            string symbol = null)
        {
            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                return new ErrorResult { error = "Chain not found" };
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                return new ErrorResult { error = "Market not available" };
            }

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract(chain.Storage, "market", "GetAuctions").ToObject();

            if (!string.IsNullOrEmpty(symbol))
            {
                entries = entries.Where(x => x.BaseSymbol == symbol);
            }

            return new SingleResult { value = entries.Count() };
        }

        [APIInfo(typeof(AuctionResult[]), "Returns the auctions available in the market.", true, 30)]
        public IAPIResult GetAuctions([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol used as filter", "NACHO")] string symbol = null,
            [APIParameter("Index of page to return", "5")] uint page = 1,
            [APIParameter("Number of items to return per page", "5")] uint pageSize = PaginationMaxResults)
        {
            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                return new ErrorResult { error = "Chain not found" };
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
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

            IEnumerable<MarketAuction> entries = (MarketAuction[])chain.InvokeContract(chain.Storage, "market", "GetAuctions").ToObject();

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

            paginatedResult.result = new ArrayResult { values = entries.Select(x => (object)FillAuction(x, chain)).ToArray() };

            return paginatedResult;
        }

        [APIInfo(typeof(AuctionResult), "Returns the auction for a specific token.", false, 30)]
        public IAPIResult GetAuction([APIParameter("Chain address or name where the market is located", "NACHO")] string chainAddressOrName, [APIParameter("Token symbol", "NACHO")] string symbol, [APIParameter("Token ID", "1")]string IDtext)
        {
            if (!Nexus.TokenExists(Nexus.RootStorage, symbol))
            {
                return new ErrorResult() { error = "invalid token" };
            }

            BigInteger ID;
            if (!BigInteger.TryParse(IDtext, out ID))
            {
                return new ErrorResult() { error = "invalid ID" };
            }

            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                return new ErrorResult { error = "Chain not found" };
            }

            if (!chain.IsContractDeployed(chain.Storage, "market"))
            {
                return new ErrorResult { error = "Market not available" };
            }

            var info = ReadNFT(symbol, ID, chain);

            var forSale = chain.InvokeContract(chain.Storage, "market", "HasAuction", ID).AsBool();
            if (!forSale)
            {
                return new ErrorResult { error = "Token not for sale" };
            }

            var auction = (MarketAuction)chain.InvokeContract(chain.Storage, "market", "GetAuction", ID).ToObject();

            return new AuctionResult()
            {
                baseSymbol = auction.BaseSymbol,
                quoteSymbol = auction.QuoteSymbol,
                creatorAddress = auction.Creator.Text,
                tokenId = auction.TokenID.ToString(),
                price = auction.Price.ToString(),
                startDate = auction.StartDate.Value,
                endDate = auction.EndDate.Value
            };
        }

        [APIInfo(typeof(ArchiveResult), "Returns info about a specific archive.", false, 300)]
        public IAPIResult GetArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                return new ErrorResult() { error = "invalid hash" };
            }

            var archive = Nexus.GetArchive(hash);
            if (archive == null)
            {
                return new ErrorResult() { error = "archive not found" };
            }

            return new ArchiveResult()
            {
                hash = hashText,
                size = (uint)archive.Size,
                flags = archive.Flags.ToString(),
                key = Base16.Encode(archive.Key),
                blockCount = (int)archive.BlockCount,
                metadata = new string[0]// archive.Metadata.Select(x => $"{x.Key}={x.Value}").ToArray()
            };
        }

        [APIInfo(typeof(bool), "Writes the contents of an incomplete archive.", false)]
        public IAPIResult WriteArchive([APIParameter("Archive hash", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText, int blockIndex, [APIParameter("Block content bytes, in hex", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string blockContent)
        {
            Hash hash;

            if (!Hash.TryParse(hashText, out hash))
            {
                return new ErrorResult() { error = "invalid hash" };
            }

            var archive = Nexus.GetArchive(hash);
            if (archive == null)
            {
                return new ErrorResult() { error = "archive not found" };
            }

            if (blockIndex < 0 || blockIndex >= archive.BlockCount)
            {
                return new ErrorResult() { error = "invalid block index" };
            }

            var bytes = Base16.Decode(blockContent);

            try
            {
                Nexus.WriteArchiveBlock(archive, bytes, blockIndex);
                return new SingleResult()
                {
                    value = true
                };
            }
            catch (Exception e)
            {
                return new ErrorResult() { error = e.Message };
            }
        }

        [APIInfo(typeof(ABIContractResult), "Returns the ABI interface of specific contract.", false, 300)]
        public IAPIResult GetABI([APIParameter("Chain address or name where the market is located", "main")] string chainAddressOrName, [APIParameter("Contract name", "account")] string contractName)
        {
            var chain = FindChainByInput(chainAddressOrName);
            if (chain == null)
            {
                return new ErrorResult { error = "Chain not found" };
            }

            if (string.IsNullOrEmpty(contractName))
            {
                return new ErrorResult { error = "Invalid contract name" };
            }

            if (!chain.IsContractDeployed(chain.Storage, contractName))
            {
                return new ErrorResult { error = "Contract not found" };
            }

            var contract = this.Nexus.GetContractByName(contractName);
            return FillABI(contractName, contract.ABI);
        }

        [APIInfo(typeof(PeerResult[]), "Returns list of known peers.", false, 20)]
        public IAPIResult GetPeers()
        {
            if (Node == null)
            {
                return new ErrorResult { error = "No node available" };
            }

            var peers = Node.Peers.Select(x => new PeerResult() { url = x.Endpoint.ToString(), version = x.Version, flags = x.Capabilities.ToString(), fee = x.MinimumFee.ToString(), pow = (uint)x.MinimumPoW }).ToList();

            peers.Add(new PeerResult() { url = Node.PublicIP, version = Node.Version, flags = Node.Capabilities.ToString(), fee = Node.MinimumFee.ToString(), pow = (uint)Node.MinimumPoW });

            peers.Shuffle();

            return new ArrayResult()
            {
                values = peers.Select(x => (object)x).ToArray()
            };
        }

        [APIInfo(typeof(bool), "Writes a message to the relay network.", false)]
        public IAPIResult RelaySend([APIParameter("Serialized receipt, in hex", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string receiptHex)
        {
            if (Node == null)
            {
                return new ErrorResult { error = "No node available" };
            }

            if (!Node.Capabilities.HasFlag(PeerCaps.Relay))
            {
                return new ErrorResult { error = "Node relay is disabled" };
            }

            byte[] bytes;
            RelayReceipt receipt;
            try
            {
                bytes = Base16.Decode(receiptHex);
                receipt = RelayReceipt.FromBytes(bytes);
            }
            catch
            {
                return new ErrorResult() { error = "error decoding receipt" };
            }

            var msgBytes = receipt.message.ToByteArray();
            if (!receipt.signature.Verify(msgBytes, receipt.message.sender))
            {
                return new ErrorResult() { error = "invalid signature" };
            }

            try
            {
                Node.PostRelayMessage(receipt);
            }
            catch (Exception e)
            {
                return new ErrorResult() { error = e.Message };
            }

            return new SingleResult()
            {
                value = true
            };
        }

        [APIInfo(typeof(ReceiptResult[]), "Receives messages from the relay network.", false)]
        public IAPIResult RelayReceive([APIParameter("Address or account name", "helloman")] string account)
        {
            if (Node == null)
            {
                return new ErrorResult { error = "No node available" };
            }

            if (!Node.Capabilities.HasFlag(PeerCaps.Relay))
            {
                return new ErrorResult { error = "Node relay is disabled" };
            }

            Address address;

            if (Address.IsValidAddress(account))
            {
                address = Address.FromText(account);
            }
            else
            {
                address = Nexus.LookUpName(Nexus.RootStorage, account);
                if (address.IsNull)
                {
                    return new ErrorResult { error = "name not owned" };
                }
            }

            var receipts = Node.GetRelayReceipts(address);
            if (receipts.Any())
            {
                var receiptList = receipts.Select(x => (object)FillReceipt(x));

                return new ArrayResult() { values = receiptList.ToArray() };
            }
            else
            {
                return new ErrorResult { error = "no messages available" };
            }
        }

        [APIInfo(typeof(EventResult[]), "Reads pending messages from the relay network.", false)]
        public IAPIResult GetEvents([APIParameter("Address or account name", "helloman")] string account)
        {
            if (Node == null)
            {
                return new ErrorResult { error = "No node available" };
            }

            if (!Node.Capabilities.HasFlag(PeerCaps.Events))
            {
                return new ErrorResult { error = "Node relay is disabled" };
            }

            Address address;

            if (Address.IsValidAddress(account))
            {
                address = Address.FromText(account);
            }
            else
            {
                address = Nexus.LookUpName(Nexus.RootStorage, account);
                if (address.IsNull)
                {
                    return new ErrorResult { error = "name not owned" };
                }
            }

            var events = Node.GetEvents(address);
            if (!events.Any())
            {
                return new ErrorResult { error = "not events available" };
            }

            var eventList = events.Select(x => (object)FillEvent(x));

            return new ArrayResult() { values = eventList.ToArray() };
        }

        [APIInfo(typeof(PlatformResult[]), "Returns an array of available interop platforms.", false, 300)]
        public IAPIResult GetPlatforms()
        {
            var platformList = new List<PlatformResult>();

            var platforms = Nexus.GetPlatforms(Nexus.RootStorage);
            var symbols = Nexus.GetTokens(Nexus.RootStorage);

            foreach (var platform in platforms)
            {
                var info = Nexus.GetPlatformInfo(Nexus.RootStorage, platform);


                var entry = new PlatformResult();
                entry.platform = platform;
                entry.interop = info.InteropAddresses.Select(x => new InteropResult()
                {
                    local = x.LocalAddress.Text,
                    external = x.ExternalAddress
                }).ToArray();
                entry.chain = DomainExtensions.GetChainAddress(info).Text;
                entry.fuel = info.Symbol;
                entry.tokens = symbols.Where(x => Nexus.GetTokenInfo(Nexus.RootStorage, x).Platform == platform).ToArray();
                platformList.Add(entry);
            }

            return new ArrayResult() { values = platformList.Select(x => (object)x).ToArray() };
        }

        [APIInfo(typeof(ValidatorResult[]), "Returns an array of available validators.", false, 300)]
        public IAPIResult GetValidators()
        {
            var validators = Nexus.GetValidators().
                Where(x => !x.address.IsNull).
                Select(x => new ValidatorResult() { address = x.address.ToString(), type = x.type.ToString() });

            return new ArrayResult() { values = validators.Select(x => (object)x).ToArray() };
        }


        [APIInfo(typeof(string), "Tries to settle a pending swap for a specific hash.", false, 1)]
        public IAPIResult SettleSwap([APIParameter("Name of platform where swap transaction was created", "phantasma")]string sourcePlatform, [APIParameter("Name of platform to settle", "phantasma")]string destPlatform, [APIParameter("Hash of transaction to settle", "EE2CC7BA3FFC4EE7B4030DDFE9CB7B643A0199A1873956759533BB3D25D95322")] string hashText)
        {
            if (TokenSwapper == null)
            {
                return new ErrorResult { error = "token swapper not available" };
            }

            if (!Nexus.PlatformExists(Nexus.RootStorage, sourcePlatform))
            {
                return new ErrorResult { error = "Invalid source platform" };
            }

            if (!Nexus.PlatformExists(Nexus.RootStorage, destPlatform))
            {
                return new ErrorResult { error = "Invalid destination platform" };
            }

            Hash hash;
            if (!Hash.TryParse(hashText, out hash) || hash == Hash.Null)
            {
                return new ErrorResult { error = "Invalid hash" };
            }

            if (destPlatform == DomainSettings.PlatformName)
            {
                try
                {
                    var swap = Nexus.RootChain.GetSwap(Nexus.RootStorage, hash);
                    if (swap.destinationHash != Hash.Null)
                    {
                        return new SingleResult() { value = swap.destinationHash.ToString() };
                    }
                }
                catch
                {
                    // do nothing, just continue
                }
            }

            try
            {
                var destHash = TokenSwapper.SettleSwap(sourcePlatform, destPlatform, hash);

                if (destHash == Hash.Null)
                {
                    return new ErrorResult { error = "Swap failed" };
                }
                else
                {
                    return new SingleResult() { value = destHash.ToString() };
                }
            }
            catch (Exception e)
            {
                return new ErrorResult { error = e.Message };
            }
        }

        [APIInfo(typeof(SwapResult[]), "Returns platform swaps for a specific address.", false, -1)]
        public IAPIResult GetSwapsForAddress([APIParameter("Address or account name", "helloman")] string account)
        {
            if (TokenSwapper == null)
            {
                return new ErrorResult { error = "token swapper not available" };
            }

            Address address;

            if (Address.IsValidAddress(account))
            {
                address = Address.FromText(account);
            }
            else
            if (Pay.Chains.NeoWallet.IsValidAddress(account))
            {
                address = Pay.Chains.NeoWallet.EncodeAddress(account);
            }
            else
            {
                address = Nexus.LookUpName(Nexus.RootStorage, account);                
            }

            if (address.IsNull)
            {
                return new ErrorResult { error = "invalid address" };
            }

            var swapList = TokenSwapper.GetPendingSwaps(address);

            var oracleReader = Nexus.CreateOracleReader();

            var txswaps = swapList.
                Select(x => new KeyValuePair<ChainSwap, InteropTransaction>(x, oracleReader.ReadTransaction(x.sourcePlatform, x.sourceChain, x.sourceHash))).ToArray();

            var swaps = txswaps.Where(x => x.Value.Transfers.Length > 0).
                Select(x => new SwapResult()
                {
                    sourcePlatform = x.Key.sourcePlatform,
                    sourceChain = x.Key.sourceChain,
                    sourceHash = x.Key.sourceHash.ToString(),
                    destinationPlatform = x.Key.destinationPlatform,
                    destinationChain = x.Key.destinationChain,
                    destinationHash = x.Key.destinationHash == Hash.Null ? "pending" : x.Key.destinationHash.ToString(),
                    sourceAddress = x.Value.Transfers[0].sourceAddress.Text,
                    destinationAddress = x.Value.Transfers[0].destinationAddress.Text,
                    symbol = x.Value.Transfers[0].Symbol,
                    value = x.Value.Transfers[0].Value.ToString(),
                });

            return new ArrayResult() { values = swaps.Select(x => (object)x).ToArray() };
        }

    }
}
