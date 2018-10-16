using System;
using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts.Native;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        public Chain ParentChain { get; private set; }
        public Block ParentBlock { get; private set; }
        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }
        public Address Owner { get; private set; }

        private Dictionary<Hash, Transaction> _transactions = new Dictionary<Hash, Transaction>();
        private Dictionary<Hash, Block> _blocks = new Dictionary<Hash, Block>();
        private Dictionary<BigInteger, Block> _blockHeightMap = new Dictionary<BigInteger, Block>();

        private Dictionary<Hash, Block> _transactionBlockMap = new Dictionary<Hash, Block>();

        public IEnumerable<Block> Blocks => _blocks.Values;

        public uint Height => (uint)_blocks.Count;
        
        public Block lastBlock { get; private set; }

        public SmartContract Contract { get; private set; }

        public readonly Logger Log;

        public NativeExecutionContext ExecutionContext { get; private set; }

        private Dictionary<Token, BalanceSheet> _tokenBalances = new Dictionary<Token, BalanceSheet>();


        public int TransactionCount => _blocks.Sum(c => c.Value.Transactions.Count());  //todo move this?

        public bool IsRoot => this.ParentChain == null;

        public Chain(Nexus nexus, Address owner, string name, SmartContract contract, Logger log = null, Chain parentChain = null, Block parentBlock = null)
        {
            Throw.IfNull(owner, "owner required");
            Throw.IfNull(contract, "contract required");
            Throw.IfNull(nexus, "nexus required");

            if (parentChain != null)
            {
                Throw.IfNull(parentBlock, "parent block required");
                Throw.IfNot(nexus.ContainsChain(parentChain), "invalid chain");
                //Throw.IfNot(parentChain.ContainsBlock(parentBlock), "invalid block"); // TODO should this be required? 
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(name.ToLower());
            var hash = CryptoExtensions.Sha256(bytes);

            this.Address = new Address(hash);

            this.Name = name;
            this.Contract = contract;
            this.Owner = owner;
            this.Nexus = nexus;

            this.ParentChain = parentChain;
            this.ParentBlock = parentBlock;

            if (contract is NativeContract)
            {
                this.ExecutionContext = new NativeExecutionContext((NativeContract)contract);
            }
            else
            {
                this.ExecutionContext = null;
            }

            this.Log = Logger.Init(log);
        }

        public bool ContainsBlock(Block block)
        {
            if (block == null)
            {
                return false;
            }

            return _blocks.ContainsKey(block.Hash);
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
                if (!tx.Execute(this, block, block.Notify))
                {
                    return false;
                }
            }

            // from here on, the block is accepted
            Log.Message($"Increased chain height to {block.Height}");

            _blockHeightMap[block.Height] = block;
            _blocks[block.Hash] = block;
            lastBlock = block;

            foreach (Transaction tx in block.Transactions)
            {
                _transactions[tx.Hash] = tx;
                _transactionBlockMap[tx.Hash] = block;
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

        public Block FindTransactionBlock(Transaction tx)
        {
            return FindTransactionBlock(tx.Hash);
        }

        public Block FindTransactionBlock(Hash hash)
        {
            return _transactionBlockMap.ContainsKey(hash) ? _transactionBlockMap[hash] : null;
        }

        public Block FindBlock(Hash hash)
        {
            return _blocks.ContainsKey(hash) ? _blocks[hash] : null;
        }

        public Block FindBlock(BigInteger height)
        {
            return _blockHeightMap.ContainsKey(height) ? _blockHeightMap[height] : null;
        }

        internal BalanceSheet GetTokenBalances(Token token)
        {
            if (_tokenBalances.ContainsKey(token))
            {
                return _tokenBalances[token];
            }

            var sheet = new BalanceSheet();
            _tokenBalances[token] = sheet;
            return sheet;
        }

        public BigInteger GetTokenBalance(Token token, Address address)
        {
            var balances = GetTokenBalances(token);
            return balances.Get(address);

/*            var contract = this.FindContract(token);
            Throw.IfNull(contract, "contract not found");

            var tokenABI = Chain.FindABI(NativeABI.Token);
            Throw.IfNot(contract.ABI.Implements(tokenABI), "invalid contract");

            var balance = (BigInteger)tokenABI["BalanceOf"].Invoke(contract, account);
            return balance;*/
        }

        public static bool ValidateName(string name)
        {
            if (name == null)
            {
                return false;
            }

            if (name.Length <= 4 || name.Length >= 20)
            {
                return false;
            }

            int index = 0;
            while (index < name.Length)
            {
                var c = (int)name[index];
                index++;

                if (c >= 97 && c <= 122) continue; // lowercase allowed
                if (c == 95) continue; // underscore allowed
                if (c >= 48 && c <= 57) continue; // numbers allowed

                return false;
            }

            return true;
        }

    }
}
