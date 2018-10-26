using System.Collections.Generic;
using System.Linq;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Storage;
using System;
using Phantasma.VM.Utils;

namespace Phantasma.Blockchain
{
    public struct ChainDiffEntry
    {
        public Block block;
        public StorageChangeSetContext changeSet;
    }
    
    public partial class Chain
    {
        public Chain ParentChain { get; private set; }
        public Block ParentBlock { get; private set; }
        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }
        public Address Owner { get; private set; }

        private Dictionary<Hash, Transaction> _transactions = new Dictionary<Hash, Transaction>();
        private Dictionary<Hash, Block> _blockHashes = new Dictionary<Hash, Block>();
        private Dictionary<BigInteger, Block> _blockHeightMap = new Dictionary<BigInteger, Block>();

        private Dictionary<Hash, Block> _transactionBlockMap = new Dictionary<Hash, Block>();

        private Dictionary<Hash, StorageChangeSetContext> _blockChangeSets = new Dictionary<Hash, StorageChangeSetContext>();

        public IEnumerable<Block> Blocks => _blockHashes.Values;

        public uint BlockHeight => (uint)_blockHashes.Count;
        
        public Block LastBlock { get; private set; }

        public SmartContract Contract { get; private set; }

        public readonly Logger Log;

        public NativeExecutionContext ExecutionContext { get; private set; }

        private Dictionary<Token, BalanceSheet> _tokenBalances = new Dictionary<Token, BalanceSheet>();

        public StorageContext Storage { get; private set; }

        public int TransactionCount => _blockHashes.Sum(c => c.Value.Transactions.Count());  //todo move this?

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

            // TODO support persistence storage
            this.Storage = new MemoryStorageContext();

            this.Log = Logger.Init(log);
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
        }

        public bool ContainsBlock(Block block)
        {
            if (block == null)
            {
                return false;
            }

            return _blockHashes.ContainsKey(block.Hash);
        }

        public bool AddBlock(Block block)
        {
            if (LastBlock != null)
            {
                if (LastBlock.Height != block.Height - 1)
                {
                    return false;
                }

                if (block.PreviousHash != LastBlock.Hash)
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

            var changeSet = new StorageChangeSetContext(this.Storage);

            foreach (Transaction tx in block.Transactions)
            {
                if (!tx.Execute(this, block, changeSet, block.Notify))
                {
                    return false;
                }
            }

            // from here on, the block is accepted
            Log.Message($"Increased chain height to {block.Height}");

            _blockHeightMap[block.Height] = block;
            _blockHashes[block.Hash] = block;
            _blockChangeSets[block.Hash] = changeSet;

            LastBlock = block;

            foreach (Transaction tx in block.Transactions)
            {
                tx.SetBlock(block);
                _transactions[tx.Hash] = tx;
                _transactionBlockMap[tx.Hash] = block;
            }

            Nexus.PluginTriggerBlock(this, block);

            return true;
        }

        private Dictionary<Address, Chain> _childChains = new Dictionary<Address, Chain>();

        public Chain FindChildChain(Address address)
        {
            Throw.If(address == Address.Null, "invalid address");

            foreach (var childChain in _childChains.Values)
            {
                if (childChain.Address == address)
                {
                    return childChain;
                }
            }

            foreach (var childChain in _childChains.Values)
            {
                var result = childChain.FindChildChain(address);
                if (result != null)
                {
                    return result;
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

        public Transaction FindTransactionByHash(Hash hash)
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

        public Block FindBlockByHash(Hash hash)
        {
            return _blockHashes.ContainsKey(hash) ? _blockHashes[hash] : null;
        }

        public Block FindBlockByHeight(BigInteger height)
        {
            return _blockHeightMap.ContainsKey(height) ? _blockHeightMap[height] : null;
        }

        public BalanceSheet GetTokenBalances(Token token)
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

            if (name.Length < 3 || name.Length >= 20)
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

        /// <summary>
        /// Deletes all blocks starting at the specified hash.
        /// </summary>
        public void DeleteBlocks(Hash targetHash)
        {
            var targetBlock = FindBlockByHash(targetHash);
            Throw.IfNull(targetBlock, nameof(targetBlock));

            var currentBlock = this.LastBlock;
            while (true)
            {
                Throw.IfNull(currentBlock, nameof(currentBlock));

                var changeSet = _blockChangeSets[currentBlock.Hash];
                changeSet.Undo();

                _blockChangeSets.Remove(currentBlock.Hash);
                _blockHeightMap.Remove(currentBlock.Height);
                _blockHashes.Remove(currentBlock.Hash);

                currentBlock = FindBlockByHash(currentBlock.PreviousHash);
                this.LastBlock = currentBlock;

                if (currentBlock.PreviousHash == targetHash)
                {
                    break;
                }
            }
        }

        internal object InvokeContract(string methodName, params object[] args)
        {
            var script = ScriptUtils.CallContractScript(this, methodName, args);
            var changeSet = new StorageChangeSetContext(this.Storage);
            var vm = new RuntimeVM(script, this, null, null, changeSet);
            Contract.SetRuntimeData(vm);

            vm.Execute();

            var result = vm.stack.Pop();

            return result.ToObject();
        }

        public void MergeBlocks(IEnumerable<ChainDiffEntry> entries)
        {
            Throw.IfNot(entries.Any(), "empty entries");

            var firstBlockHeight = entries.First().block.Height;

            var expectedLastHeight = firstBlockHeight + entries.Count();
            Throw.If(expectedLastHeight <= this.BlockHeight, "short chain");

            var currentBlockHeight = firstBlockHeight;

            foreach (var entry in entries)
            {
                if (currentBlockHeight <= this.BlockHeight)
                {
                    var localBlock = FindBlockByHeight(currentBlockHeight);

                    if (entry.block.Hash != localBlock.Hash)
                    {
                        DeleteBlocks(localBlock.Hash);
                        var diffHeight = currentBlockHeight - firstBlockHeight;
                        MergeBlocks(entries.Skip((int)diffHeight));
                        return;
                    }
                }
                else
                {
                    this.AddBlock(entry.block);
                }

                currentBlockHeight++;
            }
        }
    }
}
