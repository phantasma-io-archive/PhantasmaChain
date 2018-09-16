using System;
using System.Collections.Generic;
using System.Reflection;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        public readonly Chain Root;

        public Address Address { get; private set; }

        private Dictionary<Hash, Transaction> _transactions = new Dictionary<Hash, Transaction>();
        private Dictionary<Hash, Block> _blocks = new Dictionary<Hash, Block>();
        private Dictionary<BigInteger, Block> _blockHeightMap = new Dictionary<BigInteger, Block>();
        private Dictionary<Address, SmartContract> _contracts = new Dictionary<Address, SmartContract>();
        private TrieNode _contractLookup = new TrieNode();

        public IEnumerable<Block> Blocks => _blocks.Values;

        public uint Height => (uint)_blocks.Count;
        
        public Block lastBlock { get; private set; }

        public readonly Logger Log;

        private List<NativeExecutionContext> _nativeContexts = new List<NativeExecutionContext>();
        public IEnumerable<NativeExecutionContext> NativeContexts => _nativeContexts;

        public bool IsRoot => this.Root == this;

        public Chain(KeyPair owner, Logger log = null, Chain rootChain = null)
        {
            if (rootChain == null)
            {
                this.Root = this;
            }
            else
            {
                Throw.If(!rootChain.IsRoot, "not a root chain");
                this.Root = rootChain;
            }

            this.Log = Logger.Init(log);

            var list = Enum.GetValues(typeof(NativeContractKind));
            foreach (NativeContractKind kind in list)
            {
                var contract = Chain.GetNativeContract(kind);
                if (contract != null)
                {
                    var context = new NativeExecutionContext(contract);
                    _nativeContexts.Add(context);
                }
            }

            var block = CreateGenesisBlock(owner);
            if (!AddBlock(block))
            {
                throw new ChainException("Genesis block failure");
            }

            this.Address = new Address(block.Hash.ToByteArray());
        }

        public bool AddBlock(Block block)
        {
            if (lastBlock != null)
            {
                if (lastBlock.Height != block.Height - 1)
                {
                    return false;
                }

                if (block.PreviousHash != lastBlock.Hash)
                {
                    return false;
                }
            }

            foreach (Transaction tx in block.Transactions)
            {
                if (!tx.IsValid(this))
                {
                    return false;
                }
            }

            foreach (Transaction tx in block.Transactions)
            {
                if (!tx.Execute(this, block.Notify))
                {
                    return false;
                }
            }

            // from here on, the block is accepted
            Log.Message($"Increased chain height to {block.Height}");

            _blocks[block.Height] = block;
            lastBlock = block;

            foreach (Transaction tx in block.Transactions)
            {
                _transactions[tx.Hash] = tx;
            }

            return true;
        }

        public bool HasContract(Address address)
        {
            return _contracts.ContainsKey(address);
        }

        public SmartContract FindContract(NativeContractKind kind)
        {
            return GetNativeContract(kind);
        }

        public SmartContract FindContract(Address address)
        {
            if (_contracts.ContainsKey(address))
            {
                return _contracts[address];
            }

            return null;
        }

        private Dictionary<Address, StorageContext> _storages = new Dictionary<Address, StorageContext>();

        public StorageContext FindStorage(Address address)
        {
            if (_storages.ContainsKey(address))
            {
                return _storages[address];
            }

            var storage = new StorageContext();
            _storages[address] = storage;
            return storage;
        }


/*        public Contract GetOrCreateAccount(byte[] publicKey)
        {
            var account = FindContract(publicKey);

            if (account == null)
            {
                account = new AccountContract(this, publicKey);
                _contracts[publicKey] = account;
            }

            return account;
        }*/

        // returns public key of the specified name if existing
        public byte[] LookUpContract(string name)
        {
            var pubKey = _contractLookup.Find(name);
            return pubKey;
        }

        private static Dictionary<NativeContractKind, NativeContract> _nativeContracts = null;

        public static NativeContract GetNativeContract(NativeContractKind kind)
        {
            if (_nativeContracts == null)
            {
                _nativeContracts = new Dictionary<NativeContractKind, NativeContract>();

                var assembly = Assembly.GetExecutingAssembly();
                var types = assembly.GetTypes();

                foreach (Type type in types)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(NativeContract)))
                    {
                        var contract = (NativeContract) Activator.CreateInstance(type);
                        _nativeContracts[contract.Kind] = contract;
                    }
                }
            }

            if (_nativeContracts.ContainsKey(kind))
            {
                return _nativeContracts[kind];

            }

            return null;
        }

        private Dictionary<Address, Chain> _chains = new Dictionary<Address, Chain>();

        public Chain FindChain(Address address)
        {
            if (this.IsRoot)
            {
                return _chains.ContainsKey(address) ? _chains[address] : null;
            }

            return this.Root.FindChain(address);
        }

        public Transaction FindTransaction(Hash hash)
        {
            return _transactions.ContainsKey(hash) ? _transactions[hash] : null;
        }

        public Block FindBlock(Hash hash)
        {
            return _blocks.ContainsKey(hash) ? _blocks[hash] : null;
        }

        public Block FindBlock(BigInteger height)
        {
            return _blockHeightMap.ContainsKey(height) ? _blockHeightMap[height] : null;
        }

        public BigInteger GetTokenBalance(Address token, Address account)
        {
            var contract = this.FindContract(token);
            Throw.IfNull(contract, "contract not found");

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Throw.IfNot(contract.ABI.Implements(tokenABI), "invalid contract");

            var balance = (BigInteger)tokenABI["BalanceOf"].Invoke(contract, account);
            return balance;
        }
    }
}
