using System;
using System.Collections.Generic;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        public Chain ParentChain { get; private set; }

        public Address Address { get; private set; }

        private Dictionary<Hash, Transaction> _transactions = new Dictionary<Hash, Transaction>();
        private Dictionary<Hash, Block> _blocks = new Dictionary<Hash, Block>();
        private Dictionary<BigInteger, Block> _blockHeightMap = new Dictionary<BigInteger, Block>();

        public IEnumerable<Block> Blocks => _blocks.Values;

        public uint Height => (uint)_blocks.Count;
        
        public Block lastBlock { get; private set; }

        public SmartContract Contract { get; private set; }

        public readonly Logger Log;

        private List<NativeExecutionContext> _nativeContexts = new List<NativeExecutionContext>();
        public IEnumerable<NativeExecutionContext> NativeContexts => _nativeContexts;

        private Dictionary<Token, Dictionary<Address, BigInteger>> _tokenBalances = new Dictionary<Token, Dictionary<Address, BigInteger>>();

        public bool IsRoot => this.ParentChain == null;

        public Chain(KeyPair owner, SmartContract contract, Logger log = null, Chain parentChain = null)
        {
            Throw.IfNull(owner, "owner required");
            Throw.IfNull(contract, "contract required");

            this.ParentChain = parentChain;
            this.Log = Logger.Init(log);

            var block = CreateGenesisBlock(owner);
            if (!AddBlock(block))
            {
                throw new ChainException("Genesis block failure");
            }

            this.Contract = contract;
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

        private Dictionary<Address, Chain> _childChains = new Dictionary<Address, Chain>();

        public Chain FindChain(Address address)
        {
            if (address == this.Address)
            {
                return this;
            }

            if (this.IsRoot)
            {
                foreach (var childChain in _childChains.Values)
                {
                    var result = childChain.FindChain(address);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }

        public Chain GetRoot()
        {
            var result = this;
            while (result.ParentChain != null)
            {
                result = result.ParentChain;
            }

            return result;
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

        public BigInteger GetTokenBalance(Token token, Address address)
        {
            if (_tokenBalances.ContainsKey(token))
            {
                var balances = _tokenBalances[token];

                if (balances.ContainsKey(address))
                {
                    var balance = balances[address];
                    return balance;
                }
            }

            return 0;

/*            var contract = this.FindContract(token);
            Throw.IfNull(contract, "contract not found");

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Throw.IfNot(contract.ABI.Implements(tokenABI), "invalid contract");

            var balance = (BigInteger)tokenABI["BalanceOf"].Invoke(contract, account);
            return balance;*/
        }
    }
}
