using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public static readonly string RootChainName = "main";
        private static readonly string ChainNameMapKey = "chain.name.";
        private static readonly string ChainAddressMapKey = "chain.addr.";
        private static readonly string ChainParentNameKey = "chain.parent.";
        private static readonly string ChainChildrenBlockKey = "chain.children.";

        public static readonly string GasContractName = "gas";
        public static readonly string TokenContractName = "token";
        public static readonly string BlockContractName = "block";
        public static readonly string NexusContractName = "nexus";
        public static readonly string EnergyContractName = "energy";
        public static readonly string SwapContractName = "swap";
        public static readonly string ConsensusContractName = "consensus";
        public static readonly string GovernanceContractName = "governance";
        public static readonly string StorageContractName = "storage";
        public static readonly string ValidatorContractName = "validator";
        public static readonly string InteropContractName = "interop";

        public const string NexusProtocolVersionTag = "nexus.protocol.version";

        public const string FuelTokenSymbol = "KCAL";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string FiatTokenSymbol = "USD";
        public const string FiatTokenName = "Dollars";
        public const int FiatTokenDecimals = 8;

        public static readonly BigInteger PlatformSupply = UnitConversion.ToBigInteger(100000000, FuelTokenDecimals);
        public static readonly string PlatformName = "phantasma";

        public Chain RootChain => FindChainByName(RootChainName);

        private KeyValueStore<string, byte[]> _vars;
        private Dictionary<string, KeyValueStore<BigInteger, TokenContent>> _tokenContents = new Dictionary<string, KeyValueStore<BigInteger, TokenContent>>();

        private KeyValueStore<Hash, Archive> _archiveEntries;
        private KeyValueStore<Hash, byte[]> _archiveContents;

        public bool Ready { get; private set; }

        public string Name
        {
            get
            {
                if (_vars.ContainsKey(nameof(Name)))
                {
                    var bytes = _vars.Get(nameof(Name));
                    var result = Serialization.Unserialize<string>(bytes);
                    return result;
                }

                return null;
            }

            private set
            {
                var bytes = Serialization.Serialize(value);
                _vars.Set(nameof(Name), bytes);
            }
        }

        public Address RootChainAddress
        {
            get
            {
                if (_vars.ContainsKey(nameof(RootChainAddress)))
                {
                    return Serialization.Unserialize<Address>(_vars.Get(nameof(RootChainAddress)));
                }

                return Address.Null;
            }

            private set
            {
                _vars.Set(nameof(RootChainAddress), Serialization.Serialize(value));
            }
        }

        public Address GenesisAddress
        {
            get
            {
                if (_vars.ContainsKey(nameof(GenesisAddress)))
                {
                    return Serialization.Unserialize<Address>(_vars.Get(nameof(GenesisAddress)));
                }

                return Address.Null;
            }

            private set
            {
                _vars.Set(nameof(GenesisAddress), Serialization.Serialize(value));
            }
        }

        public Hash GenesisHash
        {
            get
            {
                if (_vars.ContainsKey(nameof(GenesisHash)))
                {
                    var result = Serialization.Unserialize<Hash>(_vars.Get(nameof(GenesisHash)));
                    return result;
                }

                return Hash.Null;
            }

            private set
            {
                _vars.Set(nameof(GenesisHash), Serialization.Serialize(value));
            }
        }

        private Timestamp _genesisDate;
        public Timestamp GenesisTime
        {
            get
            {
                if (_genesisDate.Value == 0 && Ready)
                {
                    var genesisBlock = RootChain.FindBlockByHash(GenesisHash);
                    _genesisDate = genesisBlock.Timestamp;
                }

                return _genesisDate;
            }
        }

        public IEnumerable<string> Tokens
        {
            get
            {
                if (_vars.ContainsKey(nameof(Tokens)))
                {
                    return Serialization.Unserialize<string[]>(_vars.Get(nameof(Tokens)));
                }

                return Enumerable.Empty<string>();
            }

            private set
            {
                var symbols = value.ToArray();
                _vars.Set(nameof(Tokens), Serialization.Serialize(symbols));
            }
        }

        public IEnumerable<string> Contracts
        {
            get
            {
                if (_vars.ContainsKey(nameof(Contracts)))
                {
                    return Serialization.Unserialize<string[]>(_vars.Get(nameof(Contracts)));
                }

                return Enumerable.Empty<string>();
            }

            private set
            {
                var names = value.ToArray();
                _vars.Set(nameof(Contracts), Serialization.Serialize(names));
            }
        }

        public IEnumerable<string> Chains
        {
            get
            {
                if (_vars.ContainsKey(nameof(Chains)))
                {
                    return Serialization.Unserialize<string[]>(_vars.Get(nameof(Chains)));
                }

                return Enumerable.Empty<string>();
            }

            private set
            {
                var names = value.ToArray();
                _vars.Set(nameof(Chains), Serialization.Serialize(names));
            }
        }

        public IEnumerable<string> Platforms
        {
            get
            {
                if (_vars.ContainsKey(nameof(Platforms)))
                {
                    return Serialization.Unserialize<string[]>(_vars.Get(nameof(Platforms)));
                }

                return Enumerable.Empty<string>();
            }

            private set
            {
                var names = value.ToArray();
                _vars.Set(nameof(Platforms), Serialization.Serialize(names));
            }
        }

        public IEnumerable<string> Feeds
        {
            get
            {
                if (_vars.ContainsKey(nameof(Feeds)))
                {
                    return Serialization.Unserialize<string[]>(_vars.Get(nameof(Feeds)));
                }

                return Enumerable.Empty<string>();
            }

            private set
            {
                var names = value.ToArray();
                _vars.Set(nameof(Feeds), Serialization.Serialize(names));
            }
        }

        private readonly List<IChainPlugin> _plugins = new List<IChainPlugin>();

        private readonly Logger _logger;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private Func<Nexus, OracleReader> _oracleFactory = null;

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(Logger logger = null, Func<string, IKeyValueStoreAdapter> adapterFactory= null, Func<Nexus, OracleReader> oracleFactory = null)
        {
            this._adapterFactory = adapterFactory;
            this._oracleFactory = oracleFactory;

            this._vars = new KeyValueStore<string, byte[]>(CreateKeyStoreAdapter("nexus"));

            try
            {
                var temp = this.Name;
                Ready = !string.IsNullOrEmpty(temp);

                if (Ready)
                {
                    var chainList = this.Chains;
                    foreach (var chainName in chainList)
                    {
                        FindChainByName(chainName);
                    }
                }
            }
            catch
            {
                Ready = false;
            }

            _archiveEntries= new KeyValueStore<Hash, Archive>(CreateKeyStoreAdapter("archives"));
            _archiveContents = new KeyValueStore<Hash, byte[]>(CreateKeyStoreAdapter("contents"));

            _logger = logger;
        }

        internal IKeyValueStoreAdapter CreateKeyStoreAdapter(Address address, string name)
        {
            return CreateKeyStoreAdapter(address.Text + "_" + name);
        }

        internal IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
        {
            if (_adapterFactory != null)
            {
                var result = _adapterFactory(name);
                Throw.If(result == null, "keystore adapter factory failed");
                return result;
            }

            return new MemoryStore();
        }

        #region PLUGINS
        public void AddPlugin(IChainPlugin plugin)
        {
            _plugins.Add(plugin);
        }

        internal void PluginTriggerBlock(Chain chain, Block block)
        {
            foreach (var plugin in _plugins)
            {
                var txs = chain.GetBlockTransactions(block);
                foreach (var tx in txs)
                {
                    plugin.OnTransaction(chain, block, tx);
                }
            }
        }

        public Chain FindChainForBlock(Block block)
        {
            return FindChainForBlock(block.Hash);
        }

        public Chain FindChainForBlock(Hash hash)
        {
            var chainNames = this.Chains;
            foreach (var chainName in chainNames)
            {
                var chain = FindChainByName(chainName);
                if (chain.ContainsBlock(hash))
                {
                    return chain;
                }
            }

            return null;
        }

        public Block FindBlockByTransaction(Transaction tx)
        {
            return FindBlockByHash(tx.Hash);
        }

        public Block FindBlockByHash(Hash hash)
        {
            var chainNames = this.Chains;
            foreach (var chainName in chainNames)
            {
                var chain = FindChainByName(chainName);
                if (chain.ContainsTransaction(hash))
                {
                    return chain.FindTransactionBlock(hash);
                }
            }

            return null;
        }

        public T GetPlugin<T>() where T : IChainPlugin
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is T variable)
                {
                    return variable;
                }
            }

            return default(T);
        }
        #endregion

        #region NAME SERVICE
        public Address LookUpName(string name)
        { 
            if (!ValidationUtils.ValidateName(name))
            {
                return Address.Null;
            }

            var chain = RootChain;
            return chain.InvokeContract("account", "LookUpName", name).AsAddress();
        }

        public string LookUpAddressName(Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract("account", "LookUpAddress", address).AsString();
        }

        public byte[] LookUpAddressScript(Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract("account", "LookUpScript", address).AsByteArray();
        }
        #endregion

        #region CONTRACTS
        private Dictionary<string, SmartContract> _contractCache = new Dictionary<string, SmartContract>();

        public SmartContract FindContract(string contractName) 
        {
            Throw.IfNullOrEmpty(contractName, nameof(contractName));

            if (_contractCache.ContainsKey(contractName))
            {
                return _contractCache[contractName];
            }

            SmartContract contract;
            switch (contractName)
            {
                case "nexus": contract= new NexusContract(); break;
                case "validator":  contract = new ValidatorContract(); break;
                case "governance":  contract = new GovernanceContract(); break;
                case "account":  contract  = new AccountContract(); break;
                case "friends": contract  = new FriendContract(); break;
                case "exchange": contract  = new ExchangeContract(); break;
                case "market":    contract  = new MarketContract(); break;
                case "energy":   contract  = new EnergyContract(); break;
                case "token": contract = new TokenContract(); break;
                case "swap": contract = new SwapContract(); break;
                case "gas": contract = new GasContract(); break;
                case "block": contract = new BlockContract(); break;
                case "relay": contract = new RelayContract(); break;
                case "storage": contract  = new StorageContract(); break;
                case "vault": contract  = new VaultContract(); break;
                case "apps": contract  = new AppsContract(); break;
                case "dex": contract = new ExchangeContract(); break;
                case "sale": contract = new SaleContract(); break;
                case "interop": contract = new InteropContract(); break;
                case "nacho": contract = new NachoContract(); break;
                default:
                    throw new Exception("Unknown contract: " + contractName);
            }

            _contractCache[contractName] = contract;
            return contract;
        }

        #endregion

        #region TRANSACTIONS
        public Transaction FindTransactionByHash(Hash hash)
        {
            var chainNames = this.Chains;
            foreach (var chainName in chainNames)
            {
                var chain = FindChainByName(chainName);
                var tx = chain.FindTransactionByHash(hash);
                if (tx != null)
                {
                    return tx;
                }
            }

            return null;
        }
        #endregion

        #region CHAINS
        internal Chain CreateChain(StorageContext storage, Address owner, string name, Chain parentChain, IEnumerable<string> contractNames)
        {
            if (name != RootChainName)
            {
                if (parentChain == null)
                {
                    return null;
                }
            }

            if (!Chain.ValidateName(name))
            {
                return null;
            }

            // check if already exists something with that name
            if (ChainExists(name))
            {
                return null;
            }

            if (contractNames == null)
            {
                return null;
            }

            var chain = new Chain(this, name, _logger);

            var contractSet = new HashSet<string>(contractNames);
            contractSet.Add(GasContractName);
            contractSet.Add(TokenContractName);
            contractSet.Add(BlockContractName);
            chain.DeployContracts(contractSet);

            // add to persistent list of chains
            var chainList = this.Chains.ToList();
            chainList.Add(name);
            this.Chains = chainList;

            // add address and name mapping 
            this._vars.Set(ChainNameMapKey + chain.Name, chain.Address.PublicKey);
            this._vars.Set(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
            
            if (parentChain != null)
            {
                this._vars.Set(ChainParentNameKey + chain.Name, Encoding.UTF8.GetBytes(parentChain.Name));

                var childrenList = GetChildrenListOfChain(parentChain.Name);
                childrenList.Add<string>(chain.Name);

                var tokenList = this.Tokens;
                // copy each token current supply relative to parent to the chain new
                foreach (var tokenSymbol in tokenList)
                {
                    var parentSupply = new SupplySheet(tokenSymbol, parentChain, this);
                    var localSupply = new SupplySheet(tokenSymbol, chain, this);
                    localSupply.Init(chain.Storage, storage, parentSupply);
                }
            }
            else
            {
                this.RootChainAddress = chain.Address;
            }

            _chainCache[chain.Name] = chain;

            return chain;
        }

        public string LookUpChainNameByAddress(Address address)
        {
            var key = ChainAddressMapKey + address.Text;
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                return Encoding.UTF8.GetString(bytes);
            }

            return null;
        }
        
        public bool ChainExists(string chainName)
        {
            if (string.IsNullOrEmpty(chainName))
            {
                return false;
            }

            var key = ChainNameMapKey + chainName;
            return _vars.ContainsKey(key);
        }

        private Dictionary<string, Chain> _chainCache = new Dictionary<string, Chain>();

        public string GetParentChainByAddress(Address address)
        {
            var chain = FindChainByAddress(address);
            if (chain == null)
            {
                return null;
            }
            return GetParentChainByName(chain.Name);
        }

        public string GetParentChainByName(string chainName)
        {
            if (chainName == RootChainName)
            {
                return null;
            }

            var key = ChainParentNameKey + chainName;
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                var parentName = Encoding.UTF8.GetString(bytes);
                return parentName;
            }

            throw new Exception("Parent name not found for chain: " + chainName);
        }

        public IEnumerable<string> GetChildChainsByAddress(Address chainAddress)
        {
            var chain = FindChainByAddress(chainAddress);
            if (chain == null)
            {
                return null;
            }

            return GetChildChainsByName(chain.Name);
        }

        public OracleReader CreateOracleReader()
        {
            Throw.If(_oracleFactory == null, "oracle factory is not setup");
            return _oracleFactory(this);
        }

        public IEnumerable<string> GetChildChainsByName(string chainName)
        {
            var list = GetChildrenListOfChain(chainName);
            var count = (int)list.Count();
            var names = new string[count];
            for (int i=0; i<count; i++)
            {
                names[i] = list.Get<string>(i);
            }

            return names;
        }

        private StorageList GetChildrenListOfChain(string chainName)
        {
            var key = Encoding.UTF8.GetBytes(ChainChildrenBlockKey + chainName);
            var list = new StorageList(key, new KeyStoreStorage(_vars.Adapter));
            return list;
        }

        public Chain FindChainByAddress(Address address)
        {
            var name = LookUpChainNameByAddress(address);
            if (string.IsNullOrEmpty(name))
            {
                return null; // TODO should be exception
            }

            return FindChainByName(name);
        }

        public Chain FindChainByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (_chainCache.ContainsKey(name))
            {
                return _chainCache[name];
            }

            if (ChainExists(name))
            {
                var chain = new Chain(this, name);
                _chainCache[name] = chain;
                return chain;
            }

            //throw new Exception("Chain not found: " + name);
            return null;
        }

        #endregion

        #region FEEDS
        internal bool CreateFeed(Address owner, string name, OracleFeedMode mode)
        {
            if (name == null)
            {
                return false;
            }

            if (!owner.IsUser)
            {
                return false;
            }

            // check if already exists something with that name
            if (FeedExists(name))
            {
                return false;
            }

            var feedInfo = new OracleFeed(name, owner, mode);
            EditFeed(name, feedInfo);

            // add to persistent list of feeds
            var feedList = this.Feeds.ToList();
            feedList.Add(name);
            this.Feeds = feedList;

            return true;
        }

        private string GetFeedInfoKey(string name)
        {
            return "feed:" + name.ToUpper();
        }

        private void EditFeed(string name, OracleFeed feed)
        {
            var key = GetFeedInfoKey(name);
            var bytes = Serialization.Serialize(feed);
            _vars.Set(key, bytes);
        }

        public bool FeedExists(string name)
        {
            var key = GetFeedInfoKey(name);
            return _vars.ContainsKey(key);
        }

        public OracleFeed GetFeedInfo(string name)
        {
            var key = GetFeedInfoKey(name);
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                return Serialization.Unserialize<OracleFeed>(bytes);
            }

            throw new ChainException($"Oracle feed does not exist ({name})");
        }
        #endregion

        #region TOKENS
        private void ValidateSymbol(string symbol)
        {
            foreach (var c in symbol)
            {
                Throw.If(c < 'A'  || c > 'Z', "Symbol must only contain capital letters, no other characters are allowed");
            }
        }

        internal bool CreateToken(string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            if (symbol == null || name == null || maxSupply < 0)
            {
                return false;
            }

            ValidateSymbol(symbol);

            // check if already exists something with that name
            if (TokenExists(symbol))
            {
                return false;
            }

            Throw.If(maxSupply < 0, "negative supply");
            Throw.If(maxSupply == 0 && flags.HasFlag(TokenFlags.Finite), "finite requires a supply");
            Throw.If(maxSupply > 0 && !flags.HasFlag(TokenFlags.Finite), "infinite requires no supply");

            if (!flags.HasFlag(TokenFlags.Fungible))
            {
                Throw.If(flags.HasFlag(TokenFlags.Divisible), "non-fungible token must be indivisible");
            }

            if (flags.HasFlag(TokenFlags.Divisible))
            {
                Throw.If(decimals <= 0, "divisible token must have decimals");
            }
            else
            {
                Throw.If(decimals > 0, "indivisible token can't have decimals");
            }

            var tokenInfo = new TokenInfo(symbol, name, platform, hash, maxSupply, decimals, flags, script);
            EditToken(symbol, tokenInfo);

            // add to persistent list of tokens
            var tokenList = this.Tokens.ToList();
            tokenList.Add(symbol);
            this.Tokens = tokenList;

            return true;
        }

        private string GetTokenInfoKey(string symbol)
        {
            return "info:" + symbol;
        }

        private void EditToken(string symbol, TokenInfo tokenInfo)
        {
            var key = GetTokenInfoKey(symbol);
            var bytes = Serialization.Serialize(tokenInfo);
            _vars.Set(key, bytes);
        }

        public bool TokenExists(string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            return _vars.ContainsKey(key);
        }

        public TokenInfo GetTokenInfo(string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                return Serialization.Unserialize<TokenInfo>(bytes);
            }

            throw new ChainException($"Token does not exist ({symbol})");
        }

        public BigInteger GetTokenSupply(StorageContext storage, string symbol)
        {
            if (!TokenExists(symbol))
            {
                throw new ChainException($"Token does not exist ({symbol})");
            }

            var supplies = new SupplySheet(symbol, RootChain, this);
            return supplies.GetTotal(storage);
        }

        internal bool MintTokens(RuntimeVM runtimeVM, string symbol, Address target, BigInteger amount, bool isSettlement)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (!tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            var supply = new SupplySheet(symbol, runtimeVM.Chain, this);
            if (!supply.Mint(runtimeVM.ChangeSet, amount, tokenInfo.MaxSupply))
            {
                return false;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Add(runtimeVM.ChangeSet, target, amount))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, isSettlement ? TokenContract.TriggerReceive: TokenContract.TriggerMint, target, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(target);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, isSettlement ? AccountContract.TriggerReceive:  AccountContract.TriggerMint, target, symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            return true;
        }

        // NFT version
        internal bool MintToken(RuntimeVM runtimeVM, string symbol, Address target, BigInteger tokenID, bool isSettlement)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            var supply = new SupplySheet(symbol, runtimeVM.Chain, this);
            if (!supply.Mint(runtimeVM.ChangeSet, 1, tokenInfo.MaxSupply))
            {
                return false;
            }

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Add(runtimeVM.ChangeSet, target, tokenID))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, isSettlement ? TokenContract.TriggerReceive : TokenContract.TriggerMint, target, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(target);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, isSettlement ? AccountContract.TriggerReceive:  AccountContract.TriggerMint, target, symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            EditNFTLocation(symbol, tokenID, runtimeVM.Chain.Address, target);
            return true;
        }

        internal bool BurnTokens(RuntimeVM runtimeVM, string symbol, Address target, BigInteger amount, bool isSettlement)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (!tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            var supply = new SupplySheet(symbol, runtimeVM.Chain, this);

            if (tokenInfo.IsCapped && !supply.Burn(runtimeVM.ChangeSet, amount))
            {
                return false;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Subtract(runtimeVM.ChangeSet, target, amount))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, isSettlement ? TokenContract.TriggerSend:  TokenContract.TriggerBurn, target, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(target);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, isSettlement ? AccountContract.TriggerSend: AccountContract.TriggerBurn, target, symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            return true;
        }

        // NFT version
        internal bool BurnToken(RuntimeVM runtimeVM, string symbol, Address target, BigInteger tokenID, bool isSettlement)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            var chain = RootChain;
            var supply = new SupplySheet(symbol, chain, this);

            if (!supply.Burn(runtimeVM.ChangeSet, 1))
            {
                return false;
            }

            if (!DestroyNFT(symbol, tokenID))
            {
                return false;
            }

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Remove(runtimeVM.ChangeSet, target, tokenID))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, isSettlement ? TokenContract.TriggerSend : TokenContract.TriggerBurn, target, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(target);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, isSettlement ? AccountContract.TriggerSend:  AccountContract.TriggerBurn, target, symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }
            return true;
        }

        internal bool TransferTokens(RuntimeVM runtimeVM, string symbol, Address source, Address destination, BigInteger amount)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (!tokenInfo.Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (!tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                throw new Exception("Should be fungible");
            }

            if (amount <= 0)
            {
                return false;
            }

            if (source == destination)
            {
                return true;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Subtract(runtimeVM.ChangeSet, source, amount))
            {
                return false;
            }

            if (!balances.Add(runtimeVM.ChangeSet, destination, amount))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, TokenContract.TriggerSend, source, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, TokenContract.TriggerReceive, destination, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(source);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, AccountContract.TriggerSend, source, symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            accountScript = this.LookUpAddressScript(destination);
            accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, AccountContract.TriggerReceive, destination, symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            return true;
        }

        internal bool TransferToken(RuntimeVM runtimeVM, string symbol, Address source, Address destination, BigInteger tokenID)
        {
            if (!TokenExists(symbol))
            {
                return false;
            }

            var tokenInfo = GetTokenInfo(symbol);

            if (!tokenInfo.Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                throw new Exception("Should be non-fungible");
            }

            if (tokenID <= 0)
            {
                return false;
            }

            if (source == destination)
            {
                return true;
            }

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Remove(runtimeVM.ChangeSet, source, tokenID))
            {
                return false;
            }

            if (!ownerships.Add(runtimeVM.ChangeSet, destination, tokenID))
            {
                return false;
            }

            var tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, TokenContract.TriggerSend, source, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            tokenTriggerResult = SmartContract.InvokeTrigger(runtimeVM, tokenInfo.Script, TokenContract.TriggerReceive, destination, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountScript = this.LookUpAddressScript(source);
            var accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, AccountContract.TriggerSend, source, symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            accountScript = this.LookUpAddressScript(destination);
            accountTriggerResult = SmartContract.InvokeTrigger(runtimeVM, accountScript, AccountContract.TriggerReceive, destination, symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            EditNFTLocation(symbol, tokenID, runtimeVM.Chain.Address, destination);
            return true;
        }

        #endregion

        #region NFT
        private BigInteger GenerateIDForNFT(string tokenSymbol)
        {
            var key = "ID:" + tokenSymbol;
            BigInteger tokenID;

            byte[] bytes;

            lock (_vars)
            {
                if (_vars.ContainsKey(key))
                {
                    bytes = _vars.Get(key);
                    tokenID = Serialization.Unserialize<BigInteger>(bytes);
                    tokenID++;
                }
                else
                {
                    tokenID = 1;
                }

                bytes = Serialization.Serialize(tokenID);
                _vars.Set(key, bytes);
            }

            return tokenID;
        }

        internal BigInteger CreateNFT(string tokenSymbol, Address chainAddress, byte[] rom, byte[] ram)
        {
            Throw.IfNull(rom, nameof(rom));
            Throw.IfNull(ram, nameof(ram));

            lock (_tokenContents)
            {
                KeyValueStore<BigInteger, TokenContent> contents;

                if (_tokenContents.ContainsKey(tokenSymbol))
                {
                    contents = _tokenContents[tokenSymbol];
                }
                else
                {
                    // NOTE here we specify the data size as small, meaning the total allowed size of a nft including rom + ram is 255 bytes
                    var key = "nft_" + tokenSymbol;
                    contents = new KeyValueStore<BigInteger, TokenContent>(this.CreateKeyStoreAdapter(key));
                    _tokenContents[tokenSymbol] = contents;
                }

                var tokenID = GenerateIDForNFT(tokenSymbol);

                var content = new TokenContent(chainAddress, chainAddress, rom, ram);
                contents[tokenID] = content;

                return tokenID;
            }
        }

        internal bool DestroyNFT(string tokenSymbol, BigInteger tokenID)
        {
            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(tokenSymbol))
                {
                    var contents = _tokenContents[tokenSymbol];

                    if (contents.ContainsKey(tokenID))
                    {
                        contents.Remove(tokenID);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool EditNFTLocation(string tokenSymbol, BigInteger tokenID, Address chainAddress, Address owner)
        {
            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(tokenSymbol))
                {
                    var contents = _tokenContents[tokenSymbol];

                    if (contents.ContainsKey(tokenID))
                    {
                        var content = contents[tokenID];
                        content = new TokenContent(chainAddress, owner, content.ROM, content.RAM);
                        contents.Set(tokenID, content);
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool EditNFTContent(string tokenSymbol, BigInteger tokenID, byte[] ram)
        {
            if (ram == null || ram.Length > TokenContent.MaxRAMSize)
            {
                return false;
            }

            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(tokenSymbol))
                {
                    var contents = _tokenContents[tokenSymbol];

                    if (contents.ContainsKey(tokenID))
                    {
                        var content = contents[tokenID];
                        if (ram == null)
                        {
                            ram = content.RAM;
                        }
                        content = new TokenContent(content.CurrentChain, content.CurrentOwner, content.ROM, ram);
                        contents.Set(tokenID, content);
                        return true;
                    }
                }
            }

            return false;
        }

        public TokenContent GetNFT(string tokenSymbol, BigInteger tokenID)
        {
            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(tokenSymbol))
                {
                    var contents = _tokenContents[tokenSymbol];

                    if (contents.ContainsKey(tokenID))
                    {
                        var content = contents[tokenID];
                        return content;
                    }
                }
            }

            throw new ChainException($"NFT not found ({tokenSymbol}:{tokenID})");
        }
        #endregion

        #region GENESIS
        private Transaction SetupNexusTx(KeyPair owner)
        {
            var sb = ScriptUtils.BeginScript();

            sb.CallContract("block", "OpenBlock", owner.Address);

            sb.CallContract(Nexus.TokenContractName, "MintTokens", owner.Address, owner.Address, StakingTokenSymbol, UnitConversion.ToBigInteger(8863626, StakingTokenDecimals));
            // requires staking token to be created previously
            // note this is a completly arbitrary number just to be able to generate energy in the genesis, better change it later
            sb.CallContract(Nexus.EnergyContractName, "Stake", owner.Address, UnitConversion.ToBigInteger(100000, StakingTokenDecimals));
            sb.CallContract(Nexus.EnergyContractName, "Claim", owner.Address, owner.Address);

            var script = sb.EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);

            return tx;
        }

        private Transaction ChainCreateTx(KeyPair owner, string name, params string[] contracts)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(Nexus.NexusContractName, "CreateChain", owner.Address, name, RootChain.Name, contracts).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Mine((int)ProofOfWork.Moderate);
            tx.Sign(owner);
            return tx;
        }

        private Transaction ValueCreateTx(KeyPair owner, string name, BigInteger initial, BigInteger min, BigInteger max)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(Nexus.GovernanceContractName, "CreateValue", name, initial, min, max).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction ConsensusStakeCreateTx(KeyPair owner)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("validator", "SetValidator", owner.Address, new BigInteger(0)).
                CallContract(Nexus.SwapContractName, "DepositTokens", owner.Address, StakingTokenSymbol, UnitConversion.ToBigInteger(1, StakingTokenDecimals)).
                CallContract(Nexus.SwapContractName, "DepositTokens", owner.Address, FuelTokenSymbol, UnitConversion.ToBigInteger(100, FuelTokenDecimals)).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction TokenMetadataTx(KeyPair owner, string symbol, string field, string val)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("nexus", "SetTokenMetadata", symbol, field, val).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        public bool CreateGenesisBlock(string name, KeyPair owner, Timestamp timestamp)
        {
            if (Ready)
            {
                return false;
            }

            if (!ValidationUtils.ValidateName(name))
            {
                throw new ChainException("invalid nexus name");
            }

            this.Name = name;

            this.GenesisAddress = owner.Address;

            var rootChain = CreateChain(null, owner.Address, RootChainName, null, new[] { "nexus", "validator", "governance", "account", "friends", "oracle", "exchange", "market", "energy", "swap", "interop", "vault", "storage", "apps", "relay"});

            var tokenScript = new byte[0];
            CreateToken(StakingTokenSymbol, StakingTokenName, "neo", Hash.FromUnpaddedHex("ed07cffad18f1308db51920d99a2af60ac66a7b3"),  UnitConversion.ToBigInteger(91136374, StakingTokenDecimals), StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External, tokenScript);
            CreateToken(FuelTokenSymbol, FuelTokenName, PlatformName, Hash.FromString(FuelTokenSymbol), PlatformSupply, FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel, tokenScript);
            CreateToken(FiatTokenSymbol, FiatTokenName, PlatformName, Hash.FromString(FiatTokenSymbol), 0, FiatTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Fiat, tokenScript);

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                SetupNexusTx(owner),

                ValueCreateTx(owner, NexusProtocolVersionTag, 1, 1, 1000),
                ValueCreateTx(owner, ValidatorContract.ValidatorRotationTimeTag, 120, 30, 3600),
                ValueCreateTx(owner, ValidatorContract.ValidatorCountTag, 10, 1, 100),
                ValueCreateTx(owner, GasContract.MaxLoanAmountTag, new BigInteger(10000) * 9999, 9999, new BigInteger(10000)*9999),
                ValueCreateTx(owner, GasContract.MaxLenderCountTag, 10, 1, 100),
                ValueCreateTx(owner, ConsensusContract.PollVoteLimitTag, 50000, 100, 500000),
                ValueCreateTx(owner, ConsensusContract.MaxEntriesPerPollTag, 10, 2, 1000),
                ValueCreateTx(owner, ConsensusContract.MaximumPollLengthTag, 86400 * 90, 86400 * 2, 86400 * 120),

                ChainCreateTx(owner, "privacy", "privacy"),
                ChainCreateTx(owner, "sale", "sale"),

                ConsensusStakeCreateTx(owner)
            };

            var genesisMessage = Encoding.UTF8.GetBytes("A Phantasma was born...");
            var block = new Block(Chain.InitialHeight, RootChainAddress, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, genesisMessage);

            try
            {
                rootChain.AddBlock(block, transactions, 1);
            }
            catch (Exception e)
            {
                _logger?.Error(e.ToString());
                return false;
            }

            GenesisHash = block.Hash;
            this.Ready = true;
            return true;
        }

        public int GetConfirmationsOfHash(Hash hash)
        {
            var block = FindBlockByHash(hash);
            if (block != null)
            {
                return GetConfirmationsOfBlock(block);
            }

            var tx = FindTransactionByHash(hash);
            if (tx != null)
            {
                return GetConfirmationsOfTransaction(tx);
            }

            return 0;
        }

        public int GetConfirmationsOfTransaction(Transaction transaction)
        {
            Throw.IfNull(transaction, nameof(transaction));

            var block = FindBlockByTransaction(transaction);
            if (block == null)
            {
                return 0;
            }

            return GetConfirmationsOfBlock(block);
        }

        public int GetConfirmationsOfBlock(Block block)
        {
            Throw.IfNull(block, nameof(block));

            if (block != null)
            {
                var chain = FindChainForBlock(block);
                if (chain != null)
                {
                    var lastBlock = chain.LastBlock;
                    if (lastBlock != null)
                    {
                        return (int)(1 + (lastBlock.Height - block.Height));
                    }
                }
            }

            return 0;
        }
        #endregion

        #region VALIDATORS
        public Timestamp GetValidatorLastActivity(Address target)
        {
            throw new NotImplementedException();
        }

        public ValidatorEntry[] GetValidators()
        {
            var validators = (ValidatorEntry[])RootChain.InvokeContract("validator", "GetValidators").ToObject();
            return validators;
        }

        public int GetActiveValidatorCount()
        {
            var count = RootChain.InvokeContract("validator", "GetActiveValidatorCount").AsNumber();
            return (int)count;
        }

        public bool IsActiveValidator(Address address)
        {
            var result = RootChain.InvokeContract("validator", "IsActiveValidator", address).AsBool();
            return result;
        }

        public bool IsWaitingValidator(Address address)
        {
            var result = RootChain.InvokeContract("validator", "IsWaitingValidator", address).AsBool();
            return result;
        }

        // this returns true for both active and waiting
        public bool IsKnownValidator(Address address)
        {
            var result = RootChain.InvokeContract("validator", "IsKnownValidator", address).AsBool();
            return result;
        }

        public int GetIndexOfValidator(Address address)
        {
            if (!address.IsUser)
            {
                return -1;
            }

            if (RootChain == null)
            {
                return -1;
            }

            var result = (int)RootChain.InvokeContract("validator", "GetIndexOfValidator", address).AsNumber();
            return result;
        }

        public ValidatorEntry GetValidatorByIndex(int index)
        {
            if (RootChain == null)
            {
                return new ValidatorEntry()
                {
                    address = Address.Null,
                    election = new Timestamp(0),
                    status = ValidatorStatus.Invalid
                };
            }

            Throw.If(index < 0, "invalid validator index");

            var result = (ValidatorEntry) RootChain.InvokeContract("validator", "GetValidatorByIndex", (BigInteger)index).ToObject();
            return result;
        }
        #endregion

        #region STORAGE
        public Archive FindArchive(Hash hash)
        {
            if (_archiveEntries.ContainsKey(hash))
            {
                return _archiveEntries.Get(hash);
            }

            return null;
        }

        public bool ArchiveExists(Hash hash)
        {
            return _archiveEntries.ContainsKey(hash);
        }

        public bool IsArchiveComplete(Archive archive)
        {
            for (int i=0; i<archive.BlockCount; i++)
            {
                if (!HasArchiveBlock(archive, i))
                {
                    return false;
                }
            }

            return true;
        }

        public Archive CreateArchive(MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key)
        {
            var archive = FindArchive(merkleTree.Root);
            if (archive != null)
            {
                return archive;
            }

            archive = new Archive(merkleTree, size, flags, key);
            var archiveHash = merkleTree.Root;
            _archiveEntries.Set(archiveHash, archive);

            return archive;
        }

        public bool DeleteArchive(Archive archive)
        {
            Throw.IfNull(archive, nameof(archive));

            for (int i = 0; i < archive.BlockCount; i++)
            {
                var blockHash = archive.MerkleTree.GetHash(i);
                if (_archiveContents.ContainsKey(blockHash))
                {
                    _archiveContents.Remove(blockHash);
                }
            }

            _archiveEntries.Remove(archive.Hash);

            return true;
        }

        public bool HasArchiveBlock(Archive archive, int blockIndex)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = archive.MerkleTree.GetHash(blockIndex);
            return _archiveContents.ContainsKey(hash);
        }

        public void WriteArchiveBlock(Archive archive, byte[] content, int blockIndex)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.IfNull(content, nameof(content));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = MerkleTree.CalculateBlockHash(content);
            if (!archive.MerkleTree.VerifyContent(hash, blockIndex))
            {
                throw new ArchiveException("Block content mismatch");
            }

            _archiveContents.Set(hash, content);
        }

        public byte[] ReadArchiveBlock(Archive archive, int blockIndex)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = archive.MerkleTree.GetHash(blockIndex);
            return _archiveContents.Get(hash);
        }

        #endregion

        #region CHANNELS
        public BigInteger GetRelayBalance(Address address)
        {
            var chain = RootChain;
            try
            {
                var result = chain.InvokeContract("relay", "GetBalance", address).AsNumber();
                return result;
            }
            catch
            {
                return 0;
            }
        }
        #endregion

        #region PLATFORMS
        internal bool CreatePlatform(Address address, string name, string fuelSymbol)
        {
            // check if already exists something with that name
            if (PlatformExists(name))
            {
                return false;
            }

            var entry = new PlatformInfo()
            {
                Address = address,
                Name = name,
                Symbol = fuelSymbol
            };

            // add to persistent list of tokens
            var platformList = this.Platforms.ToList();
            platformList.Add(name);
            this.Platforms = platformList;

            EditPlatform(name, entry);
            return true;
        }

        private string GetPlatformInfoKey(string name)
        {
            return "platform:" + name;
        }

        private void EditPlatform(string name, PlatformInfo platformInfo)
        {
            var key = GetPlatformInfoKey(name);
            var bytes = Serialization.Serialize(platformInfo);
            _vars.Set(key, bytes);
        }

        public bool PlatformExists(string name)
        {
            if (name == Nexus.PlatformName)
            {
                return true;
            }

            var key = GetPlatformInfoKey(name);
            return _vars.ContainsKey(key);
        }

        public PlatformInfo GetPlatformInfo(string name)
        {
            var key = GetPlatformInfoKey(name);
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                return Serialization.Unserialize<PlatformInfo>(bytes);
            }

            throw new ChainException($"Platform does not exist ({name})");
        }
        #endregion
        
        /*
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            GenesisAddress.SerializeData(writer);

            int chainCount = _chainMap.Count;
            writer.WriteVarInt(chainCount);
            foreach (Chain entry in _chainMap.Values)
            {
                entry.SerializeData(writer);
            }

            int tokenCount = _tokenMap.Count;
            writer.WriteVarInt(tokenCount);
            foreach (Token entry in _tokenMap.Values)
            {
                entry.SerializeData(writer);
            }

            writer.WriteAddress(RootChain.Address);
            writer.WriteVarString(FuelToken.Symbol);
            writer.WriteVarString(StakingToken.Symbol);
            writer.WriteVarString(StableToken.Symbol);
        }*/
    }
}
