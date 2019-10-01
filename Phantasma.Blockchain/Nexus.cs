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
using Phantasma.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Storage.Context;
using Phantasma.Domain;
using Phantasma.Contracts;
using Phantasma.Contracts.Extra;
using System.Diagnostics;

namespace Phantasma.Blockchain
{
    public class Nexus : INexus
    {
        private static readonly string ChainNameMapKey = "chain.name.";
        private static readonly string ChainAddressMapKey = "chain.addr.";
        private static readonly string ChainOwnerKey = "chain.owner.";
        private static readonly string ChainParentNameKey = "chain.parent.";
        private static readonly string ChainChildrenBlockKey = "chain.children.";

        public const string GasContractName = "gas";
        public const string BlockContractName = "block";
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

        public Chain RootChain => GetChainByName(DomainSettings.RootChainName);

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
                    var genesisBlock = RootChain.GetBlockByHash(GenesisHash);
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
                        GetChainByName(chainName);
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
                var chain = GetChainByName(chainName);
                if (chain.ContainsBlockHash(hash))
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
                var chain = GetChainByName(chainName);
                if (chain.ContainsTransaction(hash))
                {
                    var blockHash = chain.GetBlockHashOfTransaction(hash);
                    return chain.GetBlockByHash(blockHash);
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
            if (!ValidationUtils.IsValidIdentifier(name))
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

        public bool HasAddressScript(StorageContext storage, Address address)
        {
            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.HasScript), address).AsBool();
        }
        #endregion

        #region CONTRACTS
        public SmartContract GetContractByName(string contractName)
        {
            Throw.IfNullOrEmpty(contractName, nameof(contractName));
            var address = SmartContract.GetAddressForName(contractName);
            var result = GetContractByAddress(address);

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

        public SmartContract GetContractByAddress(Address contractAdress)
        {
            if (_contractMap == null)
            {
                _contractMap = new Dictionary<Address, Type>();
                RegisterContract<ValidatorContract>();
                RegisterContract<GovernanceContract>();
                RegisterContract<ConsensusContract>();
                RegisterContract<AccountContract>();
                RegisterContract<FriendsContract>();
                RegisterContract<ExchangeContract>();
                RegisterContract<MarketContract>();
                RegisterContract<StakeContract>();
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
                RegisterContract<FriendsContract>();
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
                var chain = GetChainByName(chainName);
                var tx = chain.GetTransactionByHash(hash);
                if (tx != null)
                {
                    return tx;
                }
            }

            return null;
        }

        #endregion

        #region CHAINS
        internal bool CreateChain(StorageContext storage, Address owner, string name, string parentChainName)
        {
            if (name != DomainSettings.RootChainName)
            {
                if (string.IsNullOrEmpty(parentChainName))
                {
                    return false;
                }

                if (!ChainExists(storage, parentChainName))
                {
                    return false;
                }
            }

            if (!ValidationUtils.IsValidIdentifier(name))
            {
                return false;
            }

            // check if already exists something with that name
            if (ChainExists(storage, name))
            {
                return false;
            }

            if (PlatformExists(name))
            {
                return false;
            }

            var chain = new Chain(this, name, _logger);

            // add to persistent list of chains
            var chainList = this.Chains.ToList();
            chainList.Add(name);
            this.Chains = chainList.ToArray();

            // add address and name mapping 
            storage.Put(ChainNameMapKey + chain.Name, chain.Address.ToByteArray());
            storage.Put(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
            storage.Put(ChainOwnerKey + chain.Name, owner.ToByteArray());

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

            return true;
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
            var chain = GetChainByAddress(address);
            if (chain == null)
            {
                return null;
            }
            return GetParentChainByName(chain.Name);
        }

        public string GetParentChainByName(string chainName)
        {
            if (chainName == DomainSettings.RootChainName)
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
                var owner = Address.Unserialize(bytes);
                return owner;
            }

            return GenesisAddress;
        }

        public IEnumerable<string> GetChildChainsByAddress(StorageContext storage, Address chainAddress)
        {
            var chain = GetChainByAddress(chainAddress);
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

        public Chain GetChainByAddress(Address address)
        {
            var name = LookUpChainNameByAddress(address);
            if (string.IsNullOrEmpty(name))
            {
                return null; // TODO should be exception
            }

            return GetChainByName(name);
        }

        public Chain GetChainByName(string name)
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
        internal bool CreateFeed(StorageContext storage, Address owner, string name, FeedMode mode)
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
        internal void CreateToken(StorageContext storage, string symbol, string name, string platform, Hash hash, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script)
        {
            var tokenInfo = new TokenInfo(symbol, name, platform, hash, maxSupply, decimals, flags, script);
            EditToken(symbol, tokenInfo);

            // add to persistent list of tokens
            var tokenList = this.Tokens.ToList();
            tokenList.Add(symbol);
            this.Tokens = tokenList.ToArray();
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

        internal void MintTokens(RuntimeVM Runtime, IToken token, Address source, Address target, string sourceChain, BigInteger amount)
        {
            Runtime.Expect(token.IsFungible(), "must be fungible");
            Runtime.Expect(amount > 0, "invalid amount");

            var isSettlement = sourceChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
            Runtime.Expect(supply.Mint(Runtime.Storage, amount, token.MaxSupply), "mint supply failed");

            var balances = new BalanceSheet(token.Symbol);
            Runtime.Expect(balances.Add(Runtime.Storage, target, amount), "balance add failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(token, tokenTrigger, target, token.Symbol, amount), $"token {tokenTrigger} trigger failed");

            var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(target, accountTrigger, target, token.Symbol, amount), $"token {tokenTrigger} trigger failed");

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, sourceChain));
                Runtime.Notify(EventKind.TokenClaim, target, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenMint, target, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
        }

        // NFT version
        internal void MintToken(RuntimeVM Runtime, IToken token, Address source, Address target, string sourceChain, BigInteger tokenID, byte[] rom, byte[] ram)
        {
            Runtime.Expect(!token.IsFungible(), "cant be fungible");

            var isSettlement = sourceChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
            Runtime.Expect(supply.Mint(Runtime.Storage, 1, token.MaxSupply), "supply mint failed");

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Add(Runtime.Storage, target, tokenID), "ownership add failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(token, tokenTrigger, target, token.Symbol, tokenID), $"token {tokenTrigger} trigger failed");

            var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(target, accountTrigger, target, token.Symbol, tokenID), $"token {tokenTrigger} trigger failed");

            WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, target, rom, ram, !isSettlement);

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, sourceChain));
                Runtime.Notify(EventKind.TokenClaim, target, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenMint, target, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
        }

        internal bool BurnTokens(RuntimeVM Runtime, IToken token, Address source, Address target, string targetChain, BigInteger amount)
        {
            if (!token.Flags.HasFlag(TokenFlags.Fungible))
            {
                return false;
            }

            if (amount <= 0)
            {
                return false;
            }

            var isSettlement = targetChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);

            if (token.IsCapped() && !supply.Burn(Runtime.Storage, amount))
            {
                return false;
            }

            var balances = new BalanceSheet(token.Symbol);
            if (!balances.Subtract(Runtime.Storage, source, amount))
            {
                return false;
            }

            var tokenTriggerResult = Runtime.InvokeTriggerOnToken(token, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, source, token.Symbol, amount);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = Runtime.InvokeTriggerOnAccount(source, isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn, source, token.Symbol, amount);
            if (!accountTriggerResult)
            {
                return false;
            }

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenEscrow, target, new TokenEventData(token.Symbol, amount, targetChain));
            }
            else
            {
                Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }

            return true;
        }

        // NFT version
        internal void BurnToken(RuntimeVM Runtime, IToken token, Address source, Address target, string targetChain, BigInteger tokenID)
        {
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "can't be fungible");

            var isSettlement = targetChain != Runtime.Chain.Name;

            var nft = Runtime.ReadToken(token.Symbol, tokenID);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

            var chain = RootChain;
            var supply = new SupplySheet(token.Symbol, chain, this);

            Runtime.Expect(supply.Burn(Runtime.Storage, 1), "supply burning failed");

            if (!isSettlement)
            {
                Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                DestroyNFT(Runtime, token.Symbol, tokenID);
            }

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(token, tokenTrigger, source, token.Symbol, tokenID), $"token {tokenTrigger} trigger failed: ");

            var accountTrigger = isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(source, accountTrigger, source, token.Symbol, tokenID), $"accont {accountTrigger} trigger failed: ");

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenEscrow, target, new TokenEventData(token.Symbol, tokenID, targetChain));
                Runtime.Notify(EventKind.PackedNFT, target, new PackedNFTData(token.Symbol, nft.ROM, nft.RAM));
            }
            else
            {
                Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
        }

        internal void TransferTokens(RuntimeVM Runtime, IToken token, Address source, Address destination, BigInteger amount)
        {
            if (source == destination)
            {
                return;
            }

            if (!token.Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (!token.Flags.HasFlag(TokenFlags.Fungible))
            {
                throw new Exception("Should be fungible");
            }

            Runtime.Expect(amount > 0, "invalid amount");
            Runtime.Expect(!destination.IsNull, "invalid destination");

            var balances = new BalanceSheet(token.Symbol);
            Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount), "balance subtract failed");
            Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), "balance add failed");

            Runtime.Expect(Runtime.InvokeTriggerOnToken(token, TokenTrigger.OnSend, source, token.Symbol, amount), "token onSend trigger failed");
            Runtime.Expect(Runtime.InvokeTriggerOnToken(token, TokenTrigger.OnReceive, destination, token.Symbol, amount), "token onReceive trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnAccount(source, AccountTrigger.OnSend, source, token.Symbol, amount), "account onSend trigger failed");
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(destination, AccountTrigger.OnReceive, destination, token.Symbol, amount), "account onReceive trigger failed");

            if (destination.IsSystem && destination == Runtime.CurrentContext.Address)
            {
                Runtime.Notify(EventKind.TokenEscrow, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            if (source.IsSystem && source == Runtime.CurrentContext.Address)
            {
                Runtime.Notify(EventKind.TokenClaim, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
        }

        internal bool TransferToken(RuntimeVM Runtime, IToken token, Address source, Address destination, BigInteger tokenID)
        {
            if (!token.Flags.HasFlag(TokenFlags.Transferable))
            {
                throw new Exception("Not transferable");
            }

            if (token.Flags.HasFlag(TokenFlags.Fungible))
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

            var ownerships = new OwnershipSheet(token.Symbol);
            if (!ownerships.Remove(Runtime.Storage, source, tokenID))
            {
                return false;
            }

            if (!ownerships.Add(Runtime.Storage, destination, tokenID))
            {
                return false;
            }

            var tokenTriggerResult = Runtime.InvokeTriggerOnToken(token, TokenTrigger.OnSend, source, token.Symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            tokenTriggerResult = Runtime.InvokeTriggerOnToken(token, TokenTrigger.OnReceive, destination, token.Symbol, tokenID);
            if (!tokenTriggerResult)
            {
                return false;
            }

            var accountTriggerResult = Runtime.InvokeTriggerOnAccount(source, AccountTrigger.OnSend, source, token.Symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            accountTriggerResult = Runtime.InvokeTriggerOnAccount(destination, AccountTrigger.OnReceive, destination, token.Symbol, tokenID);
            if (!accountTriggerResult)
            {
                return false;
            }

            var nft = ReadNFT(Runtime, token.Symbol, tokenID);
            WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, destination, nft.ROM, nft.RAM, true);

            if (destination.IsSystem && destination == Runtime.CurrentContext.Address)
            {
                Runtime.Notify(EventKind.TokenEscrow, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            if (source.IsSystem && source == Runtime.CurrentContext.Address)
            {
                Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }

            return true;
        }

        #endregion

        #region NFT
        internal byte[] GetKeyForNFT(string symbol)
        {
            var str = $".nft." + symbol;
            return Encoding.UTF8.GetBytes(str);
        }

        internal BigInteger CreateNFT(RuntimeVM Runtime, string symbol, string chainName, Address targetAddress, byte[] rom, byte[] ram)
        {
            Runtime.Expect(rom != null && rom.Length > 0, "invalid nft rom");
            Runtime.Expect(ram != null, "invalid nft ram");

            var key = GetKeyForNFT(symbol);
            var nftMap = new StorageMap(key, Runtime.Storage);

            var tokenID = Hash.FromBytes(rom);
            Runtime.Expect(!nftMap.ContainsKey<Hash>(tokenID), "nft with same hash already exists");

            var content = new TokenContent(chainName, targetAddress, rom, ram);
            nftMap.Set<Hash, TokenContent>(tokenID, content);
            return tokenID;
        }

        internal void DestroyNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID)
        {
            Runtime.Expect(false, "not supported yet");

            var key = GetKeyForNFT(symbol);
            var nftMap = new StorageMap(key, Runtime.Storage);

            Hash tokenHash = tokenID;
            Runtime.Expect(nftMap.ContainsKey<Hash>(tokenHash), "nft does not exists");
            nftMap.Remove<Hash>(tokenHash);
        }

    
        internal void WriteNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID, string chainName, Address owner, byte[] rom, byte[] ram, bool mustExist)
        {
            Runtime.Expect(ram != null && ram.Length < TokenContent.MaxRAMSize, "invalid nft ram update");

            var key = GetKeyForNFT(symbol);
            var nftMap = new StorageMap(key, Runtime.Storage);

            Hash tokenHash = tokenID;
            if (nftMap.ContainsKey<Hash>(tokenHash))
            {
                var content = nftMap.Get<Hash, TokenContent>(tokenHash);

                Runtime.Expect(rom.SequenceEqual(content.ROM), "invalid nft rom");

                content = new TokenContent(content.CurrentChain, content.CurrentOwner, content.ROM, ram);
                nftMap.Set<Hash, TokenContent>(tokenHash, content);
            }
            else
            {
                Runtime.Expect(!mustExist, "nft does not exist");
                var genID = CreateNFT(Runtime, symbol, chainName, owner, rom, ram);
                Runtime.Expect(genID == tokenID, "failed to regenerate NFT");
            }
        }

        public TokenContent ReadNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID)
        {
            var key = GetKeyForNFT(symbol);
            var nftMap = new StorageMap(key, Runtime.Storage);

            Hash tokenHash = tokenID;
            Runtime.Expect(nftMap.ContainsKey<Hash>(tokenHash), "nft does not exists");

            return nftMap.Get<Hash, TokenContent>(tokenHash);
        }
        #endregion

        #region GENESIS
        private Transaction BeginNexusCreateTx(PhantasmaKeys owner)
        {
            var sb = ScriptUtils.BeginScript();

            var deployInterop = "Runtime.DeployContract";
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
            sb.CallInterop(deployInterop, RankingContractName);
            sb.CallInterop(deployInterop, BombContractName);
            //sb.CallInterop(deployInterop, PrivacyContractName);
            sb.CallInterop(deployInterop, "friends");
            sb.CallInterop(deployInterop, "market");
            sb.CallInterop(deployInterop, "vault");

            sb.CallContract("block", "OpenBlock", owner.Address);

            sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(8863626, DomainSettings.StakingTokenDecimals));
            sb.MintTokens(DomainSettings.FuelTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(1000000, DomainSettings.FuelTokenDecimals));
            // requires staking token to be created previously
            sb.CallContract(Nexus.StakeContractName, "Stake", owner.Address, StakeContract.DefaultMasterThreshold);
            sb.CallContract(Nexus.StakeContractName, "Claim", owner.Address, owner.Address);

            sb.Emit(VM.Opcode.RET);
            sb.EmitRaw(Encoding.UTF8.GetBytes("A Phantasma was born..."));

            var script = sb.EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);

            return tx;
        }

        private Transaction ChainCreateTx(PhantasmaKeys owner, string name, params string[] contracts)
        {
            var sb = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallInterop("Nexus.CreateChain", owner.Address, name, RootChain.Name);

                foreach (var contractName in contracts)
                {
                    sb.CallInterop("Runtime.DeployContract", contractName);
                }

                var script = //SpendGas(owner.Address).
                    sb.EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Mine((int)ProofOfWork.Moderate);
            tx.Sign(owner);
            return tx;
        }

        private Transaction ValueCreateTx(PhantasmaKeys owner, string name, BigInteger initial, BigInteger min, BigInteger max)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(Nexus.GovernanceContractName, "CreateValue", name, initial, min, max).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction EndNexusCreateTx(PhantasmaKeys owner)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("validator", "SetValidator", owner.Address, new BigInteger(0), ValidatorType.Primary).
                CallContract(Nexus.SwapContractName, "DepositTokens", owner.Address, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals)).
                CallContract(Nexus.SwapContractName, "DepositTokens", owner.Address, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals)).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        public bool CreateGenesisBlock(string name, PhantasmaKeys owner, Timestamp timestamp)
        {
            if (HasGenesis)
            {
                return false;
            }

            if (!ValidationUtils.IsValidIdentifier(name))
            {
                throw new ChainException("invalid nexus name");
            }

            this.Name = name;

            this.GenesisAddress = owner.Address;

            var storage = RootStorage;
            if (!CreateChain(storage, owner.Address, DomainSettings.RootChainName, null))
            {
                throw new ChainException("failed to create root chain");
            }
            var rootChain = GetChainByName(DomainSettings.RootChainName);

            var tokenScript = new byte[0];
            CreateToken(storage, DomainSettings.StakingTokenSymbol, DomainSettings.StakingTokenName, "neo", Hash.FromUnpaddedHex("ed07cffad18f1308db51920d99a2af60ac66a7b3"), UnitConversion.ToBigInteger(91136374, DomainSettings.StakingTokenDecimals), DomainSettings.StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External, tokenScript);
            CreateToken(storage, DomainSettings.FuelTokenSymbol, DomainSettings.FuelTokenName, DomainSettings.PlatformName, Hash.FromString(DomainSettings.FuelTokenSymbol), DomainSettings.PlatformSupply, DomainSettings.FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel, tokenScript);
            CreateToken(storage, DomainSettings.FiatTokenSymbol, DomainSettings.FiatTokenName, DomainSettings.PlatformName, Hash.FromString(DomainSettings.FiatTokenSymbol), 0, DomainSettings.FiatTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Fiat, tokenScript);

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                BeginNexusCreateTx(owner),

                ValueCreateTx(owner, NexusProtocolVersionTag, 1, 1, 1000),
                ValueCreateTx(owner, ValidatorContract.ValidatorCountTag, 1, 1, 100),
                ValueCreateTx(owner, ValidatorContract.ValidatorRotationTimeTag, 120, 30, 3600),
                ValueCreateTx(owner, ConsensusContract.PollVoteLimitTag, 50000, 100, 500000),
                ValueCreateTx(owner, ConsensusContract.MaxEntriesPerPollTag, 10, 2, 1000),
                ValueCreateTx(owner, ConsensusContract.MaximumPollLengthTag, 86400 * 90, 86400 * 2, 86400 * 120),
                ValueCreateTx(owner, StakeContract.MasterStakeThresholdTag, StakeContract.DefaultMasterThreshold, UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals), UnitConversion.ToBigInteger(200000, DomainSettings.StakingTokenDecimals)),
                ValueCreateTx(owner, StakeContract.VotingStakeThresholdTag, UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals), UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals), UnitConversion.ToBigInteger(10000, DomainSettings.StakingTokenDecimals)),

                //ChainCreateTx(owner, "sale", "sale"),

                EndNexusCreateTx(owner)
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
                    var lastBlockHash = chain.GetLastBlockHash();
                    var lastBlock = chain.GetBlockByHash(lastBlockHash);
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

        public BigInteger GetStakeFromAddress(StorageContext storage, Address address)
        {
            var result = RootChain.InvokeContract(storage, Nexus.StakeContractName, nameof(StakeContract.GetStake), address).AsNumber();
            return result;
        }
        
        public BigInteger GetUnclaimedFuelFromAddress(StorageContext storage, Address address)
        {
            var result = RootChain.InvokeContract(storage, Nexus.StakeContractName, nameof(StakeContract.GetUnclaimed), address).AsNumber();
            return result;
        }

        public bool IsStakeMaster(StorageContext storage, Address address)
        {
            var stake = GetStakeFromAddress(storage, address);
            if (stake <= 0)
            {
                return false;
            }

            var masterThresold = RootChain.InvokeContract(storage, Nexus.StakeContractName, nameof(StakeContract.GetMasterThreshold)).AsNumber();
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
        public Archive GetArchive(Hash hash)
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

        public bool CreateArchive(StorageContext storage, MerkleTree merkleTree, BigInteger size, ArchiveFlags flags, byte[] key)
        {
            var archive = GetArchive(merkleTree.Root);
            if (archive != null)
            {
                return false;
            }

            archive = new Archive(merkleTree, size, flags, key);
            var archiveHash = merkleTree.Root;
            _archiveEntries.Set(archiveHash, archive);

            return true;
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
        internal int CreatePlatform(StorageContext storage, string externalAddress, Address interopAddress, string name, string fuelSymbol)
        {
            // check if already exists something with that name
            if (PlatformExists(name))
            {
                return -1;
            }

            var platformList = this.Platforms.ToList();
            var platformID = (byte)(1 + platformList.Count);

            var chainAddress = Address.FromHash(name);
            var entry = new PlatformInfo(name, fuelSymbol, interopAddress, externalAddress, chainAddress);

            // add to persistent list of tokens
            platformList.Add(name);
            this.Platforms = platformList.ToArray();

            EditPlatform(name, entry);
            return platformID;
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
            if (name == DomainSettings.PlatformName)
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

        public IKeyValueStoreAdapter GetChainStorage(string name)
        {
            return this.CreateKeyStoreAdapter($"chain.{name}");
        }

        public BigInteger GetGovernanceValue(StorageContext storage, string name)
        {
            if (HasGenesis)
            {
                return RootChain.InvokeContract(storage, Nexus.GovernanceContractName, "GetValue", name).AsNumber();
            }

            return 0;
        }

        // TODO optimize this
        public bool IsPlatformAddress(Address address)
        {
            if (!address.IsInterop)
            {
                return false;
            }

            var platforms = this.Platforms;
            foreach (var platform in platforms)
            {
                var info = GetPlatformInfo(platform);
                if (info.InteropAddress == address)
                {
                    return true;
                }
            }

            return false;
        }

        public StorageContext RootStorage => new KeyStoreStorage(GetChainStorage(DomainSettings.RootChainName));
    }
}
