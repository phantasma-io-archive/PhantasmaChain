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
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Tests;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain
{
    public class Nexus
    {
        public string Name { get; private set; }

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
        public Nexus(string name, KeyPair owner, Logger logger = null)
        {
            this.logger = logger;
            this.Name = name;

            this.RootChain = new Chain(this, owner.Address, "main", new NexusContract(), logger, null);
            _chains[RootChain.Name] = RootChain;

            if (!CreateGenesisBlock(owner))
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

                var txs = chain.GetBlockTransactions(block);
                foreach (var tx in txs)
                {
                    plugin.OnNewTransaction(chain, block, (Transaction) tx);
                }
            }
        }

        public Chain FindChainForBlock(Block block)
        {
            return FindChainForBlock(block.Hash);
        }

        public Chain FindChainForBlock(Hash hash)
        {
            foreach (var chain in _chains.Values)
            {
                if (chain.ContainsBlock(hash))
                {
                    return chain;
                }
            }

            return null;
        }

        public Block FindBlockForTransaction(Transaction tx)
        {
            return FindBlockForHash(tx.Hash);
        }

        public Block FindBlockForHash(Hash hash)
        {
            foreach (var chain in _chains.Values)
            {
                if (chain.ContainsTransaction(hash))
                {
                    return chain.FindTransactionBlock(hash);
                }
            }

            return null;
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
                var sb = new ScriptBuilder();
                contract = new CustomContract(sb.ToScript(), null); // TODO
            }

            var chain = new Chain(this, owner, name, contract, this.logger, parentChain, parentBlock);

            lock (_chains)
            {
                _chains[name] = chain;

                foreach (var plugin in _plugins)
                {
                    plugin.OnNewChain(chain);
                }
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

        public Chain FindChainByKind(ContractKind kind)
        {
            return FindChainByName(kind.ToString().ToLower());
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
            var script = ScriptUtils.CallContractScript(chain.Address, "CreateToken", owner.Address, symbol, name, totalSupply, decimals, flags);

            //var script = ScriptUtils.TokenMintScript(nativeToken.Address, owner.Address, TokenContract.MaxSupply);

            var tx = new Transaction(this.Name, script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);

            return tx;
        }

        private Transaction TokenMintTx(Chain chain, KeyPair owner, string symbol, BigInteger amount)
        {
            var script = ScriptUtils.CallContractScript(chain.Address, "MintTokens", owner.Address, symbol, amount);
            var tx = new Transaction(this.Name, script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);
            return tx;
        }

        private Transaction SideChainCreateTx(Chain chain, KeyPair owner, ContractKind kind)
        {
            var name = kind.ToString();

            var script = ScriptUtils.CallContractScript(chain.Address, "CreateChain", owner.Address, name, RootChain.Name);
            var tx = new Transaction(this.Name, script, 0, 0, Timestamp.Now + TimeSpan.FromDays(300), 0);
            tx.Sign(owner);
            return tx;
        }

        public const string NativeTokenSymbol = "SOUL";
        public const string PlatformName = "Phantasma";

        public const string StableTokenSymbol = "ALMA";
        public const string StableTokenName = "Stable Coin";

        public const int NativeTokenDecimals = 8;
        public const int StableTokenDecimals = 8;

        public readonly static BigInteger PlatformSupply = TokenUtils.ToBigInteger(91136374, NativeTokenDecimals);

        private bool CreateGenesisBlock(KeyPair owner)
        {
            if (this.NativeToken != null)
            {
                return false;
            }

            this.GenesisAddress = owner.Address;

            var transactions = new List<Transaction>();

            transactions.Add(TokenCreateTx(RootChain, owner, NativeTokenSymbol, PlatformName, PlatformSupply, NativeTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Finite | TokenFlags.Divisible));
            transactions.Add(TokenMintTx(RootChain, owner, NativeTokenSymbol, PlatformSupply));
            transactions.Add(TokenCreateTx(RootChain, owner, StableTokenSymbol, StableTokenName, 0, StableTokenDecimals, TokenFlags.Fungible | TokenFlags.Transferable | TokenFlags.Divisible));

            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Privacy));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Distribution));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Account));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Stake));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Vault));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Bank));
            transactions.Add(SideChainCreateTx(RootChain, owner, ContractKind.Apps));

            var genesisMessage = Encoding.UTF8.GetBytes("SOUL genesis");
            var block = new Block(Chain.InitialHeight, RootChain.Address, owner.Address, Timestamp.Now, transactions.Select(tx => tx.Hash), Hash.Null, genesisMessage);

            return RootChain.AddBlock(block, transactions);
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
                    return (int)(chain.LastBlock.Height - block.Height);
                }
            }

            return 0;
        }
        #endregion
    }
}
