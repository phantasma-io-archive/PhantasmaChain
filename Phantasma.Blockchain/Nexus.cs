using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public string Name { get; }

        public Chain RootChain { get; }

        public Token FuelToken { get; private set; }
        public Token StakingToken { get; private set; }
        public Token StableToken { get; private set; }

        private readonly Dictionary<string, Chain> _chains = new Dictionary<string, Chain>();
        private readonly Dictionary<string, Token> _tokens = new Dictionary<string, Token>();

        private Dictionary<Token, Dictionary<BigInteger, TokenContent>> _tokenContents = new Dictionary<Token, Dictionary<BigInteger, TokenContent>>();

        public IEnumerable<Chain> Chains
        {
            get
            {
                lock (_chains)
                {
                    return _chains.Values;
                }
            }
        }

        public IEnumerable<Token> Tokens => _tokens.Values;

        public readonly int CacheSize;

        public readonly Address GenesisAddress;
        public readonly Address StorageAddress;

        private readonly List<IChainPlugin> _plugins = new List<IChainPlugin>();

        private readonly Logger _logger;

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(string name, Address genesisAddress, int cacheSize, Logger logger = null)
        {
            GenesisAddress = genesisAddress;

            this.CacheSize = cacheSize;

            var temp = ByteArrayUtils.DupBytes(genesisAddress.PublicKey);
            var str = "STORAGE";
            for (int i=0; i<str.Length; i++)
            {
                temp[i] = (byte)str[i];
            }
            StorageAddress = new Address(temp);

            _logger = logger;
            Name = name;

            // TODO this probably should be done using a normal transaction instead of here
            var contracts = new List<SmartContract>
            {
                new NexusContract(),
                new TokenContract(),
                new ConsensusContract(),
                new GovernanceContract(),
                new AccountContract(),
                new OracleContract(),
                new ExchangeContract(),
                new MarketContract(),
                new GasContract(),
            };

            RootChain = new Chain(this, "main", contracts, logger);
            _chains[RootChain.Name] = RootChain;
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
            lock (_chains)
            {
                foreach (var chain in _chains.Values)
                {
                    if (chain.ContainsBlock(hash))
                    {
                        return chain;
                    }
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

        public Block FindBlockForTransaction(Transaction tx)
        {
            return FindBlockForHash(tx.Hash);
        }

        public Block FindBlockForHash(Hash hash)
        {
            lock (_chains)
            {
                foreach (var chain in _chains.Values)
                {
                    if (chain.ContainsTransaction(hash))
                    {
                        return chain.FindTransactionBlock(hash);
                    }
                }
            }

            return null;
        }

        public Block FindBlockByHash(Hash hash)
        {
            lock (_chains)
            {
                foreach (var chain in _chains.Values)
                {
                    if (chain.ContainsBlock(hash))
                    {
                        return chain.FindBlockByHash(hash);
                    }
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
            foreach (var chain in Chains)
            {
                var tx = chain.FindTransactionByHash(hash);
                if (tx != null)
                {
                    return tx;
                }
            }

            return null;
        }

        public int GetTotalTransactionCount()
        {
            return Chains.Sum(x => (int)x.TransactionCount);
        }
        #endregion

        #region CHAINS
        internal Chain CreateChain(Address owner, string name, Chain parentChain, Block parentBlock)
        {
            if (parentChain == null || parentBlock == null)
            {
                return null;
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

            lock (_chains)
            {
                _chains[name] = chain;
            }

            return chain;
        }

        public bool ContainsChain(Chain chain)
        {
            if (chain == null)
            {
                return false;
            }

            bool result;
            lock (_chains)
            {
                result = _chains.ContainsKey(chain.Name);
            }

            return result;
        }

        public Chain FindChainByAddress(Address address)
        {
            lock (_chains)
            {
                foreach (var entry in _chains.Values)
                {
                    if (entry.Address == address)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        public Chain FindChainByName(string name)
        {
            lock (_chains)
            {
                if (_chains.ContainsKey(name))
                {
                    return _chains[name];
                }
            }

            return null;
        }

        #endregion

        #region TOKENS
        internal Token CreateToken(Chain chain, Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags)
        {
            if (symbol == null || name == null || maxSupply < 0)
            {
                return null;
            }

            // check if already exists something with that name
            var temp = FindTokenBySymbol(symbol);
            if (temp != null)
            {
                return null;
            }

            var token = new Token(chain, owner, symbol, name, maxSupply, decimals, flags);

            if (symbol == FuelTokenSymbol)
            {
                FuelToken = token;
            }
            else
            if (symbol == StakingTokenSymbol)
            {
                StakingToken = token;
            }
            else
            if (symbol == StableTokenSymbol)
            {
                StableToken = token;
            }

            _tokens[symbol] = token;

            return token;
        }

        public Token FindTokenBySymbol(string symbol)
        {
            if (_tokens.ContainsKey(symbol))
            {
                return _tokens[symbol];
            }

            return null;
        }
        #endregion

        #region NFT
        internal BigInteger CreateNFT(Token token, Address chainAddress, Address ownerAddress, byte[] rom, byte[] ram)
        {
            lock (_tokenContents)
            {
                Dictionary<BigInteger, TokenContent> contents;

                if (_tokenContents.ContainsKey(token))
                {
                    contents = _tokenContents[token];
                }
                else
                {
                    contents = new Dictionary<BigInteger, TokenContent>();
                    _tokenContents[token] = contents;
                }

                var tokenID = token.GenerateID();

                var content = new TokenContent(rom, ram);
                content.CurrentChain = chainAddress;
                content.CurrentOwner = ownerAddress;
                contents[tokenID] = content;

                return tokenID;
            }
        }

        internal bool DestroyNFT(Token token, BigInteger tokenID)
        {
            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(token))
                {
                    var contents = _tokenContents[token];

                    if (contents.ContainsKey(tokenID))
                    {
                        contents.Remove(tokenID);
                        return true;
                    }
                }
            }

            return false;
        }

        public TokenContent GetNFT(Token token, BigInteger tokenID)
        {
            lock (_tokenContents)
            {
                if (_tokenContents.ContainsKey(token))
                {
                    var contents = _tokenContents[token];

                    if (contents.ContainsKey(tokenID))
                    {
                        var result = contents[tokenID];

                        var chain = FindChainByAddress(result.CurrentChain);
                        if (chain != null)
                        {
                            result.CurrentOwner = chain.GetTokenOwner(token, tokenID);
                        }
                        else
                        {
                            result.CurrentOwner = Address.Null;
                        }

                        return result;
                    }
                }
            }

            return null;
        }
        #endregion

        #region GENESIS
        private Transaction TokenCreateTx(Chain chain, KeyPair owner, string symbol, string name, BigInteger totalSupply, int decimals, TokenFlags flags)
        {
            var sb = ScriptUtils.BeginScript();

            if (symbol != FuelTokenSymbol)
            {
                sb.AllowGas(owner.Address, Address.Null, 1, 9999);
            }

            sb.CallContract(ScriptBuilderExtensions.NexusContract, "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags);

            if (symbol == FuelTokenSymbol)
            {
                sb.CallContract(ScriptBuilderExtensions.TokenContract, "MintTokens", owner.Address, symbol, totalSupply);
                sb.AllowGas(owner.Address, Address.Null, 1, 9999); // done here only because before fuel token does not exist yet!
            }
            else
            if (symbol == StakingTokenSymbol)
            {
                sb.CallContract(ScriptBuilderExtensions.TokenContract, "MintTokens", owner.Address, symbol, UnitConversion.ToBigInteger(8863626, StakingTokenDecimals));
            }

            var script = sb.SpendGas(owner.Address).EndScript();

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

        public const string FuelTokenSymbol = "ALMA";
        public const string FuelTokenName = "Phantasma Energy";
        public const int FuelTokenDecimals = 10;

        public const string StakingTokenSymbol = "SOUL";
        public const string StakingTokenName = "Phantasma Stake";
        public const int StakingTokenDecimals = 8;

        public const string StableTokenSymbol = "SUS";
        public const string StableTokenName = "Phantasma Dollar";
        public const int StableTokenDecimals = 8;

        public static readonly BigInteger PlatformSupply = UnitConversion.ToBigInteger(100000000, FuelTokenDecimals);

        public bool CreateGenesisBlock(KeyPair owner, Timestamp timestamp)
        {
            if (FuelToken != null)
            {
                return false;
            }

            var transactions = new List<Transaction>
            {
                TokenCreateTx(RootChain, owner, FuelTokenSymbol, FuelTokenName, PlatformSupply, FuelTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Fuel),
                TokenCreateTx(RootChain, owner, StableTokenSymbol, StableTokenName, 0, StableTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.Stable),
                TokenCreateTx(RootChain, owner, StakingTokenSymbol, StakingTokenName, UnitConversion.ToBigInteger(91136374, StakingTokenDecimals), StakingTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.Stakable | TokenFlags.External),

                SideChainCreateTx(RootChain, owner, "privacy"),
                SideChainCreateTx(RootChain, owner, "vault"),
                SideChainCreateTx(RootChain, owner, "bank"),
                SideChainCreateTx(RootChain, owner, "interop"),
                // SideChainCreateTx(RootChain, owner, "market"), TODO
                SideChainCreateTx(RootChain, owner, "apps"),
                SideChainCreateTx(RootChain, owner, "energy"),

                TokenCreateTx(RootChain, owner, "NEO", "NEO", UnitConversion.ToBigInteger(100000000, 0), 0, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.External),
                TokenCreateTx(RootChain, owner, "ETH", "Ethereum", UnitConversion.ToBigInteger(0, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible | TokenFlags.External),
                TokenCreateTx(RootChain, owner, "EOS", "EOS", UnitConversion.ToBigInteger(1006245120, 18), 18, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible | TokenFlags.External),

                ConsensusStakeCreateTx(RootChain, owner)
            };

            var genesisMessage = Encoding.UTF8.GetBytes("A Phantasma was born...");
            var block = new Block(Chain.InitialHeight, RootChain.Address, timestamp, transactions.Select(tx => tx.Hash), Hash.Null, genesisMessage);

            try
            {
                RootChain.AddBlock(block, transactions);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public int GetConfirmationsOfHash(Hash hash)
        {
            var block = FindBlockForHash(hash);
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

            var block = FindBlockForTransaction(transaction);
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
    }
}
