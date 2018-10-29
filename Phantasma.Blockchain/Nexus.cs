using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public Chain RootChain { get; private set; }
        public Token NativeToken { get; private set; }
        public Token StableToken { get; private set; }

        private Dictionary<string, Chain> _chains = new Dictionary<string, Chain>();
        private Dictionary<string, Token> _tokens = new Dictionary<string, Token>();

        public IEnumerable<Chain> Chains => _chains.Values;
        public IEnumerable<Token> Tokens => _tokens.Values;

        public Address GenesisAddress { get; private set; }

        private List<INexusPlugin> _plugins = new List<INexusPlugin>();

        private Logger logger;

        /// <summary>
        /// The constructor bootstraps the main chain and all core side chains.
        /// </summary>
        public Nexus(KeyPair owner, Logger logger = null)
        {
            this.logger = logger;

            this.RootChain = new Chain(this, owner.Address, "main", new NexusContract(), logger, null);
            _chains[RootChain.Name] = RootChain;

            var genesisBlock = CreateGenesisBlock(owner);
            if (!RootChain.AddBlock(genesisBlock))
            {
                throw new ChainException("Genesis block failure");
            }
        }

        #region PLUGINS
        public void AddPlugin(INexusPlugin plugin)
        {
            _plugins.Add(plugin);
        }

        internal void PluginTriggerBlock(Chain chain, Block block)
        {
            foreach (var plugin in _plugins)
            {
                plugin.OnNewBlock(chain, block);

                foreach (var tx in block.Transactions)
                {
                    plugin.OnNewTransaction(chain, block, (Transaction) tx);
                }
            }
        }

        public T GetPlugin<T>() where T: INexusPlugin
        {
            foreach (var plugin in _plugins)
            {
                if (plugin is T)
                {
                    return (T)plugin;
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

            var chain = FindChainByKind(ContractKind.Account); // TODO cache this
            return (Address)chain.InvokeContract("LookUpName", name);
        }

        public string LookUpAddress(Address address)
        {
            var chain = FindChainByKind(ContractKind.Account); // TODO cache this
            return (string)chain.InvokeContract("LookUpAddress", address);
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
            return Chains.Sum(x => x.TransactionCount);
        }
        #endregion

        #region CHAINS
        internal Chain CreateChain(Address owner, string name, Chain parentChain, Block parentBlock)
        {
            if (parentChain == null || parentBlock == null)
            {
                return null;
            }

            if (owner != this.GenesisAddress)
            {
                if (parentChain.Contract.Kind != ContractKind.Apps && parentChain.Contract.Kind != ContractKind.Custom)
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

            ContractKind contractKind;
            if (Enum.TryParse<ContractKind>(name, true, out contractKind))
            {
                switch (contractKind)
                {
                    case ContractKind.Privacy: contract = new PrivacyContract(); break;
                    case ContractKind.Distribution: contract = new DistributionContract(); break;
                    case ContractKind.Exchange: contract = new ExchangeContract(); break;
                    case ContractKind.Governance: contract = new GovernanceContract(); break;
                    case ContractKind.Stake: contract = new StakeContract(); break;
                    case ContractKind.Storage: contract = new StorageContract(); break;
                    case ContractKind.Account: contract = new AccountContract(); break;
                    case ContractKind.Vault: contract = new VaultContract(); break;
                    case ContractKind.Bank: contract = new BankContract(); break;
                    case ContractKind.Apps: contract = new AppsContract(); break;

                    default:
                        throw new ChainException("Could not create contract for: " + contractKind);
                }
            }
            else
            {
                contract = null; // TODO
            }

            var chain = new Chain(this, owner, name, contract, this.logger, parentChain, parentBlock);
            _chains[name] = chain;

            foreach (var plugin in _plugins)
            {
                plugin.OnNewChain(chain);
            }

            return chain;
        }

        public bool ContainsChain(Chain chain)
        {
            if (chain == null)
            {
                return false;
            }

            return _chains.ContainsKey(chain.Name);
        }

        public Chain FindChainByAddress(Address address)
        {
            foreach (var entry in _chains.Values)
            {
                if (entry.Address == address)
                {
                    return entry;
                }
            }

            return null;
        }

        public Chain FindChainByKind(ContractKind kind)
        {
            return FindChainByName(kind.ToString().ToLower());
        }

        public Chain FindChainByName(string name)
        {
            if (_chains.ContainsKey(name))
            {
                return _chains[name];
            }

            return null;
        }

        #endregion

        #region TOKENS
        internal Token CreateToken(Address owner, string symbol, string name, BigInteger maxSupply, int decimals, TokenFlags flags)
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

            var token = new Token(owner, symbol, name, maxSupply, decimals, flags);

            if (symbol == NativeTokenSymbol)
            {
                NativeToken = token;
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

        #region GENESIS
        private Transaction TokenCreateTx(Chain chain, KeyPair owner, string symbol, string name, BigInteger totalSupply, int decimals, TokenFlags flags)
        {
            var script = ScriptUtils.CallContractScript(chain, "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags);

            //var script = ScriptUtils.TokenMintScript(nativeToken.Address, owner.Address, TokenContract.MaxSupply);

            var tx = new Transaction(script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);

            return tx;
        }

        private Transaction TokenMintTx(Chain chain, KeyPair owner, string symbol, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(chain, "MintTokens", owner.Address, symbol, amount);
            var tx = new Transaction(script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);
            return tx;
        }

        private Transaction SideChainCreateTx(Chain chain, KeyPair owner, ContractKind kind)
        {
            var name = kind.ToString();

            var script = ScriptUtils.CallContractScript(chain, "CreateChain", owner.Address, name, RootChain.Name);
            var tx = new Transaction(script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);
            return tx;
        }

        /*        
                private Transaction GenerateDistributionDeployTx(KeyPair owner)
                {
                    var script = ScriptUtils.ContractDeployScript(DistributionContract.DefaultScript, DistributionContract.DefaultABI);
                    var tx = new Transaction(owner.Address, script, 0, 0);
                    tx.Sign(owner);
                    return tx;
                }

                private Transaction GenerateGovernanceDeployTx(KeyPair owner)
                {
                    var script = ScriptUtils.ContractDeployScript(GovernanceContract.DefaultScript, GovernanceContract.DefaultABI);
                    var tx = new Transaction(owner.Address, script, 0, 0);
                    tx.Sign(owner);
                    return tx;
                }

                private Transaction GenerateStakeDeployTx(KeyPair owner)
                {
                    var script = ScriptUtils.ContractDeployScript(StakeContract.DefaultScript, StakeContract.DefaultABI);
                    var tx = new Transaction(owner.Address, script, 0, 0);
                    tx.Sign(owner);
                    return tx;
                }
        */

        public const string NativeTokenSymbol = "SOUL";
        public const string PlatformName = "Phantasma";

        public const string StableTokenSymbol = "ALMA";
        public const string StableTokenName = "Stable Coin";

        public const string TrophyTokenSymbol = "TROPHY";
        public const string TrophyTokenName = "Trophy";

        public const int NativeTokenDecimals = 8;
        public const int StableTokenDecimals = 8;

        public readonly static BigInteger PlatformSupply = TokenUtils.ToBigInteger(93000000, NativeTokenDecimals);

        private Block CreateGenesisBlock(KeyPair owner)
        {
            this.GenesisAddress = owner.Address;

            var transactions = new List<Transaction>();

            transactions.Add(TokenCreateTx(RootChain, owner, NativeTokenSymbol, PlatformName, PlatformSupply, NativeTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible));
            transactions.Add(TokenMintTx(RootChain, owner, NativeTokenSymbol, PlatformSupply));
            transactions.Add(TokenCreateTx(RootChain, owner, StableTokenSymbol, StableTokenName, 0, StableTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible));
            transactions.Add(TokenCreateTx(RootChain, owner, TrophyTokenSymbol, TrophyTokenName, 0, 0, TokenFlags.None));

            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Privacy));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Distribution));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Account));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Stake));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Vault));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Bank));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Apps));

            /*var distTx = GenerateDistributionDeployTx(owner);
            var govTx = GenerateDistributionDeployTx(owner);
            var stakeTx = GenerateStakeDeployTx(owner);*/

            var block = new Block(RootChain, owner.Address, Timestamp.Now, transactions);

            return block;
        }
        #endregion
    }
}
