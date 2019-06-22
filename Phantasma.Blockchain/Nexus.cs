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
        private static readonly string ChainParentBlockKey = "chain.block.";
        private static readonly string ChainChildrenBlockKey = "chain.children.";

        public static readonly string GasContractName = "gas";
        public static readonly string TokenContractName = "token";

        public Chain RootChain => FindChainByName(RootChainName);

        private KeyValueStore<string, byte[]> _vars;
        private Dictionary<string, KeyValueStore<BigInteger, TokenContent>> _tokenContents = new Dictionary<string, KeyValueStore<BigInteger, TokenContent>>();

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

        private readonly List<IChainPlugin> _plugins = new List<IChainPlugin>();

        private readonly Logger _logger;

        private Func<string, IKeyValueStoreAdapter> _adapterFactory = null;

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(Logger logger = null, Func<string, IKeyValueStoreAdapter> adapterFactory= null)
        {
            this._adapterFactory = adapterFactory;

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

        public Address FindValidatorForBlock(Block block)
        {
            Throw.IfNull(block, nameof(block));

            var chain = FindChainForBlock(block);
            if (chain == null)
            {
                return Address.Null;
            }

            var epoch = chain.CurrentEpoch;//todo was chain.FindEpochForBlockHash(block.Hash);
            if (epoch == null)
            {
                return Address.Null;
            }

            return epoch.ValidatorAddress;
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
            if (!AccountContract.ValidateAddressName(name))
            {
                return Address.Null;
            }

            var chain = RootChain;
            return (Address)chain.InvokeContract("account", "LookUpName", name);
        }

        public string LookUpAddress(Address address)
        {
            var chain = RootChain;
            return (string)chain.InvokeContract("account", "LookUpAddress", address);
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
                case "consensus":  contract = new ConsensusContract(); break;
                case "governance":  contract = new GovernanceContract(); break;
                case "account":  contract  = new AccountContract(); break;
                case "friends": contract  = new FriendContract(); break;
                case "exchange": contract  = new ExchangeContract(); break;
                case "market":    contract  = new MarketContract(); break;
                case "energy":   contract  = new EnergyContract(); break;
                case "token": contract = new TokenContract(); break;
                case "swap": contract = new SwapContract(); break;
                case "gas":  contract  = new GasContract(); break;
                case "privacy": contract  = new PrivacyContract(); break;
                case "storage": contract  = new StorageContract(); break;
                case "vault": contract  = new VaultContract(); break;
                case "bank": contract  = new BankContract(); break;
                case "apps": contract  = new AppsContract(); break;
                case "dex": contract  = new ExchangeContract(); break;
                case "nacho": contract  = new NachoContract(); break;
                case "casino": contract  = new CasinoContract(); break;
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
        internal Chain CreateChain(StorageContext storage, Address owner, string name, Chain parentChain, Block parentBlock, IEnumerable<string> contractNames)
        {
            if (name != RootChainName)
            {
                if (parentChain == null || parentBlock == null)
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
                this._vars.Set(ChainParentBlockKey + chain.Name, parentBlock.Hash.ToByteArray());

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

        private string LookUpChainNameByAddress(Address address)
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

        public Hash GetParentBlockByName(string chainName)
        {
            if (chainName == RootChainName)
            {
                return null;
            }

            var key = ChainParentBlockKey + chainName;
            if (_vars.ContainsKey(key))
            {
                var bytes = _vars.Get(key);
                return new Hash(bytes);
            }

            throw new Exception("Parent block not found for chain: " + chainName);
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

        #region TOKENS
        internal bool CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags)
        {
            if (symbol == null || name == null || maxSupply < 0)
            {
                return false;
            }

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

            var tokenInfo = new TokenInfo(owner, symbol, name, maxSupply, decimals, flags);
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

        internal bool MintTokens(string symbol, StorageContext storage, Chain chain, Address target, BigInteger amount)
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

            var supply = new SupplySheet(symbol, chain, this);
            if (!supply.Mint(storage, amount, tokenInfo.MaxSupply))
            {
                return false;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Add(storage, target, amount))
            {
                return false;
            }

            return true;
        }

        // NFT version
        internal bool MintToken(string symbol, StorageContext storage, Chain chain, Address target, BigInteger tokenID)
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

            var supply = new SupplySheet(symbol, chain, this);
            if (!supply.Mint(storage, 1, tokenInfo.MaxSupply))
            {
                return false;
            }

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Give(storage, target, tokenID))
            {
                return false;
            }

            EditNFTLocation(symbol, tokenID, chain.Address, target);
            return true;
        }

        internal bool BurnTokens(string symbol, StorageContext storage, Chain chain, Address target, BigInteger amount)
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

            var supply = new SupplySheet(symbol, chain, this);

            if (tokenInfo.IsCapped && !supply.Burn(storage, amount))
            {
                return false;
            }

            var balances = new BalanceSheet(symbol);
            if (!balances.Subtract(storage, target, amount))
            {
                return false;
            }

            return true;
        }

        // NFT version
        internal bool BurnToken(string symbol, StorageContext storage, Address target, BigInteger tokenID)
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

            if (!supply.Burn(storage, 1))
            {
                return false;
            }

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Take(storage, target, tokenID))
            {
                return false;
            }

            return true;
        }

        internal bool TransferTokens(string symbol, StorageContext storage, Chain chain, Address source, Address destination, BigInteger amount)
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

            var balances = new BalanceSheet(symbol);
            if (!balances.Subtract(storage, source, amount))
            {
                return false;
            }

            if (!balances.Add(storage, destination, amount))
            {
                return false;
            }

            return true;
        }

        internal bool TransferToken(string symbol, StorageContext storage, Chain chain, Address source, Address destination, BigInteger tokenID)
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

            var ownerships = new OwnershipSheet(symbol);
            if (!ownerships.Take(storage, source, tokenID))
            {
                return false;
            }

            if (!ownerships.Give(storage, destination, tokenID))
            {
                return false;
            }

            EditNFTLocation(symbol, tokenID, chain.Address, destination);
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

        internal BigInteger CreateNFT(string tokenSymbol, Address chainAddress, byte[] rom, byte[] ram, BigInteger value)
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

                var content = new TokenContent(chainAddress, chainAddress, rom, ram, value);
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
                        content = new TokenContent(chainAddress, owner, content.ROM, content.RAM, content.Value);
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
                        content = new TokenContent(content.CurrentChain, content.CurrentOwner, content.ROM, ram, content.Value);
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
        private Transaction TokenInitTx(KeyPair owner)
        {
            var sb = ScriptUtils.BeginScript();

            sb.CallContract(ScriptBuilderExtensions.TokenContract, "MintTokens", owner.Address, StakingTokenSymbol, UnitConversion.ToBigInteger(8863626, StakingTokenDecimals));
            // requires staking token to be created previously
            // note this is a completly arbitrary number just to be able to generate energy in the genesis, better change it later
            sb.CallContract(ScriptBuilderExtensions.EnergyContract, "Stake", owner.Address, UnitConversion.ToBigInteger(100000, StakingTokenDecimals));
            sb.CallContract(ScriptBuilderExtensions.EnergyContract, "Claim", owner.Address, owner.Address);

            var script = sb.EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);

            return tx;
        }

        private Transaction ChainCreateTx(KeyPair owner, string name, params string[] contracts)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(ScriptBuilderExtensions.NexusContract, "CreateChain", owner.Address, name, RootChain.Name, contracts).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction ConsensusStakeCreateTx(KeyPair owner)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("consensus", "Stake", owner.Address).
                CallContract(ScriptBuilderExtensions.SwapContract, "DepositTokens", owner.Address, StakingTokenSymbol, UnitConversion.ToBigInteger(1, StakingTokenDecimals)).
                CallContract(ScriptBuilderExtensions.SwapContract, "DepositTokens", owner.Address, FuelTokenSymbol, UnitConversion.ToBigInteger(100, FuelTokenDecimals)).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction TokenMetadataTx(KeyPair owner, string symbol, string field, object val)
        {
            var bytes = Serialization.Serialize(val);

            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("nexus", "SetTokenMetadata", symbol, field, bytes).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, RootChainName, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        public const string FuelTokenSymbol = "KCAL";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string StableTokenSymbol = "SUS";
        public const string StableTokenName = "Phantasma Dollar";
        public const int StableTokenDecimals = 8;

        public static readonly BigInteger PlatformSupply = UnitConversion.ToBigInteger(100000000, FuelTokenDecimals);

        public bool CreateGenesisBlock(string name, KeyPair owner, Timestamp timestamp)
        {
            if (Ready)
            {
                return false;
            }

            // TODO validate name
            this.Name = name;

            this.GenesisAddress = owner.Address;

            var rootChain = CreateChain(null, owner.Address, RootChainName, null, null, new[] { "nexus", "consensus", "governance", "account", "friends", "oracle", "exchange", "market", "energy", "swap", "interop", "storage", "apps"});

            CreateToken(owner.Address, StakingTokenSymbol, StakingTokenName, UnitConversion.ToBigInteger(91136374, StakingTokenDecimals), StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External);
            CreateToken(owner.Address, FuelTokenSymbol, FuelTokenName, PlatformSupply, FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel);
            CreateToken(owner.Address, StableTokenSymbol, StableTokenName, 0, StableTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stable);

            CreateToken(owner.Address, "NEO", "NEO", UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.External);
            CreateToken(owner.Address, "GAS", "GAS", UnitConversion.ToBigInteger(100000000, 8), 8, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Finite | TokenFlags.External);
            CreateToken(owner.Address, "ETH", "Ethereum", UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.External);
            CreateToken(owner.Address, "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External);

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                TokenInitTx(owner),

                ChainCreateTx(owner, "privacy", "privacy"),
                ChainCreateTx(owner, "bank", "bank", "vault"),
                // ChainCreateTx(owner, "market"), TODO

                TokenMetadataTx(owner, StakingTokenSymbol, "interop.neo", "ed07cffad18f1308db51920d99a2af60ac66a7b3"),
                TokenMetadataTx(owner, "NEO", "interop.neo", "c56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b"),
                TokenMetadataTx(owner, "GAS", "interop.neo", "602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7"),

                ConsensusStakeCreateTx(owner)
            };

            var genesisMessage = Encoding.UTF8.GetBytes("A Phantasma was born...");
            var block = new Block(Chain.InitialHeight, RootChainAddress, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, genesisMessage);

            try
            {
                rootChain.AddBlock(block, transactions, null);
            }
            catch (Exception e)
            {
                _logger.Error(e.ToString());
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
        public IEnumerable<Address> GetValidators()
        {
            var validators = (Address[])RootChain.InvokeContract("consensus", "GetValidators");
            return validators;
        }

        public int GetValidatorCount()
        {
            var count = (BigInteger)RootChain.InvokeContract("consensus", "GetActiveValidatorss");
            return (int)count;
        }

        public bool IsValidator(Address address)
        {
            return GetIndexOfValidator(address) >= 0;
        }

        public int GetIndexOfValidator(Address address)
        {
            if (address == Address.Null)
            {
                return -1;
            }

            if (RootChain == null)
            {
                return -1;
            }

            var result = (int)(BigInteger)RootChain.InvokeContract("consensus", "GetIndexOfValidator", address);
            return result;
        }

        public Address GetValidatorByIndex(int index)
        {
            if (RootChain == null)
            {
                return Address.Null;
            }

            Throw.If(index < 0, "invalid validator index");

            var result = (Address)RootChain.InvokeContract("consensus", "GetValidatorByIndex", (BigInteger)index);
            return result;
        }
        #endregion

        #region STORAGE
        public Archive FindArchive(Hash hash)
        {
            throw new NotImplementedException();
        }

        public Archive CreateArchive(MerkleTree merkleTree, ArchiveFlags flags)
        {
            var archive = FindArchive(merkleTree.Root);
            if (archive != null)
            {
                return archive;
            }

            throw new NotImplementedException();
        }

        public bool DeleteArchive(Hash hash)
        {
            var archive = FindArchive(hash);
            if (archive == null)
            {
                return false;
            }

            throw new NotImplementedException();
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
