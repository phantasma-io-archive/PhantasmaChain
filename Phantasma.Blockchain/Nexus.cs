using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Core.Performance;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;
using Phantasma.Storage.Context;
using Phantasma.Domain;
using Phantasma.Core.Utils;
using Phantasma.VM;
using Phantasma.Blockchain.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public class Nexus : INexus
    {
        private string ChainNameMapKey => ".chain.name.";
        private string ChainAddressMapKey => ".chain.addr.";
        private string ChainOrgKey => ".chain.org.";
        private string ChainParentNameKey => ".chain.parent.";
        private string ChainChildrenBlockKey => ".chain.children.";

        private string ChainArchivesKey => ".chain.archives.";


        public readonly static string GasContractName = NativeContractKind.Gas.GetContractName();
        public readonly static string BlockContractName = NativeContractKind.Block.GetContractName();
        public readonly static string StakeContractName = NativeContractKind.Stake.GetContractName();
        public readonly static string SwapContractName = NativeContractKind.Swap.GetContractName();
        public readonly static string AccountContractName = NativeContractKind.Account.GetContractName();
        public readonly static string ConsensusContractName = NativeContractKind.Consensus.GetContractName();
        public readonly static string GovernanceContractName = NativeContractKind.Governance.GetContractName();
        public readonly static string StorageContractName = NativeContractKind.Storage.GetContractName();
        public readonly static string ValidatorContractName = NativeContractKind.Validator.GetContractName();
        public readonly static string InteropContractName = NativeContractKind.Interop.GetContractName();
        public readonly static string ExchangeContractName = NativeContractKind.Exchange.GetContractName();
        public readonly static string PrivacyContractName = NativeContractKind.Privacy.GetContractName();
        public readonly static string RelayContractName = NativeContractKind.Relay.GetContractName();
        public readonly static string RankingContractName = NativeContractKind.Ranking.GetContractName();
        public readonly static string MailContractName = NativeContractKind.Mail.GetContractName();

        public const string NexusProtocolVersionTag = "nexus.protocol.version";
        public const string FuelPerContractDeployTag = "nexus.contract.cost";
        public const string FuelPerTokenDeployTag = "nexus.token.cost";

        public readonly string Name;

        private Chain _rootChain = null;
        public Chain RootChain
        {
            get
            {
                if (_rootChain == null)
                {
                    _rootChain = GetChainByName(DomainSettings.RootChainName);
                }
                return _rootChain;
            }
        }

        private KeyValueStore<Hash, byte[]> _archiveContents;

        public bool HasGenesis { get; private set; }

        private readonly List<IChainPlugin> _plugins = new List<IChainPlugin>();

        private readonly Logger _logger;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;
        private OracleReader _oracleReader = null;
        private List<IOracleObserver> _observers = new List<IOracleObserver>();

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(string name, Logger logger = null, Func<string, IKeyValueStoreAdapter> adapterFactory = null)
        {
            this._adapterFactory = adapterFactory;

            var key = GetNexusKey("hash");
            var storage = new KeyStoreStorage(GetChainStorage(DomainSettings.RootChainName));
            RootStorage = storage;
            HasGenesis = storage.Has(key);

            if (!ValidationUtils.IsValidIdentifier(name))
            {
                throw new ChainException("invalid nexus name");
            }

            this.Name = name;

            if (HasGenesis)
            {
                LoadNexus(storage);
            }
            else
            {
                if (!ChainExists(storage, DomainSettings.RootChainName))
                {
                    if (!CreateChain(storage, DomainSettings.ValidatorsOrganizationName, DomainSettings.RootChainName, null))
                    {
                        throw new ChainException("failed to create root chain");
                    }
                }
            }

            _archiveContents = new KeyValueStore<Hash, byte[]>(CreateKeyStoreAdapter("contents"));

            _logger = logger;

            this._oracleReader = null;
        }

        public void SetOracleReader(OracleReader oracleReader)
        {
            this._oracleReader = oracleReader;
        }

        public void Attach(IOracleObserver observer)
        {
            this._observers.Add(observer);
        }

        public void Detach(IOracleObserver observer)
        {
            this._observers.Remove(observer);
        }

        public void Notify(StorageContext storage)
        {
            foreach (var observer in _observers)
            {
                observer.Update(this, storage);
            }
        }

        public void LoadNexus(StorageContext storage)
        {
            var chainList = this.GetChains(storage);
            foreach (var chainName in chainList)
            {
                GetChainByName(chainName);
            }
        }

        private Dictionary<string, IKeyValueStoreAdapter> _keystoreCache = new Dictionary<string, IKeyValueStoreAdapter>();

        public IKeyValueStoreAdapter CreateKeyStoreAdapter(string name)
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
            if (chain == _rootChain && block.Height == 1)
            {
                var storage = RootStorage;
                storage.Put(GetNexusKey("hash"), block.Hash);
                HasGenesis = true;
            }

            foreach (var plugin in _plugins)
            {
                plugin.OnBlock(chain, block);

                var txs = chain.GetBlockTransactions(block);
                foreach (var tx in txs)
                {
                    plugin.OnTransaction(chain, block, tx);
                }
            }
        }

        public Block FindBlockByTransaction(Transaction tx)
        {
            return FindBlockByTransactionHash(tx.Hash);
        }

        public Block FindBlockByTransactionHash(Hash hash)
        {
            var chainNames = this.GetChains(RootStorage);
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

            var contract = this.GetContractByName(storage, name);
            if (contract != null)
            {
                return contract.Address;
            }

            var dao = this.GetOrganizationByName(storage, name);
            if (dao != null)
            {
                return dao.Address;
            }

            var chain = RootChain;
            return chain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.LookUpName), name).AsAddress();
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
        public SmartContract GetContractByName(StorageContext storage, string contractName)
        {
            Throw.IfNullOrEmpty(contractName, nameof(contractName));

            if (ValidationUtils.IsValidTicker(contractName))
            {
                var tokenInfo = GetTokenInfo(storage, contractName);
                return new CustomContract(contractName, tokenInfo.Script, tokenInfo.ABI);
            }

            var address = SmartContract.GetAddressForName(contractName);
            var result = GetNativeContractByAddress(address);

            return result;
        }

        private Dictionary<Address, Type> _contractMap = null;
        private void RegisterContract<T>() where T : SmartContract
        {
            var alloc = (SmartContract)Activator.CreateInstance<T>();
            var addr = alloc.Address;
            _contractMap[addr] = typeof(T);
        }

        // only works for native contracts!!
        public NativeContract GetNativeContractByAddress(Address contractAddress)
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
                RegisterContract<InteropContract>();
                RegisterContract<RankingContract>();
                RegisterContract<FriendsContract>();
                RegisterContract<MailContract>();
                RegisterContract<PrivacyContract>();
                RegisterContract<SaleContract>();
           }

            if (_contractMap.ContainsKey(contractAddress)) {
                var type = _contractMap[contractAddress];
                return (NativeContract)Activator.CreateInstance(type);
            }

            return null;
        }

        #endregion

        #region TRANSACTIONS
        public Transaction FindTransactionByHash(Hash hash)
        {
            var chainNames = this.GetChains(RootStorage);
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
        internal bool CreateChain(StorageContext storage, string organization, string name, string parentChainName)
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

            if (PlatformExists(storage, name))
            {
                return false;
            }

            var chain = new Chain(this, name, _logger);

            // add to persistent list of chains
            var chainList = this.GetSystemList(ChainTag, storage);
            chainList.Add(name);

            // add address and name mapping 
            storage.Put(ChainNameMapKey + chain.Name, chain.Address.ToByteArray());
            storage.Put(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));
            storage.Put(ChainOrgKey + chain.Name, Encoding.UTF8.GetBytes(organization));

            if (!string.IsNullOrEmpty(parentChainName))
            {
                storage.Put(ChainParentNameKey + chain.Name, Encoding.UTF8.GetBytes(parentChainName));
                var childrenList = GetChildrenListOfChain(storage, parentChainName);
                childrenList.Add<string>(chain.Name);
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

        public string GetChainOrganization(string chainName)
        {
            var key = ChainOrgKey + chainName;
            if (RootStorage.Has(key))
            {
                var bytes = RootStorage.Get(key);
                var orgName = Encoding.UTF8.GetString(bytes);
                return orgName;
            }

            return null;
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

        public OracleReader GetOracleReader()
        {
            Throw.If(_oracleReader == null, "Oracle reader has not been set yet.");
            return _oracleReader;
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
            if (FeedExists(storage, name))
            {
                return false;
            }

            var feedInfo = new OracleFeed(name, owner, mode);
            EditFeed(storage, name, feedInfo);

            // add to persistent list of feeds
            var feedList = this.GetSystemList(FeedTag, storage);
            feedList.Add(name);

            return true;
        }

        private string GetFeedInfoKey(string name)
        {
            return ".feed:" + name.ToUpper();
        }

        private void EditFeed(StorageContext storage, string name, OracleFeed feed)
        {
            var key = GetFeedInfoKey(name);
            var bytes = Serialization.Serialize(feed);
            storage.Put(key, bytes);
        }

        public bool FeedExists(StorageContext storage, string name)
        {
            var key = GetFeedInfoKey(name);
            return storage.Has(key);
        }

        public OracleFeed GetFeedInfo(StorageContext storage, string name)
        {
            var key = GetFeedInfoKey(name);
            if (storage.Has(key))
            {
                var bytes = storage.Get(key);
                return Serialization.Unserialize<OracleFeed>(bytes);
            }

            throw new ChainException($"Oracle feed does not exist ({name})");
        }
        #endregion

        #region TOKENS

        internal IToken CreateToken(StorageContext storage, string symbol, string name, Address owner, BigInteger maxSupply, int decimals, TokenFlags flags, byte[] script, ContractInterface abi = null)
        {
            Throw.IfNull(script, nameof(script));
            Throw.IfNull(abi, nameof(abi));

            var tokenInfo = new TokenInfo(symbol, name, owner, maxSupply, decimals, flags, script, abi);
            EditToken(storage, symbol, tokenInfo);

            if (symbol == "TTRS")  // support for 22series tokens with a dummy script that conforms to the standard
            {
                byte[] nftScript;
                ContractInterface nftABI;

                var url = "https://www.22series.com/part_info?id=*";
                Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript, out nftABI);

                CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
            }
            else
            if (symbol == DomainSettings.RewardTokenSymbol)  
            {
                byte[] nftScript;
                ContractInterface nftABI;

                var url = "https://phantasma.io/crown?id=*";
                Tokens.TokenUtils.GenerateNFTDummyScript(symbol, $"{symbol} #*", $"{symbol} #*", url, url, out nftScript, out nftABI);

                CreateSeries(storage, tokenInfo, 0, maxSupply, TokenSeriesMode.Unique, nftScript, nftABI);
            }

            // add to persistent list of tokens
            var tokenList = this.GetSystemList(TokenTag, storage);
            tokenList.Add(symbol);

            return tokenInfo;
        }

        private string GetTokenInfoKey(string symbol)
        {
            return ".token:" + symbol;
        }

        private void EditToken(StorageContext storage, string symbol, TokenInfo tokenInfo)
        {
            var key = GetTokenInfoKey(symbol);
            var bytes = Serialization.Serialize(tokenInfo);
            storage.Put(key, bytes);
        }

        public bool TokenExists(StorageContext storage, string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            return storage.Has(key);
        }

        public IToken GetTokenInfo(StorageContext storage, string symbol)
        {
            var key = GetTokenInfoKey(symbol);
            if (storage.Has(key))
            {
                var bytes = storage.Get(key);
                var token = Serialization.Unserialize<TokenInfo>(bytes);

                TokenUtils.FetchProperty(storage, RootChain, "getOwner", token, (prop, value) =>
                {
                    token.Owner = value.AsAddress();
                });

                return token;
            }

            throw new ChainException($"Token does not exist ({symbol})");
        }

        internal void MintTokens(RuntimeVM Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger amount)
        {
            Runtime.Expect(token.IsFungible(), "must be fungible");
            Runtime.Expect(amount > 0, "invalid amount");

            var isSettlement = sourceChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
            Runtime.Expect(supply.Mint(Runtime.Storage, amount, token.MaxSupply), "mint supply failed");

            var balances = new BalanceSheet(token);
            Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), "balance add failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, amount) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed");

            var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, accountTrigger, source, destination, token.Symbol, amount) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed");

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, sourceChain));
                Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenMint, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
        }

        // NFT version
        internal void MintToken(RuntimeVM Runtime, IToken token, Address source, Address destination, string sourceChain, BigInteger tokenID)
        {
            Runtime.Expect(!token.IsFungible(), "cant be fungible");

            var isSettlement = sourceChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);
            Runtime.Expect(supply.Mint(Runtime.Storage, 1, token.MaxSupply), "supply mint failed");

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnReceive : TokenTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed");

            var accountTrigger = isSettlement ? AccountTrigger.OnReceive : AccountTrigger.OnMint;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, accountTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed");

            var nft = ReadNFT(Runtime, token.Symbol, tokenID);
            using (var m = new ProfileMarker("Nexus.WriteNFT"))
                WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, destination, nft.ROM, nft.RAM, nft.SeriesID, nft.Timestamp, nft.Infusion, !isSettlement);

            using (var m = new ProfileMarker("Runtime.Notify"))
            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, sourceChain));
                Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenMint, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
        }

        private string GetBurnKey(string symbol)
        {
            return $".burned.{symbol}";
        }

        private void Internal_UpdateBurnedSupply(StorageContext storage, string burnKey, BigInteger burnAmount)
        {
            var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
            burnedSupply += burnAmount;
            storage.Put<BigInteger>(burnKey, burnedSupply);
        }

        private void UpdateBurnedSupply(StorageContext storage, string symbol, BigInteger burnAmount)
        {
            var burnKey = GetBurnKey(symbol);
            Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
        }

        private void UpdateBurnedSupplyForSeries(StorageContext storage, string symbol, BigInteger burnAmount, BigInteger seriesID)
        {
            var burnKey = GetBurnKey($"{symbol}.{seriesID}");
            Internal_UpdateBurnedSupply(storage, burnKey, burnAmount);
        }

        public BigInteger GetBurnedTokenSupply(StorageContext storage, string symbol)
        {
            var burnKey = GetBurnKey(symbol);
            var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
            return burnedSupply;
        }

        public BigInteger GetBurnedTokenSupplyForSeries(StorageContext storage, string symbol, BigInteger seriesID)
        {
            var burnKey = GetBurnKey($"{symbol}.{seriesID}");
            var burnedSupply = storage.Has(burnKey) ? storage.Get<BigInteger>(burnKey) : 0;
            return burnedSupply;
        }

        internal void BurnTokens(RuntimeVM Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger amount)
        {
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

            Runtime.Expect(amount > 0, "invalid amount");

            var allowed = Runtime.IsWitness(source);

            if (!allowed)
            {
                allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
            }

            Runtime.Expect(allowed, "invalid witness or allowance");

            var isSettlement = targetChain != Runtime.Chain.Name;

            var supply = new SupplySheet(token.Symbol, Runtime.Chain, this);

            Runtime.Expect(supply.Burn(Runtime.Storage, amount), $"{token.Symbol} burn failed");

            var balances = new BalanceSheet(token);
            Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount), $"{token.Symbol} balance subtract failed from {source.Text}");

            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account trigger failed");

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, amount, targetChain));
            }
            else
            {
                UpdateBurnedSupply(Runtime.Storage, token.Symbol, amount);
                Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
        }

        // NFT version
        internal void BurnToken(RuntimeVM Runtime, IToken token, Address source, Address destination, string targetChain, BigInteger tokenID)
        {
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), $"{token.Symbol} can't be fungible");

            var isSettlement = targetChain != Runtime.Chain.Name;

            var nft = Runtime.ReadToken(token.Symbol, tokenID);
            Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

            Runtime.Expect(nft.CurrentOwner == source, $"{source} is not the owner of {token.Symbol} #{tokenID}");

            Runtime.Expect(source != DomainSettings.InfusionAddress, $"{token.Symbol} #{tokenID} is currently infused");

            var chain = RootChain;
            var supply = new SupplySheet(token.Symbol, chain, this);

            Runtime.Expect(supply.Burn(Runtime.Storage, 1), "supply burning failed");

            if (!isSettlement)
            {
                Runtime.Expect(source == destination, "source and destination must match when burning");
                Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                DestroyNFT(Runtime, token.Symbol, tokenID, source);
            }

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership removal failed");

            var tokenTrigger = isSettlement ? TokenTrigger.OnSend : TokenTrigger.OnBurn;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");

            var accountTrigger = isSettlement ? AccountTrigger.OnSend : AccountTrigger.OnBurn;
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, accountTrigger, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, $"accont {accountTrigger} trigger failed: ");

            if (isSettlement)
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenStake, destination, new TokenEventData(token.Symbol, tokenID, targetChain));
                Runtime.Notify(EventKind.PackedNFT, destination, new PackedNFTData(token.Symbol, nft.ROM, nft.RAM));
            }
            else
            {
                UpdateBurnedSupply(Runtime.Storage, token.Symbol, 1);
                UpdateBurnedSupplyForSeries(Runtime.Storage, token.Symbol, 1, nft.SeriesID);
                Runtime.Notify(EventKind.TokenBurn, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
        }

        internal void InfuseToken(RuntimeVM Runtime, IToken token, Address from, BigInteger tokenID, IToken infuseToken, BigInteger value)
        {
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "can't be fungible");

            var nft = Runtime.ReadToken(token.Symbol, tokenID);
            Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");
            Runtime.Expect(nft.CurrentOwner == from, "nft does not belong to " + from);
            Runtime.Expect(nft.CurrentChain == Runtime.Chain.Name, "not on this chain");

            if (token.Symbol == infuseToken.Symbol)
            {
                Runtime.Expect(value != tokenID, "cannot infuse token into itself");
            }

            var target = DomainSettings.InfusionAddress;

            var tokenTrigger = TokenTrigger.OnInfuse;
            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, tokenTrigger, from, target, infuseToken.Symbol, value) != TriggerResult.Failure, $"token {tokenTrigger} trigger failed: ");

            if (infuseToken.IsFungible())
            {
                this.TransferTokens(Runtime, infuseToken, from, target, value, true);
            }
            else
            {
                this.TransferToken(Runtime, infuseToken, from, target, value, true);
            }

            int index = -1;

            if (infuseToken.IsFungible())
            {
                for (int i = 0; i < nft.Infusion.Length; i++)
                {
                    if (nft.Infusion[i].Symbol == infuseToken.Symbol)
                    {
                        index = i;
                        break;
                    }
                }
            }

            var infusion = nft.Infusion.ToList();

            if (index < 0)
            {
                infusion.Add(new TokenInfusion(infuseToken.Symbol, value));
            }
            else
            {
                var temp = nft.Infusion[index];
                infusion[index] = new TokenInfusion(infuseToken.Symbol, value + temp.Value);
            }

            WriteNFT(Runtime, token.Symbol, tokenID, nft.CurrentChain, nft.CurrentOwner, nft.ROM, nft.RAM, nft.SeriesID, nft.Timestamp, infusion, true);

            Runtime.Notify(EventKind.Infusion, nft.CurrentOwner, new InfusionEventData(token.Symbol, tokenID, infuseToken.Symbol, value, nft.CurrentChain));
        }

        internal void TransferTokens(RuntimeVM Runtime, IToken token, Address source, Address destination, BigInteger amount, bool isInfusion = false)
        {
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "must be fungible");

            Runtime.Expect(amount > 0, "invalid amount");
            Runtime.Expect(source != destination, "source and destination must be different");
            Runtime.Expect(!destination.IsNull, "invalid destination");

            if (destination.IsSystem)
            {
                var destName = Runtime.Chain.GetNameFromAddress(Runtime.Storage, destination);
                Runtime.Expect(destName != ValidationUtils.ANONYMOUS_NAME, "anonymous system address as destination");
            }

            var allowed = Runtime.IsWitness(source);

            if (!allowed)
            {
                allowed = Runtime.SubtractAllowance(source, token.Symbol, amount);
            }

            Runtime.Expect(allowed, "invalid witness or allowance");

            var balances = new BalanceSheet(token);
            Runtime.Expect(balances.Subtract(Runtime.Storage, source, amount), $"{token.Symbol} balance subtract failed from {source.Text}");
            Runtime.Expect(balances.Add(Runtime.Storage, destination, amount), $"{token.Symbol} balance add failed to {destination.Text}");

            Runtime.AddAllowance(destination, token.Symbol, amount);

            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token onSend trigger failed");
            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol, amount) != TriggerResult.Failure, "token onReceive trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, AccountTrigger.OnSend, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account onSend trigger failed");
            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, AccountTrigger.OnReceive, source, destination, token.Symbol, amount) != TriggerResult.Failure, "account onReceive trigger failed");

            Runtime.RemoveAllowance(destination, token.Symbol);

            if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
            {
                Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
            {
                Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, amount, Runtime.Chain.Name));
            }
        }

        internal void TransferToken(RuntimeVM Runtime, IToken token, Address source, Address destination, BigInteger tokenID, bool isInfusion = false)
        {
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Transferable), "Not transferable");
            Runtime.Expect(!token.Flags.HasFlag(TokenFlags.Fungible), "Should be non-fungible");

            Runtime.Expect(tokenID > 0, "invalid nft id");

            Runtime.Expect(source != destination, "source and destination must be different");

            Runtime.Expect(!destination.IsNull, "destination cant be null");

            var nft = ReadNFT(Runtime, token.Symbol, tokenID);
            Runtime.Expect(nft.CurrentOwner != Address.Null, "nft already destroyed");

            var ownerships = new OwnershipSheet(token.Symbol);
            Runtime.Expect(ownerships.Remove(Runtime.Storage, source, tokenID), "ownership remove failed");

            Runtime.Expect(ownerships.Add(Runtime.Storage, destination, tokenID), "ownership add failed");

            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnSend, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "token send trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnToken(true, token, TokenTrigger.OnReceive, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "token receive trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, source, AccountTrigger.OnSend, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "account send trigger failed");

            Runtime.Expect(Runtime.InvokeTriggerOnAccount(true, destination, AccountTrigger.OnReceive, source, destination, token.Symbol, tokenID) != TriggerResult.Failure, "account received trigger failed");

            WriteNFT(Runtime, token.Symbol, tokenID, Runtime.Chain.Name, destination, nft.ROM, nft.RAM, nft.SeriesID, Runtime.Time, nft.Infusion, true);

            if (destination.IsSystem && (destination == Runtime.CurrentContext.Address || isInfusion))
            {
                Runtime.Notify(EventKind.TokenStake, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            if (source.IsSystem && (source == Runtime.CurrentContext.Address || isInfusion))
            {
                Runtime.Notify(EventKind.TokenClaim, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
            else
            {
                Runtime.Notify(EventKind.TokenSend, source, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
                Runtime.Notify(EventKind.TokenReceive, destination, new TokenEventData(token.Symbol, tokenID, Runtime.Chain.Name));
            }
        }

        #endregion

        #region NFT

        public byte[] GetKeyForNFT(string symbol, BigInteger tokenID)
        {
            return GetKeyForNFT(symbol, tokenID.ToString());
        }

        public byte[] GetKeyForNFT(string symbol, string key)
        {
            var tokenKey = SmartContract.GetKeyForField(symbol, key, false);
            return tokenKey;
        }

        private StorageList GetSeriesList(StorageContext storage, string symbol)
        {
            var key = System.Text.Encoding.ASCII.GetBytes("series." + symbol);
            return new StorageList(key, storage);
        }

        public BigInteger[] GetAllSeriesForToken(StorageContext storage, string symbol) 
        {
            var list = GetSeriesList(storage, symbol);
            return list.All<BigInteger>();
        }

        internal TokenSeries CreateSeries(StorageContext storage, IToken token, BigInteger seriesID, BigInteger maxSupply, TokenSeriesMode mode, byte[] script, ContractInterface abi)
        {
            if (token.IsFungible())
            {
                throw new ChainException($"Can't create series for fungible token");
            }

            var key = GetTokenSeriesKey(token.Symbol, seriesID);

            if (storage.Has(key))
            {
                throw new ChainException($"Series {seriesID} of token {token.Symbol} already exist");
            }

            if (token.IsCapped() && maxSupply < 1)
            {
                throw new ChainException($"Token series supply must be 1 or more");
            }

            var nftStandard = Tokens.TokenUtils.GetNFTStandard();

            if (!abi.Implements(nftStandard))
            {
                throw new ChainException($"Token series abi does not implement the NFT standard");
            }

            var series = new TokenSeries(0, maxSupply, mode, script, abi, null);
            WriteTokenSeries(storage, token.Symbol, seriesID, series);

            var list = GetSeriesList(storage, token.Symbol);
            list.Add(seriesID);

            return series;
        }

        private byte[] GetTokenSeriesKey(string symbol, BigInteger seriesID)
        {
            return GetKeyForNFT(symbol, $"serie{seriesID}");
        }

        public TokenSeries GetTokenSeries(StorageContext storage, string symbol, BigInteger seriesID)
        {
            var key = GetTokenSeriesKey(symbol, seriesID);

            if (storage.Has(key))
            {
                return storage.Get<TokenSeries>(key);
            }

            return null;
        }

        private void WriteTokenSeries(StorageContext storage, string symbol, BigInteger seriesID, TokenSeries series)
        {
            var key = GetTokenSeriesKey(symbol, seriesID);
            storage.Put<TokenSeries>(key, series);
        }

        internal BigInteger GenerateNFT(RuntimeVM Runtime, string symbol, string chainName, Address targetAddress, byte[] rom, byte[] ram, BigInteger seriesID)
        {
            Runtime.Expect(ram != null, "invalid nft ram");

            Runtime.Expect(seriesID >= 0, "invalid series ID");

            var series = GetTokenSeries(Runtime.RootStorage, symbol, seriesID);
            Runtime.Expect(series != null, $"{symbol} series {seriesID} does not exist");

            BigInteger mintID = series.GenerateMintID();
            Runtime.Expect(mintID > 0, "invalid mintID generated");

            if (series.Mode == TokenSeriesMode.Duplicated)
            {
                if (mintID > 1)
                {
                    if (rom == null || rom.Length == 0)
                    {
                        rom = series.ROM;
                    }
                    else
                    {
                        Runtime.Expect(ByteArrayUtils.CompareBytes(rom, series.ROM), $"rom can't be unique in {symbol} series {seriesID}");
                    }
                }
                else
                {
                    series.SetROM(rom);
                }

                rom = new byte[0];
            }
            else
            {
                Runtime.Expect(rom != null && rom.Length > 0, "invalid nft rom");
            }

            WriteTokenSeries(Runtime.RootStorage, symbol, seriesID, series);

            var token = Runtime.GetToken(symbol);

            if (series.MaxSupply > 0)
            {
                Runtime.Expect(mintID <= series.MaxSupply, $"{symbol} series {seriesID} reached max supply already");
            }
            else
            {
                Runtime.Expect(!token.IsCapped(), $"{symbol} series {seriesID} max supply is not defined yet");
            }

            var content = new TokenContent(seriesID, mintID, chainName, targetAddress, targetAddress, rom, ram, Runtime.Time, null, series.Mode);

            var tokenKey = GetKeyForNFT(symbol, content.TokenID);
            Runtime.Expect(!Runtime.Storage.Has(tokenKey), "duplicated nft");

            var contractAddress = token.GetContractAddress();

            var bytes = content.ToByteArray();
            bytes = CompressionUtils.Compress(bytes);

            Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);

            return content.TokenID;
        }

        internal void DestroyNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID, Address target)
        {
            var infusionAddress = DomainSettings.InfusionAddress;

            var tokenContent = ReadNFT(Runtime, symbol, tokenID);

            foreach (var asset in tokenContent.Infusion)
            {
                var assetInfo = this.GetTokenInfo(Runtime.RootStorage, asset.Symbol);

                Runtime.AddAllowance(infusionAddress, asset.Symbol, asset.Value);
                
                if (assetInfo.IsFungible())
                {
                    this.TransferTokens(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }
                else
                {
                    this.TransferToken(Runtime, assetInfo, infusionAddress, target, asset.Value, true);
                }

                Runtime.RemoveAllowance(infusionAddress, asset.Symbol);
            }

            var token = Runtime.GetToken(symbol);
            var contractAddress = token.GetContractAddress();

            var tokenKey = GetKeyForNFT(symbol, tokenID);

            Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.DeleteData), contractAddress, tokenKey);
        }

        internal void WriteNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID, string chainName, Address owner, byte[] rom, byte[] ram, BigInteger seriesID, Timestamp timestamp, IEnumerable<TokenInfusion> infusion, bool mustExist)
        {
            Runtime.Expect(ram != null && ram.Length < TokenContent.MaxRAMSize, "invalid nft ram update");

            var tokenKey = GetKeyForNFT(symbol, tokenID);

            if (Runtime.RootStorage.Has(tokenKey))
            {
                var content = ReadNFTRaw(Runtime.RootStorage, tokenKey, Runtime.ProtocolVersion);

                var series = GetTokenSeries(Runtime.RootStorage, symbol, content.SeriesID);
                Runtime.Expect(series != null, $"could not find series {seriesID} for {symbol}");

                switch (series.Mode)
                {
                    case TokenSeriesMode.Unique:
                        Runtime.Expect(rom.CompareBytes(content.ROM), "rom does not match original value");
                        break;

                    case TokenSeriesMode.Duplicated:
                        Runtime.Expect(rom.Length == 0 || rom.CompareBytes(series.ROM), "rom does not match original value");
                        break;

                    default:
                        throw new ChainException("WriteNFT: unsupported series mode: " + series.Mode);
                }

                content = new TokenContent(content.SeriesID, content.MintID, chainName, content.Creator, owner, content.ROM, ram, timestamp, infusion, series.Mode);

                var token = Runtime.GetToken(symbol);
                var contractAddress = token.GetContractAddress();

                var bytes = content.ToByteArray();
                bytes = CompressionUtils.Compress(bytes);

                Runtime.CallNativeContext(NativeContractKind.Storage, nameof(StorageContract.WriteData), contractAddress, tokenKey, bytes);
            }
            else
            {
                Runtime.Expect(!mustExist, $"nft {symbol} {tokenID} does not exist");
                var genID = GenerateNFT(Runtime, symbol, chainName, owner, rom, ram, seriesID);
                Runtime.Expect(genID == tokenID, "failed to regenerate NFT");
            }
        }

        public TokenContent ReadNFT(RuntimeVM Runtime, string symbol, BigInteger tokenID)
        {
            return ReadNFT(Runtime.RootStorage, symbol, tokenID, Runtime.ProtocolVersion);
        }

        public TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID)
        {
            var protocol = this.GetProtocolVersion(storage);
            return ReadNFT(storage, symbol, tokenID, protocol);
        }

        private TokenContent ReadNFTRaw(StorageContext storage, byte[] tokenKey, uint ProtocolVersion)
        {
            var bytes = storage.Get(tokenKey);

            bytes = CompressionUtils.Decompress(bytes);

            var content = Serialization.Unserialize<TokenContent>(bytes);
            return content;
        }

        private TokenContent ReadNFT(StorageContext storage, string symbol, BigInteger tokenID, uint ProtocolVersion)
        {
            var tokenKey = GetKeyForNFT(symbol, tokenID);

            Throw.If(!storage.Has(tokenKey), $"nft {symbol} {tokenID} does not exist");

            var content = ReadNFTRaw(storage, tokenKey, ProtocolVersion);

            var series = GetTokenSeries(storage, symbol, content.SeriesID);

            content.UpdateTokenID(series.Mode);

            if (series.Mode == TokenSeriesMode.Duplicated)
            {
                content.ReplaceROM(series.ROM);
            }


            return content;
        }

        public bool HasNFT(StorageContext storage, string symbol, BigInteger tokenID)
        {
            var tokenKey = GetKeyForNFT(symbol, tokenID);
            return storage.Has(tokenKey);
        }
        #endregion

        #region GENESIS
        private Transaction BeginNexusCreateTx(PhantasmaKeys owner)
        {
            var sb = ScriptUtils.BeginScript();
            sb.CallInterop("Nexus.BeginInit", owner.Address);

            var deployInterop = "Runtime.DeployContract";
            sb.CallInterop(deployInterop, owner.Address, ValidatorContractName);
            sb.CallInterop(deployInterop, owner.Address, GovernanceContractName);
            sb.CallInterop(deployInterop, owner.Address, ConsensusContractName);
            sb.CallInterop(deployInterop, owner.Address, AccountContractName);
            sb.CallInterop(deployInterop, owner.Address, ExchangeContractName);
            sb.CallInterop(deployInterop, owner.Address, SwapContractName);
            sb.CallInterop(deployInterop, owner.Address, InteropContractName);
            sb.CallInterop(deployInterop, owner.Address, StakeContractName);
            sb.CallInterop(deployInterop, owner.Address, StorageContractName);
            sb.CallInterop(deployInterop, owner.Address, RelayContractName);
            sb.CallInterop(deployInterop, owner.Address, RankingContractName);
            sb.CallInterop(deployInterop, owner.Address, PrivacyContractName);
            sb.CallInterop(deployInterop, owner.Address, MailContractName);
            sb.CallInterop(deployInterop, owner.Address, "friends");
            sb.CallInterop(deployInterop, owner.Address, "market");
            sb.CallInterop(deployInterop, owner.Address, "sale");

            var orgInterop = "Nexus.CreateOrganization";
            var orgScript = new byte[0];
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.ValidatorsOrganizationName, "Block Producers", orgScript);
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.MastersOrganizationName, "Soul Masters", orgScript);
            sb.CallInterop(orgInterop, owner.Address, DomainSettings.StakersOrganizationName, "Soul Stakers", orgScript);

            sb.MintTokens(DomainSettings.StakingTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(2863626, DomainSettings.StakingTokenDecimals));
            sb.MintTokens(DomainSettings.FuelTokenSymbol, owner.Address, owner.Address, UnitConversion.ToBigInteger(1000000, DomainSettings.FuelTokenDecimals));
            // requires staking token to be created previously
            sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Stake), owner.Address, StakeContract.DefaultMasterThreshold);
            sb.CallContract(NativeContractKind.Stake, nameof(StakeContract.Claim), owner.Address, owner.Address);

            sb.CallInterop("Nexus.EndInit", owner.Address);

            sb.Emit(VM.Opcode.RET);

            var script = sb.EndScript();

            var tx = new Transaction(this.Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Mine(ProofOfWork.Minimal);
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
                sb.CallInterop("Runtime.DeployContract", owner.Address, contractName);
            }

            var script = //SpendGas(owner.Address).
                sb.EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Mine((int)ProofOfWork.Moderate);
            tx.Sign(owner);
            return tx;
        }

        private Transaction ValueCreateTx(PhantasmaKeys owner, Dictionary<string, KeyValuePair<BigInteger, ChainConstraint[]>> values)
        {
            var sb = ScriptUtils.
                BeginScript();
            //AllowGas(owner.Address, Address.Null, 1, 9999).
            foreach (var entry in values)
            {
                var name = entry.Key;
                var initial = entry.Value.Key;
                var constraints = entry.Value.Value;
                var bytes = Serialization.Serialize(constraints);
                sb.CallContract(NativeContractKind.Governance, nameof(GovernanceContract.CreateValue), name, initial, bytes);
            }
            //SpendGas(owner.Address).
            var script = sb.EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction EndNexusCreateTx(PhantasmaKeys owner)
        {
            var script = ScriptUtils.
                BeginScript().
                //AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(NativeContractKind.Validator, nameof(ValidatorContract.SetValidator), owner.Address, new BigInteger(0), ValidatorType.Primary).
                CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.StakingTokenSymbol, UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals)).
                CallContract(NativeContractKind.Swap, nameof(SwapContract.DepositTokens), owner.Address, DomainSettings.FuelTokenSymbol, UnitConversion.ToBigInteger(100, DomainSettings.FuelTokenDecimals)).
                //SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, DomainSettings.RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        internal void BeginInitialize(RuntimeVM vm, Address owner)
        {
            var storage = RootStorage;

            storage.Put(GetNexusKey("owner"), owner);

            var tokenScript = new byte[] { (byte)Opcode.RET };
            var abi = ContractInterface.Empty;

            //UnitConversion.ToBigInteger(91136374, DomainSettings.StakingTokenDecimals)
            CreateToken(storage, DomainSettings.StakingTokenSymbol, DomainSettings.StakingTokenName, owner, 0, DomainSettings.StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stakable, tokenScript, abi);
            CreateToken(storage, DomainSettings.FuelTokenSymbol, DomainSettings.FuelTokenName, owner, 0, DomainSettings.FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Burnable | TokenFlags.Fuel, tokenScript, abi);
            CreateToken(storage, DomainSettings.FiatTokenSymbol, DomainSettings.FiatTokenName, owner, 0, DomainSettings.FiatTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Fiat, tokenScript, abi);
            CreateToken(storage, DomainSettings.RewardTokenSymbol, DomainSettings.RewardTokenName, owner, 0, 0, TokenFlags.Transferable | TokenFlags.Burnable, tokenScript, abi);

            CreateToken(storage, "NEO", "NEO", owner, UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite, tokenScript, abi);
            CreateToken(storage, "GAS", "GAS", owner, UnitConversion.ToBigInteger(100000000, 8), 8, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite, tokenScript, abi);
            CreateToken(storage, "ETH", "Ethereum", owner, UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible, tokenScript, abi);
            //CreateToken(storage, "DAI", "Dai Stablecoin", owner, UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Foreign, tokenScript, abi);
            //GenerateToken(_owner, "EOS", "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External, tokenScript, abi);

            SetPlatformTokenHash(DomainSettings.StakingTokenSymbol, "neo", Hash.FromUnpaddedHex("ed07cffad18f1308db51920d99a2af60ac66a7b3"), storage);
            SetPlatformTokenHash("NEO", "neo", Hash.FromUnpaddedHex("c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b"), storage);
            SetPlatformTokenHash("GAS", "neo", Hash.FromUnpaddedHex("602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7"), storage);
            SetPlatformTokenHash("ETH", "ethereum", Hash.FromString("ETH"), storage);
            //SetPlatformTokenHash("DAI", "ethereum", Hash.FromUnpaddedHex("6b175474e89094c44da98b954eedeac495271d0f"), storage);
        }

        internal void FinishInitialize(RuntimeVM vm, Address owner)
        {
            var storage = RootStorage;

            var symbols = GetTokens(storage);
            foreach (var symbol in symbols)
            {
                var token = GetTokenInfo(storage, symbol);

                var constructor = token.ABI.FindMethod(SmartContract.ConstructorName);

                if (constructor != null)
                {
                    vm.CallContext(symbol, constructor, owner);
                }
            }
        }

        public bool CreateGenesisBlock(PhantasmaKeys owner, Timestamp timestamp, int version)
        {
            if (HasGenesis)
            {
                return false;
            }

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                BeginNexusCreateTx(owner),

                ValueCreateTx(owner,
                 new Dictionary<string, KeyValuePair<BigInteger, ChainConstraint[]>>() {
                     {
                         NexusProtocolVersionTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             version, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MustIncrease}
                         })
                     },

                     {
                         ValidatorContract.ValidatorCountTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             1, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MustIncrease}
                         })
                     },

                     {
                         ValidatorContract.ValidatorRotationTimeTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             120, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 30},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 3600},
                         })
                     },

                     {
                         ConsensusContract.PollVoteLimitTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             50000, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 100},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 500000},
                         })
                     },

                     {
                         ConsensusContract.MaxEntriesPerPollTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             10, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 2},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 1000},
                         })
                     },

                     {
                         ConsensusContract.MaximumPollLengthTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             86400 * 90, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 86400 * 2},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 86400 * 120},
                         })
                     },

                     {
                         StakeContract.MasterStakeThresholdTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             StakeContract.DefaultMasterThreshold, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals)},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(200000, DomainSettings.StakingTokenDecimals)},
                         })
                     },
                     
                     {
                         StakeContract.StakeSingleBonusPercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             5, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 100 },
                         })
                     },

                     {
                         StakeContract.StakeMaxBonusPercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             100, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 50},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 500 },
                         })
                     },

                     {
                         StakeContract.VotingStakeThresholdTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             UnitConversion.ToBigInteger(1000, DomainSettings.StakingTokenDecimals), new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals)},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(10000, DomainSettings.StakingTokenDecimals)},
                         })
                     },

                     {
                         SwapContract.SwapMakerFeePercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             2, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 20},
                             new ChainConstraint() { Kind = ConstraintKind.LessThanOther, Tag = SwapContract.SwapTakerFeePercentTag},
                         })
                     },


                     {
                         SwapContract.SwapTakerFeePercentTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             5, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 1},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 20},
                             new ChainConstraint() { Kind = ConstraintKind.GreatThanOther, Tag = SwapContract.SwapMakerFeePercentTag},
                         })
                     },

                     {
                         StorageContract.KilobytesPerStakeTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             40, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 1},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 10000},
                         })
                     },
                     
                     {
                         StorageContract.FreeStoragePerContractTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             1024, new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = 1024 * 512},
                         })
                     },

                     {
                         Nexus.FuelPerContractDeployTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             UnitConversion.ToBigInteger(10, DomainSettings.FiatTokenDecimals), new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.FiatTokenDecimals)},
                         })
                     },

                     {
                         Nexus.FuelPerTokenDeployTag, new KeyValuePair<BigInteger, ChainConstraint[]>(
                             UnitConversion.ToBigInteger(100, DomainSettings.FiatTokenDecimals), new ChainConstraint[]
                         {
                             new ChainConstraint() { Kind = ConstraintKind.MinValue, Value = 0},
                             new ChainConstraint() { Kind = ConstraintKind.MaxValue, Value = UnitConversion.ToBigInteger(1000, DomainSettings.FiatTokenDecimals)},
                         })
                     },
                 }),
                
                //ChainCreateTx(owner, "sale", "sale"),

                EndNexusCreateTx(owner)
            };

            var rootChain = GetChainByName(DomainSettings.RootChainName);

            var payload = Encoding.UTF8.GetBytes("A Phantasma was born...");
            var block = new Block(Chain.InitialHeight, rootChain.Address, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, 0, owner.Address, payload);

	        Transaction inflationTx = null;
            var changeSet = rootChain.ProcessBlock(block, transactions, 1, out inflationTx, owner);
	        if (inflationTx != null)
 	        {
		        transactions.Add(inflationTx);
	        }

            block.Sign(owner);
            rootChain.AddBlock(block, transactions, 1, changeSet);

            var storage = RootStorage;
            storage.Put(GetNexusKey("hash"), block.Hash);

            this.HasGenesis = true;
            return true;
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

        public Timestamp GetStakeTimestampOfAddress(StorageContext storage, Address address)
        {
            var result = RootChain.InvokeContract(storage, Nexus.StakeContractName, nameof(StakeContract.GetStakeTimestamp), address).AsTimestamp();
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

        private StorageMap GetArchiveMap(StorageContext storage)
        {
            var map = new StorageMap(ChainArchivesKey, storage);
            return map;
        }

        public Archive GetArchive(StorageContext storage, Hash hash)
        {
            var map = GetArchiveMap(storage);

            if (map.ContainsKey(hash))
            {
                var bytes = map.Get<Hash, byte[]>(hash);
                var archive = Archive.Unserialize(bytes);
                return archive;
            }

            return null;
        }

        public bool ArchiveExists(StorageContext storage, Hash hash)
        {
            var map = GetArchiveMap(storage);
            return map.ContainsKey(hash);
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

        public IArchive CreateArchive(StorageContext storage, MerkleTree merkleTree, Address owner, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption)
        {
            var archive = GetArchive(storage, merkleTree.Root);
            Throw.If(archive != null, "archive already exists");

            archive = new Archive(merkleTree, name, size, time, encryption,
                Enumerable.Range(0, (int)MerkleTree.GetChunkCountForSize(size)).ToList());
            var archiveHash = merkleTree.Root;

            AddOwnerToArchive(storage, archive, owner);

            // ModifyArchive(storage, archive); => not necessary, addOwner already calls this

            return archive;
        }

        private void ModifyArchive(StorageContext storage, Archive archive)
        {
            var map = GetArchiveMap(storage);
            var bytes = archive.ToByteArray();
            map.Set<Hash, byte[]>(archive.Hash, bytes);
        }

        public bool DeleteArchive(StorageContext storage, Archive archive)
        {
            Throw.IfNull(archive, nameof(archive));

            Throw.If(archive.OwnerCount > 0, "can't delete archive, still has owners");

            for (int i = 0; i < archive.BlockCount; i++)
            {
                var blockHash = archive.MerkleTree.GetHash(i);
                if (_archiveContents.ContainsKey(blockHash))
                {
                    _archiveContents.Remove(blockHash);
                }
            }

            var map = GetArchiveMap(storage);
            map.Remove(archive.Hash);

            return true;
        }

        public bool HasArchiveBlock(Archive archive, int blockIndex)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = archive.MerkleTree.GetHash(blockIndex);
            return _archiveContents.ContainsKey(hash);
        }

        public void WriteArchiveBlock(Archive archive, int blockIndex, byte[] content)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.IfNull(content, nameof(content));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = MerkleTree.CalculateBlockHash(content);

            if (_archiveContents.ContainsKey(hash))
            {
                return;
            }

            if (!archive.MerkleTree.VerifyContent(hash, blockIndex))
            {
                throw new ArchiveException("Block content mismatch");
            }

            _archiveContents.Set(hash, content);

            archive.AddMissingBlock(blockIndex);
            ModifyArchive(RootStorage, archive);
        }

        public byte[] ReadArchiveBlock(Archive archive, int blockIndex)
        {
            Throw.IfNull(archive, nameof(archive));
            Throw.If(blockIndex < 0 || blockIndex >= archive.BlockCount, "invalid block index");

            var hash = archive.MerkleTree.GetHash(blockIndex);

            if (_archiveContents.ContainsKey(hash))
            {
                return _archiveContents.Get(hash);
            }

            return null;
        }

        public void AddOwnerToArchive(StorageContext storage, Archive archive, Address owner)
        {
            archive.AddOwner(owner);
            ModifyArchive(storage, archive);
        }

        public void RemoveOwnerFromArchive(StorageContext storage, Archive archive, Address owner)
        {
            archive.RemoveOwner(owner);

            if (archive.OwnerCount <= 0)
            {
                DeleteArchive(storage, archive);
            }
            else
            {
                ModifyArchive(storage, archive);
            }
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
            // check if something with this name already exists
            if (PlatformExists(storage, name))
            {
                return -1;
            }

            var platformList = this.GetSystemList(PlatformTag, storage);
            var platformID = (byte)(1 + platformList.Count());

            //var chainAddress = Address.FromHash(name);
            var entry = new PlatformInfo(name, fuelSymbol, new PlatformSwapAddress[] {
                new PlatformSwapAddress() { LocalAddress = interopAddress, ExternalAddress = externalAddress }
            });

            // add to persistent list of tokens
            platformList.Add(name);

            EditPlatform(storage, name, entry);
            // notify oracles on new platform
            this.Notify(storage);
            return platformID;
        }

        private byte[] GetPlatformInfoKey(string name)
        {
            return GetNexusKey($"platform.{name}");
        }

        private void EditPlatform(StorageContext storage, string name, PlatformInfo platformInfo)
        {
            var key = GetPlatformInfoKey(name);
            var bytes = Serialization.Serialize(platformInfo);
            storage.Put(key, bytes);
        }

        public bool PlatformExists(StorageContext storage, string name)
        {
            if (name == DomainSettings.PlatformName)
            {
                return true;
            }

            var key = GetPlatformInfoKey(name);
            return storage.Has(key);
        }

        public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
        {
            var key = GetPlatformInfoKey(name);
            if (storage.Has(key))
            {
                var bytes = storage.Get(key);
                return Serialization.Unserialize<PlatformInfo>(bytes);
            }

            throw new ChainException($"Platform does not exist ({name})");
        }
        #endregion

        #region Contracts
        internal void CreateContract(StorageContext storage, string name, byte[] script)
        {
            var contractList = this.GetSystemList(ContractTag, storage);
            
            /*
            var entry = new PlatformInfo(name, fuelSymbol, new PlatformSwapAddress[] {
                new PlatformSwapAddress() { LocalAddress = interopAddress, ExternalAddress = externalAddress }
            });

            // add to persistent list of tokens
            contractList.Add(name);

            EditPlatform(storage, name, entry);
            return platformID;*/
        }

        private byte[] GetContractInfoKey(string name)
        {
            return GetNexusKey($"contract.{name}");
        }

        /*
        private void EditContract(StorageContext storage, string name, PlatformInfo platformInfo)
        {
            var key = GetPlatformInfoKey(name);
            var bytes = Serialization.Serialize(platformInfo);
            storage.Put(key, bytes);
        }*/

        public static bool IsNativeContract(string name)
        {
            NativeContractKind kind;
            return Enum.TryParse<NativeContractKind>(name, true, out kind);
        }

        public bool ContractExists(StorageContext storage, string name)
        {
            if (IsNativeContract(name))
            {
                return true;
            }

            var key = GetContractInfoKey(name);
            return storage.Has(key);
        }

        /*
        public PlatformInfo GetPlatformInfo(StorageContext storage, string name)
        {
            var key = GetPlatformInfoKey(name);
            if (storage.Has(key))
            {
                var bytes = storage.Get(key);
                return Serialization.Unserialize<PlatformInfo>(bytes);
            }

            throw new ChainException($"Platform does not exist ({name})");
        }*/
        #endregion

        #region ORGANIZATIONS
        internal void CreateOrganization(StorageContext storage, string ID, string name, byte[] script)
        {
            var organizationList = GetSystemList(OrganizationTag, storage);

            var organization = new Organization(ID, storage);
            organization.Init(name, script);

            // add to persistent list of tokens
            organizationList.Add(ID);

            var organizationMap = GetSystemMap(OrganizationTag, storage);
            organizationMap.Set<Address, string>(organization.Address, ID);
        }

        public bool OrganizationExists(StorageContext storage, string name)
        {
            var orgs = GetOrganizations(storage);
            return orgs.Contains(name);
        }

        public Organization GetOrganizationByName(StorageContext storage, string name)
        {
            if (OrganizationExists(storage, name))
            {
                var org = new Organization(name, storage);
                return org;
            }

            return null;
        }

        public Organization GetOrganizationByAddress(StorageContext storage, Address address)
        {
            var organizationMap = GetSystemMap(OrganizationTag, storage);
            if (organizationMap.ContainsKey<Address>(address))
            {
                var name = organizationMap.Get<Address, string>(address);
                return GetOrganizationByName(storage, name);
            }

            return null;
        }
        #endregion

        public int GetIndexOfChain(string name)
        {
            var chains = this.GetChains(RootStorage);
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
                //return RootChain.InvokeContract(storage, Nexus.GovernanceContractName, nameof(GovernanceContract.GetValue), name).AsNumber();
                return OptimizedGetGovernanceValue(storage, name);
            }

            return 0;
        }

        private BigInteger OptimizedGetGovernanceValue(StorageContext storage, string name)
        {
            var valueMapKey = Encoding.UTF8.GetBytes($".{Nexus.GovernanceContractName}._valueMap");
            var valueMap = new StorageMap(valueMapKey, storage);

            Throw.If(valueMap.ContainsKey(name) == false, "invalid value name");

            var value = valueMap.Get<string, BigInteger>(name);
            return value;
        }

        public void RegisterPlatformAddress(StorageContext storage, string platform, Address localAddress, string externalAddress)
        {
            var platformInfo = GetPlatformInfo(storage, platform);
            
            foreach (var entry in platformInfo.InteropAddresses)
            {
                Throw.If(entry.LocalAddress == localAddress || entry.ExternalAddress== externalAddress, "address already part of platform interops");
            }

            var newEntry = new PlatformSwapAddress()
            {
                ExternalAddress = externalAddress,
                LocalAddress = localAddress,
            };

            platformInfo.AddAddress(newEntry);
            EditPlatform(storage, platform, platformInfo);
        }

        // TODO optimize this
        public bool IsPlatformAddress(StorageContext storage, Address address)
        {
            if (!address.IsInterop)
            {
                return false;
            }

            var platforms = this.GetPlatforms(storage);
            foreach (var platform in platforms)
            {
                var info = GetPlatformInfo(storage, platform);

                foreach (var entry in info.InteropAddresses)
                {
                    if (entry.LocalAddress == address)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public readonly StorageContext RootStorage;

        private StorageList GetSystemList(string name, StorageContext storage)
        {
            var key = System.Text.Encoding.UTF8.GetBytes($".{name}.list");
            return new StorageList(key, storage);
        }

        private StorageMap GetSystemMap(string name, StorageContext storage)
        {
            var key = System.Text.Encoding.UTF8.GetBytes($".{name}.map");
            return new StorageMap(key, storage);
        }

        private const string TokenTag = "tokens";
        private const string ContractTag = "contracts";
        private const string ChainTag = "chains";
        private const string PlatformTag = "platforms";
        private const string FeedTag = "feeds";
        private const string OrganizationTag = "orgs";

        public string[] GetTokens(StorageContext storage)
        {
            var list = GetSystemList(TokenTag, storage);
            return list.All<string>();
        }

        public string[] GetContracts(StorageContext storage)
        {
            var list = GetSystemList(ContractTag, storage);
            return list.All<string>();
        }

        public string[] GetChains(StorageContext storage)
        {
            var list = GetSystemList(ChainTag, storage);
            return list.All<string>();
        }

        public string[] GetPlatforms(StorageContext storage)
        {
            var list = GetSystemList(PlatformTag, storage);
            return list.All<string>();
        }

        public string[] GetFeeds(StorageContext storage)
        {
            var list = GetSystemList(FeedTag, storage);
            return list.All<string>();
        }

        public string[] GetOrganizations(StorageContext storage)
        {
            var list = GetSystemList(OrganizationTag, storage);
            return list.All<string>();
        }

        private byte[] GetNexusKey(string key)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes($".nexus.{key}");
            return bytes;
        }

        public Address GetGenesisAddress(StorageContext storage)
        {
            var key = GetNexusKey("owner");
            if (storage.Has(key))
            {
                return storage.Get<Address>(key);
            }

            return Address.Null;
        }

        public Hash GetGenesisHash(StorageContext storage)
        {
            var key = GetNexusKey("hash");
            if (storage.Has(key))
            {
                return storage.Get<Hash>(key);
            }

            return Hash.Null;
        }

        public Block GetGenesisBlock()
        {
            if (HasGenesis)
            {
                var genesisHash = GetGenesisHash(RootStorage);
                return RootChain.GetBlockByHash(genesisHash);
            }

            return null;
        }

        public bool TokenExistsOnPlatform(string symbol, string platform, StorageContext storage)
        {
            var key = GetNexusKey($"{symbol}.{platform}.hash");
            if (storage.Has(key))
            {
                return true;
            }

            return false;
        }

        public Hash GetTokenPlatformHash(string symbol, string platform, StorageContext storage)
        {
            if (platform == DomainSettings.PlatformName)
            {
                return Hash.FromString(symbol);
            }

            var key = GetNexusKey($"{symbol}.{platform}.hash");
            if (storage.Has(key))
            {
                return storage.Get<Hash>(key);
            }

            return Hash.Null;
        }

        public Hash[] GetPlatformTokenHashes(string platform, StorageContext storage)
        {
            var tokens = GetTokens(storage);

            var hashes = new List<Hash>();

            if (platform == DomainSettings.PlatformName)
            {
                foreach (var token in tokens)
                {
                    hashes.Add(Hash.FromString(token));
                }
                return hashes.ToArray();
            }

            foreach (var token in tokens)
            {
                var key = GetNexusKey($"{token}.{platform}.hash");
                if (storage.Has(key))
                {
                    var tokenHash = storage.Get<Hash>(key);
                    if (tokenHash != null)
                    {
                        hashes.Add(tokenHash);
                    }
                }
            }

            return hashes.Distinct().ToArray();
        }

        public string GetPlatformTokenByHash(Hash hash, string platform, StorageContext storage)
        {
            var tokens = GetTokens(storage);

            if (platform == DomainSettings.PlatformName)
            {
                foreach (var token in tokens)
                {
                    if (Hash.FromString(token) == hash)
                        return token;
                }
            }

            foreach (var token in tokens)
            {
                var key = GetNexusKey($"{token}.{platform}.hash");
                if (HasTokenPlatformHash(token, platform, storage))
                {
                    var tokenHash = storage.Get<Hash>(key);
                    if (tokenHash == hash)
                    {
                        return token;
                    }
                }
            }

            _logger.Warning($"Token hash {hash} doesn't exist!");
            return null;
        }

        public void SetPlatformTokenHash(string symbol, string platform, Hash hash, StorageContext storage)
        {
            var tokenKey = GetTokenInfoKey(symbol);
            if (!storage.Has(tokenKey))
            {
                throw new ChainException($"Token does not exist ({symbol})");
            }

            if (platform == DomainSettings.PlatformName)
            {
                throw new ChainException($"cannot set token hash of {symbol} for native platform");
            }

            var bytes = storage.Get(tokenKey);
            var info = Serialization.Unserialize<TokenInfo>(bytes);

            if (!info.Flags.HasFlag(TokenFlags.Swappable))
            {
                info.Flags |= TokenFlags.Swappable;
                EditToken(storage, symbol, info);
            }

            var hashKey = GetNexusKey($"{symbol}.{platform}.hash");

            //should be updateable since a foreign token hash could change
            if (storage.Has(hashKey))
            {
                _logger.Warning($"Token hash of {symbol} already set for platform {platform}, updating to {hash}");
            }

            storage.Put<Hash>(hashKey, hash);
        }

        public bool HasTokenPlatformHash(string symbol, string platform, StorageContext storage)
        {
            if (platform == DomainSettings.PlatformName)
            {
                return true;
            }

            var key = GetNexusKey($"{symbol}.{platform}.hash");
            return storage.Has(key);
        }

        internal IToken GetTokenInfo(StorageContext storage, Address contractAddress)
        {
            var symbols = GetTokens(storage);
            foreach (var symbol in symbols)
            {
                var tokenAddress = TokenUtils.GetContractAddress(symbol);

                if (tokenAddress == contractAddress)
                {
                    var token = GetTokenInfo(storage, symbol);
                    return token;
                }
            }

            return null;
        }

        internal void MigrateTokenOwner(StorageContext storage, Address oldOwner, Address newOwner)
        {
            var symbols = GetTokens(storage);
            foreach (var symbol in symbols)
            {
                var token = (TokenInfo) GetTokenInfo(storage, symbol);
                if (token.Owner == oldOwner)
                {
                    token.Owner = newOwner;
                    EditToken(storage, symbol, token);
                }
            }
        }

        internal void UpgradeTokenContract(StorageContext storage, string symbol, byte[] script, ContractInterface abi)
        {
            var key = GetTokenInfoKey(symbol);
            if (!storage.Has(key))
            {
                throw new ChainException($"Cannot upgrade non-existing token contract: {symbol}");
            }

            var bytes = storage.Get(key);
            var info = Serialization.Unserialize<TokenInfo>(bytes);

            info = new TokenInfo(info.Symbol, info.Name, info.Owner, info.MaxSupply, info.Decimals, info.Flags, script, abi);
            bytes = Serialization.Serialize(info);
            storage.Put(key, bytes);
        }

        public CustomContract GetTokenContract(StorageContext storage, string symbol)
        {
            if (TokenExists(storage, symbol))
            {
                var token = GetTokenInfo(storage, symbol);

                return new CustomContract(symbol, token.Script, token.ABI);
            }

            return null;
        }

        public CustomContract GetTokenContract(StorageContext storage, Address contractAddress)
        {
            var token = GetTokenInfo(storage, contractAddress);
            if (token != null)
            {
                return new CustomContract(token.Symbol, token.Script, token.ABI);
            }

            return null;
        }

        public uint GetProtocolVersion(StorageContext storage)
        {
            return (uint)this.GetGovernanceValue(storage, Nexus.NexusProtocolVersionTag);
        }
    }
}
