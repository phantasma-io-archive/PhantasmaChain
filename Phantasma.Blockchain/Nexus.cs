using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Numerics;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Storage;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public static readonly string RootChainName = "main";
        private static readonly string ChainAddressMapKey = "chain.";

        public Chain RootChain => FindChainByName(RootChainName);

        private KeyValueStore<string, byte[]> _vars;
        private Dictionary<string, KeyValueStore<BigInteger, TokenContent>> _tokenContents = new Dictionary<string, KeyValueStore<BigInteger, TokenContent>>();

        public bool Ready { get; private set; }

        public string Name
        {
            get
            {
                var bytes = _vars.Get(nameof(Name));
                var result = Serialization.Unserialize<string>(bytes);
                return result;
            }

            private set
            {
                var bytes = Serialization.Serialize(value);
                _vars.Set(nameof(Name), bytes);
            }
        }

        public Address RootAddress
        {
            get
            {
                return Serialization.Unserialize<Address>(_vars.Get(nameof(RootAddress)));
            }

            private set
            {
                _vars.Set(nameof(RootAddress), Serialization.Serialize(value));
            }
        }

        public Address GenesisAddress
        {
            get
            {
                return Serialization.Unserialize<Address>(_vars.Get(nameof(GenesisAddress)));
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
                var result = Serialization.Unserialize<Hash>(_vars.Get(nameof(GenesisHash)));
                return result;
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
                return Serialization.Unserialize<string[]>(_vars.Get(nameof(Tokens)));
            }

            private set
            {
                var symbols = value.ToArray();
                _vars.Set(nameof(Tokens), Serialization.Serialize(symbols));
            }
        }

        public IEnumerable<string> Chains
        {
            get
            {
                return Serialization.Unserialize<string[]>(_vars.Get(nameof(Chains)));
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
            }
            catch
            {
                Ready = false;
            }

            _logger = logger;
        }

        internal IKeyValueStoreAdapter CreateKeyStoreAdapter(Address address, string name)
        {
            return CreateKeyStoreAdapter(address.Text + "_ " + name);
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
        internal Chain CreateChain(Address owner, string name, Chain parentChain, Block parentBlock)
        {
            if (name != RootChainName)
            {
                if (parentChain == null || parentBlock == null)
                {
                    return null;
                }
            }

            if (owner != GenesisAddress)
            {
                if (parentChain.Level < 2)
                {
                    return null;
                }
            }

            if (!Chain.ValidateName(name))
            {
                return null;
            }

            // check if already exists something with that name
            var temp = FindChainByName(name);
            if (temp != null)
            {
                return null;
            }

            SmartContract contract;

            switch (name)
            {
                case "privacy": contract = new PrivacyContract(); break;
                case "storage": contract = new StorageContract(); break;
                case "vault": contract = new VaultContract(); break;
                case "bank": contract = new BankContract(); break;
                case "apps": contract = new AppsContract(); break;
                case "dex": contract = new ExchangeContract(); break;
                case "market": contract = new MarketContract(); break;
                case "energy": contract = new EnergyContract(); break;
                case "nacho": contract = new NachoContract(); break;
                case "casino": contract = new CasinoContract(); break;
                default:
                    {
                        var sb = new ScriptBuilder();
                        contract = new CustomContract(sb.ToScript(), null); // TODO
                        break;
                    }
            }

            var tokenContract = new TokenContract();
            var gasContract = new GasContract();

            var chain = new Chain(this, name, new[] { tokenContract, gasContract, contract }, _logger, parentChain, parentBlock);

            // add to persisent list of chains
            var chainList = this.Chains.ToList();
            chainList.Add(name);
            this.Chains = chainList;

            // add address mapping 
            this._vars.Set(ChainAddressMapKey + chain.Address.Text, Encoding.UTF8.GetBytes(chain.Name));

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

            return FindChainByName(chainName) != null;
        }

        private Dictionary<string, Chain> _chainCache = new Dictionary<string, Chain>();

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
            if (_chainCache.ContainsKey(name))
            {
                return _chainCache[name];
            }

            throw new Exception("fixme pls");
            /*
            var chain = new Chain(this);
            _chainCache[name] = chain;
            return chain;*/
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

        public BigInteger GetTokenSupply(string symbol)
        {
            if (!TokenExists(symbol))
            {
                throw new ChainException($"Token does not exist ({symbol})");
            }

            return EditTokenSupply(symbol, 0);
        }

        private BigInteger EditTokenSupply(string symbol, BigInteger change)
        {
            if (!TokenExists(symbol))
            {
                throw new ChainException($"Token does not exist ({symbol})");
            }

            var tokenInfo = GetTokenInfo(symbol);

            var key = "supply:" + symbol;

            BigInteger currentSupply;
            byte[] bytes;
            if (_vars.ContainsKey(key))
            {
                bytes = _vars.Get(key);
                currentSupply = Serialization.Unserialize<BigInteger>(bytes);
            }
            else {
                currentSupply = 0;
            }

            currentSupply += change;

            if (currentSupply<0)
            {
                throw new ChainException("Invalid negative supply");
            }
            else
            if (tokenInfo.IsCapped && currentSupply > tokenInfo.MaxSupply)
            {
                throw new ChainException("Exceeded max supply");
            }

            if (change != 0)
            {
                bytes = Serialization.Serialize(currentSupply);
                _vars.Set(key, bytes);
            }

            return currentSupply;
        }

        internal bool MintTokens(string symbol, StorageContext storage, BalanceSheet balances, SupplySheet supply, Address target, BigInteger amount)
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

            if (tokenInfo.IsCapped)
            {
                if (!supply.Mint(amount))
                {
                    return false;
                }
            }

            if (!balances.Add(storage, target, amount))
            {
                return false;
            }

            EditTokenSupply(symbol, amount);
            return true;
        }

        // NFT version
        internal bool MintToken(string symbol)
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

            EditTokenSupply(symbol, 1);

            return true;
        }

        internal bool BurnTokens(string symbol, StorageContext storage, BalanceSheet balances, SupplySheet supply, Address target, BigInteger amount)
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

            if (tokenInfo.IsCapped && !supply.Burn(amount))
            {
                return false;
            }

            if (!balances.Subtract(storage, target, amount))
            {
                return false;
            }

            EditTokenSupply(symbol, -amount);
            return true;
        }

        // NFT version
        internal bool BurnToken(string symbol)
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

            EditTokenSupply(symbol, -1);
            return true;
        }

        internal bool TransferTokens(string symbol, StorageContext storage, BalanceSheet balances, Address source, Address destination, BigInteger amount)
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

        internal bool TransferToken(string symbol, StorageContext storage, OwnershipSheet ownerships, Address source, Address destination, BigInteger ID)
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

            if (ID <= 0)
            {
                return false;
            }

            if (!ownerships.Take(storage, source, ID))
            {
                return false;
            }

            if (!ownerships.Give(storage, destination, ID))
            {
                return false;
            }

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

        internal BigInteger CreateNFT(string tokenSymbol, Address chainAddress, Address ownerAddress, byte[] rom, byte[] ram)
        {
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

                var content = new TokenContent(chainAddress, ownerAddress, rom, ram);
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

        internal bool EditNFTLocation(string tokenSymbol, BigInteger tokenID, Address chainAddress, Address owner)
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
        private Transaction TokenCreateTx(Chain chain, KeyPair owner, string symbol, string name, BigInteger totalSupply, int decimals, TokenFlags flags, bool useGas)
        {
            var sb = ScriptUtils.BeginScript();

            if (useGas)
            {
                sb.AllowGas(owner.Address, Address.Null, 1, 9999);
            }

            sb.CallContract(ScriptBuilderExtensions.NexusContract, "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags);

            if (symbol == StakingTokenSymbol)
            {
                sb.CallContract(ScriptBuilderExtensions.TokenContract, "MintTokens", owner.Address, symbol, UnitConversion.ToBigInteger(8863626, StakingTokenDecimals));
            }
            else
            if (symbol == FuelTokenSymbol)
            {
                // requires staking token to be created previously
                // note this is a completly arbitrary number just to be able to generate energy in the genesis, better change it later
                sb.CallContract(ScriptBuilderExtensions.EnergyContract, "Stake", owner.Address, UnitConversion.ToBigInteger(100000, StakingTokenDecimals));
                sb.CallContract(ScriptBuilderExtensions.EnergyContract, "Claim", owner.Address, owner.Address);
            }

            if (useGas)
            {
                sb.SpendGas(owner.Address);
            }

            var script = sb.EndScript();

            var tx = new Transaction(Name, chain.Name, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);

            return tx;
        }

        private Transaction SideChainCreateTx(Chain chain, KeyPair owner, string name)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract(ScriptBuilderExtensions.NexusContract, "CreateChain", owner.Address, name, RootChain.Name).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, chain.Name, script, Timestamp.Now + TimeSpan.FromDays(300));
            tx.Sign(owner);
            return tx;
        }

        private Transaction ConsensusStakeCreateTx(Chain chain, KeyPair owner)
        {
            var script = ScriptUtils.
                BeginScript().
                AllowGas(owner.Address, Address.Null, 1, 9999).
                CallContract("consensus", "Stake", owner.Address).
                SpendGas(owner.Address).
                EndScript();

            var tx = new Transaction(Name, chain.Name, script, Timestamp.Now + TimeSpan.FromDays(300));
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

            // TODO this probably should be done using a normal transaction instead of here
            var contracts = new List<SmartContract>
            {
                new NexusContract(),
                new TokenContract(),
                new ConsensusContract(),
                new GovernanceContract(),
                new AccountContract(),
                new FriendContract(),
                new OracleContract(),
                new ExchangeContract(),
                new MarketContract(),
                new GasContract(),
                new EnergyContract(),
            };

            // create root chain, TODO this probably should also be included as a transaction later
            var rootChain = new Chain(this, RootChainName, contracts, this._logger);
            _chainCache[rootChain.Name] = rootChain;
            this.RootAddress = rootChain.Address;

            // create genesis transactions
            var transactions = new List<Transaction>
            {
                TokenCreateTx(rootChain, owner, StakingTokenSymbol, StakingTokenName, UnitConversion.ToBigInteger(91136374, StakingTokenDecimals), StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External, false),
                TokenCreateTx(rootChain, owner, FuelTokenSymbol, FuelTokenName, PlatformSupply, FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel, false),
                TokenCreateTx(rootChain, owner, StableTokenSymbol, StableTokenName, 0, StableTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stable, true),

                SideChainCreateTx(rootChain, owner, "privacy"),
                SideChainCreateTx(rootChain, owner, "vault"),
                SideChainCreateTx(rootChain, owner, "bank"),
                SideChainCreateTx(rootChain, owner, "interop"),
                // SideChainCreateTx(RootChain, owner, "market"), TODO
                SideChainCreateTx(rootChain, owner, "apps"),
                SideChainCreateTx(rootChain, owner, "energy"),

                // TODO remove those from here, theyare here just for testing
                SideChainCreateTx(rootChain, owner, "nacho"),
                SideChainCreateTx(rootChain, owner, "casino"),

                TokenCreateTx(rootChain, owner, "NEO", "NEO", UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.External, true),
                TokenCreateTx(rootChain, owner, "ETH", "Ethereum", UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.External, true),
                TokenCreateTx(rootChain, owner, "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External, true),

                ConsensusStakeCreateTx(rootChain, owner)
            };

            var genesisMessage = Encoding.UTF8.GetBytes("A Phantasma was born...");
            var block = new Block(Chain.InitialHeight, rootChain.Address, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, genesisMessage);

            try
            {
                rootChain.AddBlock(block, transactions);
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
                    return (int)(1 + (chain.LastBlock.Height - block.Height));
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

        /*
        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarString(Name);
            writer.WriteAddress(GenesisAddress);
            writer.WriteHash(GenesisHash);

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
