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
using Phantasma.Domain;

namespace Phantasma.Blockchain
{
    public enum ChainStorageShard
    {
        Data,
        Transactions,
        Blocks,
        Heights,
        TxBlockMap,
    }

    public class Nexus : INexus
    {
        public static readonly string RootChainName = "main";
        private static readonly string ChainNameMapKey = "chain.name.";
        private static readonly string ChainAddressMapKey = "chain.addr.";
        private static readonly string ChainOwnerKey = "chain.owner.";
        private static readonly string ChainParentNameKey = "chain.parent.";
        private static readonly string ChainChildrenBlockKey = "chain.children.";

        public const string GasContractName = "gas";
        public const string TokenContractName = "token";
        public const string BlockContractName = "block";
        public const string NexusContractName = "nexus";
        public const string StakeContractName = "stake";
        public const string SwapContractName = "swap";
        public const string AccountContractName = "account";
        public const string ConsensusContractName = "consensus";
        public const string GovernanceContractName = "governance";
        public const string StorageContractName = "storage";
        public const string ValidatorContractName = "validator";
        public const string InteropContractName = "interop";
        public const string ExchangeContractName = "exchange";
        public const string PrivacyContractName = "privacy";
        public const string RelayContractName = "relay";
        public const string BombContractName = "bomb";
        public const string RankingContractName = "ranking";

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

        private Dictionary<string, KeyValueStore<BigInteger, TokenContent>> _tokenContents = new Dictionary<string, KeyValueStore<BigInteger, TokenContent>>();

        private KeyValueStore<Hash, Archive> _archiveEntries;
        private KeyValueStore<Hash, byte[]> _archiveContents;

        public bool HasGenesis { get; private set; }

        public string Name
        {
            get
            {
                if (RootStorage.Has(nameof(Name)))
                {
                    var bytes = RootStorage.Get(nameof(Name));
                    var result = Serialization.Unserialize<string>(bytes);
                    return result;
                }

                return null;
            }

            private set
            {
                var bytes = Serialization.Serialize(value);
                RootStorage.Put(nameof(Name), bytes);
            }
        }

        public Address RootChainAddress
        {
            get
            {
                if (RootStorage.Has(nameof(RootChainAddress)))
                {
                    return Serialization.Unserialize<Address>(RootStorage.Get(nameof(RootChainAddress)));
                }

                return Address.Null;
            }

            private set
            {
                RootStorage.Put(nameof(RootChainAddress), Serialization.Serialize(value));
            }
        }

        public Address GenesisAddress
        {
            get
            {
                if (RootStorage.Has(nameof(GenesisAddress)))
                {
                    return Serialization.Unserialize<Address>(RootStorage.Get(nameof(GenesisAddress)));
                }

                return Address.Null;
            }

            private set
            {
                RootStorage.Put(nameof(GenesisAddress), Serialization.Serialize(value));
            }
        }

        public Hash GenesisHash
        {
            get
            {
                if (RootStorage.Has(nameof(GenesisHash)))
                {
                    var result = Serialization.Unserialize<Hash>(RootStorage.Get(nameof(GenesisHash)));
                    return result;
                }

                return Hash.Null;
            }

            private set
            {
                RootStorage.Put(nameof(GenesisHash), Serialization.Serialize(value));
            }
        }

        private Timestamp _genesisDate;
        public Timestamp GenesisTime
        {
            get
            {
                if (_genesisDate.Value == 0 && HasGenesis)
                {
                    var genesisBlock = RootChain.FindBlockByHash(GenesisHash);
                    _genesisDate = genesisBlock.Timestamp;
                }

                return _genesisDate;
            }
        }

        public string[] Tokens
        {
            get
            {
                if (RootStorage.Has(nameof(Tokens)))
                {
                    return Serialization.Unserialize<string[]>(RootStorage.Get(nameof(Tokens)));
                }

                return new string[0];
            }

            private set
            {
                RootStorage.Put(nameof(Tokens), Serialization.Serialize(value));
            }
        }

        public string[] Contracts
        {
            get
            {
                if (RootStorage.Has(nameof(Contracts)))
                {
                    return Serialization.Unserialize<string[]>(RootStorage.Get(nameof(Contracts)));
                }

                return new string[0];
            }

            private set
            {
                RootStorage.Put(nameof(Contracts), Serialization.Serialize(value));
            }
        }

        public string[] Chains
        {
            get
            {
                if (RootStorage.Has(nameof(Chains)))
                {
                    return Serialization.Unserialize<string[]>(RootStorage.Get(nameof(Chains)));
                }

                return new string[0];
            }

            private set
            {
                RootStorage.Put(nameof(Chains), Serialization.Serialize(value));
            }
        }

        public string[] Platforms
        {
            get
            {
                if (RootStorage.Has(nameof(Platforms)))
                {
                    return Serialization.Unserialize<string[]>(RootStorage.Get(nameof(Platforms)));
                }

                return new string[0];
            }

            private set
            {
                RootStorage.Put(nameof(Platforms), Serialization.Serialize(value));
            }
        }

        public string[] Feeds
        {
            get
            {
                if (RootStorage.Has(nameof(Feeds)))
                {
                    return Serialization.Unserialize<string[]>(RootStorage.Get(nameof(Feeds)));
                }

                return new string[0];
            }

            private set
            {
                RootStorage.Put(nameof(Feeds), Serialization.Serialize(value));
            }
        }

        private readonly List<IChainPlugin> _plugins = new List<IChainPlugin>();

        private readonly Logger _logger;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private Func<Nexus, OracleReader> _oracleFactory = null;

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(Logger logger = null, Func<string, IKeyValueStoreAdapter> adapterFactory = null, Func<Nexus, OracleReader> oracleFactory = null)
        {
            this._adapterFactory = adapterFactory;
            this._oracleFactory = oracleFactory;
            try
            {
                var temp = this.Name;
                HasGenesis = !string.IsNullOrEmpty(temp);

                if (HasGenesis)
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
                HasGenesis = false;
            }

            _archiveEntries = new KeyValueStore<Hash, Archive>(CreateKeyStoreAdapter("archives"));
            _archiveContents = new KeyValueStore<Hash, byte[]>(CreateKeyStoreAdapter("contents"));

            _logger = logger;
        }

        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache = new Dictionary<string, IKeyValueStoreAdapter>();

        internal IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
        {
            if (_keystoreCache.ContainsKey(name))
            {
                return _keystoreCache[name];
            }

            IKeyValueStoreAdapter result;

            if (_adapterFactory != null)
            {
                result = _adapterFactory(name);
                Throw.If(result == null, "keystore adapter factory failed");
            }
            else
            {
                result = new MemoryStore();
            }

            _keystoreCache[name] = result;
            return result;
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
        public Address LookUpName(StorageContext storage, string name)
        {
            if (!ValidationUtils.ValidateName(name))
            {
                return Address.Null;
            }

            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.LookUpName), name).AsAddress();
        }

        public string LookUpAddressName(StorageContext storage, Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.LookUpAddress), address).AsString();
        }

        public byte[] LookUpAddressScript(StorageContext storage, Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.LookUpScript), address).AsByteArray();
        }

        public bool HasScript(StorageContext storage, Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.HasScript), address).AsBool();
        }
        #endregion

        #region CONTRACTS
        public SmartContract AllocContractByName(string contractName)
        {
            Throw.IfNullOrEmpty(contractName, nameof(contractName));
            var address = SmartContract.GetAddressForName(contractName);
            var result = AllocContractByAddress(address);

            if (result == null)
            {
                throw new Exception("Unknown contract: " + contractName);
            }

            return result;
        }

        private Dictionary<Address, Type> _contractMap = null;
        private void RegisterContract<T>() where T : SmartContract
        {
            var alloc = (SmartContract)Activator.CreateInstance<T>();
            var addr = alloc.Address;
            _contractMap[addr] = typeof(T);
        }

        public SmartContract AllocContractByAddress(Address contractAdress)
        {
            if (_contractMap == null)
            {
                _contractMap = new Dictionary<Address, Type>();
                RegisterContract<NexusContract>();
                RegisterContract<ValidatorContract>();
                RegisterContract<GovernanceContract>();
                RegisterContract<ConsensusContract>();
                RegisterContract<AccountContract>();
                RegisterContract<FriendContract>();
                RegisterContract<ExchangeContract>();
                RegisterContract<MarketContract>();
                RegisterContract<StakeContract>();
                RegisterContract<TokenContract>();
                RegisterContract<SwapContract>();
                RegisterContract<GasContract>();
                RegisterContract<BlockContract>();
                RegisterContract<RelayContract>();
                RegisterContract<StorageContract>();
                RegisterContract<VaultContract>();
                RegisterContract<SaleContract>();
                RegisterContract<InteropContract>();
                RegisterContract<NachoContract>();
                RegisterContract<BombContract>();
                RegisterContract<RankingContract>();
            }

            if (_contractMap.ContainsKey(contractAdress)) {
                var type = _contractMap[contractAdress];
                return (SmartContract)Activator.CreateInstance(type);
            }

            return null;
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
        internal Chain CreateChain(StorageContext storage, Address owner, string name, string parentChainName)
        {
            if (name != RootChainName)
            {
                if (string.IsNullOrEmpty(parentChainName))
                {
                    return null;
                }

                if (!ChainExists(storage, parentChainName))
                {
                    return null;
                }
            }

            if (!Chain.ValidateName(name))
            {
                return null;
            }

            // check if already exists something with that name
            if (ChainExists(storage, name))
            {
                return null;
            }

            var chain = new Chain(this, name, _logger);

            chain.DeployNativeContract(storage, SmartContract.GetAddressForName(GasContractName));
            chain.DeployNativeContract(storage, SmartContract.GetAddressForName(BlockContractName));
            chain.DeployNativeContract(storage, SmartContract.GetAddressForName(TokenContractName));

            // add to persistent list of chains
            var chainList = this.Chains.ToList();
            chainList.Add(name);
            this.Chains = chainList.ToArray();

            // add address and name mapping 
            storage.Put(ChainNameMapKey + chain.Name, chain.Address.PublicKey);
            storage.Put(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
            storage.Put(ChainOwnerKey + chain.Name, owner.PublicKey);

            if (!string.IsNullOrEmpty(parentChainName))
            {
                storage.Put(ChainParentNameKey + chain.Name, Encoding.UTF8.GetBytes(parentChainName));
                var childrenList = GetChildrenListOfChain(storage, parentChainName);
                childrenList.Add<string>(chain.Name);
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
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
                return Encoding.UTF8.GetString(bytes);
            }

            return null;
        }

        public bool ChainExists(StorageContext storage, string chainName)
        {
            if (string.IsNullOrEmpty(chainName))
            {
                return false;
            }

            var key = ChainNameMapKey + chainName;
            return storage.Has(key);
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
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
                var parentName = Encoding.UTF8.GetString(bytes);
                return parentName;
            }

            throw new Exception("Parent name not found for chain: " + chainName);
        }

        public Address GetChainOwnerByName(string chainName)
        {
            var key = ChainOwnerKey + chainName;
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
                var owner = new Address(bytes);
                return owner;
            }

            return GenesisAddress;
        }

        public IEnumerable<string> GetChildChainsByAddress(StorageContext storage, Address chainAddress)
        {
            var chain = FindChainByAddress(chainAddress);
            if (chain == null)
            {
                return null;
            }

            return GetChildChainsByName(storage, chain.Name);
        }

        public OracleReader CreateOracleReader()
        {
            Throw.If(_oracleFactory == null, "oracle factory is not setup");
            return _oracleFactory(this);
        }

        public IEnumerable<string> GetChildChainsByName(StorageContext storage, string chainName)
        {
            var list = GetChildrenListOfChain(storage, chainName);
            var count = (int)list.Count();
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = list.Get<string>(i);
            }

            return names;
        }

        private StorageList GetChildrenListOfChain(StorageContext storage, string chainName)
        {
            var key = Encoding.UTF8.GetBytes(ChainChildrenBlockKey + chainName);
            var list = new StorageList(key, storage);
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

            if (ChainExists(RootStorage, name))
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
        internal bool CreateFeed(Address owner, string name, FeedMode mode)
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
            this.Feeds = feedList.ToArray();

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
            RootStorage.Put(key, bytes);
        }

        public bool FeedExists(string name)
        {
            var key = GetFeedInfoKey(name);
            return RootStorage.Has(key);
        }

        public OracleFeed GetFeedInfo(string name)
        {
            var key = GetFeedInfoKey(name);
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
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
                Throw.If(c < 'A' || c > 'Z', "Symbol must only contain capital letters, no other characters are allowed");
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
            this.Tokens = tokenList.ToArray();

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
            RootStorage.Put(key, bytes);
        }

        public bool TokenExists(string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            return RootStorage.Has(key);
        }

        public TokenInfo GetTokenInfo(string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
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

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint, target, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(target, isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint, target, symbol, amount);
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

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint, target, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(target, isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint, target, symbol, tokenID);
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

            if (tokenInfo.IsCapped() && !supply.Burn(runtimeVM.ChangeSet, amount))
            {
                return false;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Subtract(runtimeVM.ChangeSet, target, amount))
            {
                return false;
            }

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, target, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(target, isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn, target, symbol, amount);
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

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, target, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(target, isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn, target, symbol, tokenID);
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

            if (destination.IsNull)
            {
                return false;
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

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, TokenTrigger.OnSend, source, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, TokenTrigger.OnReceive, destination, symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(source, AccountTrigger.OnSend, source, symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(destination, AccountTrigger.OnReceive, destination, symbol, amount);
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

            if (destination.IsNull)
            {
                return false;
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

            var tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, TokenTrigger.OnSend, source, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            tokenTriggerResult = runtimeVM.InvokeTriggerOnToken(tokenInfo, TokenTrigger.OnReceive, destination, symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(source, AccountTrigger.OnSend, source, symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            accountTriggerResult = runtimeVM.InvokeTriggerOnAccount(destination, AccountTrigger.OnReceive, destination, symbol, tokenID);
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

            if (RootStorage.Has(key))
            {
                bytes = RootStorage.Get(key);
                tokenID = Serialization.Unserialize<BigInteger>(bytes);
                tokenID++;
            }
            else
            {
                tokenID = 1;
            }

            bytes = Serialization.Serialize(tokenID);
            RootStorage.Put(key, bytes);

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

            var deployInterop = "Runtime.DeployContract";
            sb.CallInterop(deployInterop, NexusContractName);
            sb.CallInterop(deployInterop, ValidatorContractName);
            sb.CallInterop(deployInterop, GovernanceContractName);
            sb.CallInterop(deployInterop, ConsensusContractName);
            sb.CallInterop(deployInterop, AccountContractName);
            sb.CallInterop(deployInterop, ExchangeContractName);
            sb.CallInterop(deployInterop, SwapContractName);
            sb.CallInterop(deployInterop, InteropContractName);
            sb.CallInterop(deployInterop, StakeContractName);
            sb.CallInterop(deployInterop, InteropContractName);
            sb.CallInterop(deployInterop, StorageContractName);
            sb.CallInterop(deployInterop, RelayContractName);
            sb.CallInterop(deployInterop, BombContractName);
            sb.CallInterop(deployInterop, RankingContractName);
            sb.CallInterop(deployInterop, PrivacyContractName);
            sb.CallInterop(deployInterop, "friends");
            sb.CallInterop(deployInterop, "market");
            sb.CallInterop(deployInterop, "vault");
            sb.CallInterop(deployInterop, "apps");

            sb.CallContract("block", "OpenBlock", owner.Address);

            sb.CallContract(Nexus.TokenContractName, "MintTokens", owner.Address, owner.Address, StakingTokenSymbol, UnitConversion.ToBigInteger(8863626, StakingTokenDecimals));
            // requires staking token to be created previously
            // note this is a completly arbitrary number just to be able to generate energy in the genesis, better change it later
            sb.CallContract(Nexus.StakeContractName, "Stake", owner.Address, UnitConversion.ToBigInteger(100000, StakingTokenDecimals));
            sb.CallContract(Nexus.StakeContractName, "Claim", owner.Address, owner.Address);
            sb.CallContract(Nexus.BombContractName, "Initialize", owner.Address);

            sb.Emit(VM.Opcode.RET);
            sb.EmitRaw(Encoding.UTF8.GetBytes("A Phantasma was born..."));

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
                CallContract("validator", "SetValidator", owner.Address, new BigInteger(0), ValidatorType.Primary).
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
            if (HasGenesis)
            {
                return false;
            }

            if (!ValidationUtils.ValidateName(name))
            {
                throw new ChainException("invalid nexus name");
            }

            this.Name = name;

            this.GenesisAddress = owner.Address;

            var rootChain = CreateChain(RootStorage, owner.Address, RootChainName, null);

            var tokenScript = new byte[0];
            CreateToken(StakingTokenSymbol, StakingTokenName, "neo", Hash.FromUnpaddedHex("ed07cffad18f1308db51920d99a2af60ac66a7b3"), UnitConversion.ToBigInteger(91136374, StakingTokenDecimals), StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External, tokenScript);
            CreateToken(FuelTokenSymbol, FuelTokenName, PlatformName, Hash.FromString(FuelTokenSymbol), PlatformSupply, FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel, tokenScript);
            CreateToken(FiatTokenSymbol, FiatTokenName, PlatformName, Hash.FromString(FiatTokenSymbol), 0, FiatTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Fiat, tokenScript);

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                SetupNexusTx(owner),

                ValueCreateTx(owner, NexusProtocolVersionTag, 1, 1, 1000),
                ValueCreateTx(owner, ValidatorContract.ValidatorCountTag, 1, 1, 100),
                ValueCreateTx(owner, ValidatorContract.ValidatorRotationTimeTag, 120, 30, 3600),
                ValueCreateTx(owner, GasContract.MaxLoanAmountTag, new BigInteger(10000) * 9999, 9999, new BigInteger(10000)*9999),
                ValueCreateTx(owner, GasContract.MaxLenderCountTag, 10, 1, 100),
                ValueCreateTx(owner, ConsensusContract.PollVoteLimitTag, 50000, 100, 500000),
                ValueCreateTx(owner, ConsensusContract.MaxEntriesPerPollTag, 10, 2, 1000),
                ValueCreateTx(owner, ConsensusContract.MaximumPollLengthTag, 86400 * 90, 86400 * 2, 86400 * 120),
                ValueCreateTx(owner, StakeContract.MasterStakeThresholdTag, StakeContract.DefaultMasterThreshold, UnitConversion.ToBigInteger(1000, Nexus.StakingTokenDecimals), UnitConversion.ToBigInteger(200000, Nexus.StakingTokenDecimals)),
                ValueCreateTx(owner, StakeContract.VotingStakeThresholdTag, UnitConversion.ToBigInteger(1000, Nexus.StakingTokenDecimals), UnitConversion.ToBigInteger(1, Nexus.StakingTokenDecimals), UnitConversion.ToBigInteger(10000, Nexus.StakingTokenDecimals)),

                ChainCreateTx(owner, "sale", "sale"),

                ConsensusStakeCreateTx(owner)
            };

            var block = new Block(Chain.InitialHeight, RootChainAddress, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, 0);

            rootChain.AddBlock(block, transactions, 1);

            GenesisHash = block.Hash;
            this.HasGenesis = true;
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
            var validators = (ValidatorEntry[])RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetValidators)).ToObject();
            return validators;
        }

        public int GetPrimaryValidatorCount()
        {
            var count = RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
            if (count < 1)
            {
                return 1;
            }
            return (int)count;
        }

        public int GetSecondaryValidatorCount()
        {
            var count = RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetValidatorCount), ValidatorType.Primary).AsNumber();
            return (int)count;
        }

        public ValidatorType GetValidatorType(Address address)
        {
            var result = RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetValidatorType), address).AsEnum<ValidatorType>();
            return result;
        }

        public bool IsPrimaryValidator(Address address)
        {
            var result = GetValidatorType(address);
            return result == ValidatorType.Primary;
        }

        public bool IsSecondaryValidator(Address address)
        {
            var result = GetValidatorType(address);
            return result == ValidatorType.Secondary;
        }

        // this returns true for both active and waiting
        public bool IsKnownValidator(Address address)
        {
            var result = GetValidatorType(address);
            return result != ValidatorType.Invalid;
        }

        public BigInteger GetStakeFromAddress(Address address)
        {
            var result = RootChain.InvokeContract(this.RootStorage, Nexus.StakeContractName, nameof(StakeContract.GetStake), address).AsNumber();
            return result;
        }

        public bool IsStakeMaster(Address address)
        {
            var stake = GetStakeFromAddress(address);
            var masterThresold = RootChain.InvokeContract(this.RootStorage, Nexus.StakeContractName, nameof(StakeContract.GetMasterThreshold)).AsNumber();
            return stake >= masterThresold;
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

            var result = (int)RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetIndexOfValidator), address).AsNumber();
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
                    type = ValidatorType.Invalid
                };
            }

            Throw.If(index < 0, "invalid validator index");

            var result = (ValidatorEntry)RootChain.InvokeContract(this.RootStorage, Nexus.ValidatorContractName, nameof(ValidatorContract.GetValidatorByIndex), (BigInteger)index).ToObject();
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
            for (int i = 0; i < archive.BlockCount; i++)
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
                var result = chain.InvokeContract(this.RootStorage, "relay", "GetBalance", address).AsNumber();
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

            var entry = new PlatformInfo(name, fuelSymbol, address);

            // add to persistent list of tokens
            var platformList = this.Platforms.ToList();
            platformList.Add(name);
            this.Platforms = platformList.ToArray();

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
            RootStorage.Put(key, bytes);
        }

        public bool PlatformExists(string name)
        {
            if (name == Nexus.PlatformName)
            {
                return true;
            }

            var key = GetPlatformInfoKey(name);
            return RootStorage.Has(key);
        }

        public PlatformInfo GetPlatformInfo(string name)
        {
            var key = GetPlatformInfoKey(name);
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
                return Serialization.Unserialize<PlatformInfo>(bytes);
            }

            throw new ChainException($"Platform does not exist ({name})");
        }
        #endregion

        public int GetIndexOfChain(string name)
        {
            var chains = this.Chains;
            int index = 0;
            foreach (var chain in chains)
            {
                if (chain == name)
                {
                    return index;
                }

                index++;
            }
            return -1;
        }

        public IKeyValueStoreAdapter GetChainStorage(string name, ChainStorageShard shard)
        {
            var shardName = shard.ToString().ToLower();
            return this.CreateKeyStoreAdapter($"chain.{name}.{shardName}");
        }

        public BigInteger GetGovernanceValue(StorageContext storage, string name)
        {
            if (HasGenesis)
            {
                return RootChain.InvokeContract(storage, Nexus.GovernanceContractName, "GetValue", name).AsNumber();
            }

            return 0;
        } 

        public StorageContext RootStorage => new KeyStoreStorage(GetChainStorage(RootChainName, ChainStorageShard.Data));
    }
}
