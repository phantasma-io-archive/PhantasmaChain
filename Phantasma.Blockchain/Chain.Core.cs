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
using Phantasma.VM;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Consensus;

namespace Phantasma.Blockchain
{
    public partial class Chain
    {
        #region PRIVATE
        private Dictionary<Hash, Transaction> _transactions = new Dictionary<Hash, Transaction>();
        private Dictionary<Hash, Block> _blockHashes = new Dictionary<Hash, Block>();
        private Dictionary<BigInteger, Block> _blockHeightMap = new Dictionary<BigInteger, Block>();

        private Dictionary<Hash, Block> _transactionBlockMap = new Dictionary<Hash, Block>();

        private Dictionary<Hash, Epoch> _epochMap = new Dictionary<Hash, Epoch>();

        private Dictionary<Hash, StorageChangeSetContext> _blockChangeSets = new Dictionary<Hash, StorageChangeSetContext>();

        private Dictionary<Token, BalanceSheet> _tokenBalances = new Dictionary<Token, BalanceSheet>();
        private Dictionary<Token, OwnershipSheet> _tokenOwnerships = new Dictionary<Token, OwnershipSheet>();

        private Dictionary<Token, Dictionary<BigInteger, TokenContent>> _tokenContents = new Dictionary<Token, Dictionary<BigInteger, TokenContent>>();

        private Dictionary<Token, SupplySheet> _tokenSupplies = new Dictionary<Token, SupplySheet>();

        private Dictionary<string, SmartContract> _contracts = new Dictionary<string, SmartContract>();
        private Dictionary<string, ExecutionContext> _contractContexts = new Dictionary<string, ExecutionContext>();

        private int _level;
        #endregion

        #region PUBLIC
        public static readonly uint InitialHeight = 1;

        public int Level => _level;

        public Chain ParentChain { get; private set; }
        public Block ParentBlock { get; private set; }
        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public Epoch CurrentEpoch { get; private set; }

        public IEnumerable<Block> Blocks => _blockHashes.Values;

        public uint BlockHeight => (uint)_blockHashes.Count;
       
        public Block LastBlock { get; private set; }

        public readonly Logger Log;

        public StorageContext Storage { get; private set; }

        public int TransactionCount => _blockHashes.Sum(entry => entry.Value.TransactionHashes.Count());  //todo move this?
        public bool IsRoot => this.ParentChain == null;
        #endregion

        public Chain(Nexus nexus, string name, IEnumerable<SmartContract> contracts, Logger log = null, Chain parentChain = null, Block parentBlock = null)
        {
            Throw.IfNull(nexus, "nexus required");
            Throw.If(contracts == null || !contracts.Any(), "contracts required");

            if (parentChain != null)
            {
                Throw.IfNull(parentBlock, "parent block required");
                Throw.IfNot(nexus.ContainsChain(parentChain), "invalid chain");
                //Throw.IfNot(parentChain.ContainsBlock(parentBlock), "invalid block"); // TODO should this be required? 
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(name.ToLower());
            var hash = CryptoExtensions.Sha256(bytes);

            this.Address = new Address(hash);

            foreach (var contract in contracts)
            {
                if (this._contracts.ContainsKey(contract.Name))
                {
                    throw new ChainException("Duplicated contract name: " + contract.Name);
                }

                this._contracts[contract.Name] = contract;
                this._contractContexts[contract.Name] = new NativeExecutionContext(contract);
            }

            this.Name = name;
            this.Nexus = nexus;

            this.ParentChain = parentChain;
            this.ParentBlock = parentBlock;

            // TODO support persistence storage
            this.Storage = new MemoryStorageContext();
            this.Log = Logger.Init(log);

            if (parentChain != null)
            {
                parentChain._childChains[name] = this;
                _level = ParentChain.Level + 1;
            }
            else
            {
                _level = 1;
            }
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
        }

        public bool ContainsBlock(Hash hash)
        {
            if (hash == null)
            {
                return false;
            }

            return _blockHashes.ContainsKey(hash);
        }

        public IEnumerable<Transaction> GetBlockTransactions(Block block)
        {
            return block.TransactionHashes.Select(hash => FindTransactionByHash(hash));
        }

        public bool AddBlock(Block block, IEnumerable<Transaction> transactions)
        {
            /*if (CurrentEpoch != null && CurrentEpoch.IsSlashed(Timestamp.Now))
            {
                return false;
            }*/

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

            var inputHashes = new HashSet<Hash>(transactions.Select(x => x.Hash));
            foreach (var hash in block.TransactionHashes)
            {
                if (!inputHashes.Contains(hash))
                {
                    return false;
                }
            }

            var outputHashes = new HashSet<Hash>(block.TransactionHashes);
            foreach (var tx in transactions)
            {
                if (!outputHashes.Contains(tx.Hash))
                {
                    return false;
                }
            }

            foreach (var tx in transactions)
            {
                if (!tx.IsValid(this))
                {
                    return false;
                }
            }

            var changeSet = new StorageChangeSetContext(this.Storage);

            foreach (var  tx in transactions)
            {
                if (!tx.Execute(this, block, changeSet, block.Notify))
                {
                    return false;
                }
            }

            // from here on, the block is accepted
            Log.Message($"{Name} height is now {block.Height}");

            _blockHeightMap[block.Height] = block;
            _blockHashes[block.Hash] = block;
            _blockChangeSets[block.Hash] = changeSet;

            changeSet.Execute();

            if (CurrentEpoch == null)
            {
                GenerateEpoch();
            }

            CurrentEpoch.AddBlockHash(block.Hash);
            CurrentEpoch.UpdateHash();

            LastBlock = block;

            foreach (Transaction tx in transactions)
            {
                _transactions[tx.Hash] = tx;
                _transactionBlockMap[tx.Hash] = block;
            }

            Nexus.PluginTriggerBlock(this, block);

            return true;
        }

        private Dictionary<string, Chain> _childChains = new Dictionary<string, Chain>();
        public IEnumerable<Chain> ChildChains => _childChains.Values;

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

        public bool ContainsTransaction(Hash hash)
        {
            return _transactions.ContainsKey(hash);
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
            Throw.If(!token.Flags.HasFlag(TokenFlags.Fungible), "should be fungible");

            if (_tokenBalances.ContainsKey(token))
            {
                return _tokenBalances[token];
            }

            var sheet = new BalanceSheet();
            _tokenBalances[token] = sheet;
            return sheet;
        }

        internal void InitSupplySheet(Token token, BigInteger maxSupply)
        {
            Throw.If(!token.Flags.HasFlag(TokenFlags.Fungible), "should be fungible");
            Throw.If(!token.IsCapped, "should be capped");
            Throw.If(_tokenSupplies.ContainsKey(token), "supply sheet already created");

            var sheet = new SupplySheet(0, 0, maxSupply);
            _tokenSupplies[token] = sheet;
        }

        internal SupplySheet GetTokenSupplies(Token token)
        {
            Throw.If(!token.Flags.HasFlag(TokenFlags.Fungible), "should be fungible");
            Throw.If(!token.IsCapped, "should be capped");

            if (_tokenSupplies.ContainsKey(token))
            {
                return _tokenSupplies[token];
            }

            Throw.If(this.ParentChain == null, "supply sheet not created");

            var parentSupplies = this.ParentChain.GetTokenSupplies(token);

            var sheet = new SupplySheet(parentSupplies.LocalBalance, 0, token.MaxSupply);
            _tokenSupplies[token] = sheet;
            return sheet;
        }

        public OwnershipSheet GetTokenOwnerships(Token token)
        {
            Throw.If(token.Flags.HasFlag(TokenFlags.Fungible), "cannot be fungible");

            if (_tokenOwnerships.ContainsKey(token))
            {
                return _tokenOwnerships[token];
            }

            var sheet = new OwnershipSheet();
            _tokenOwnerships[token] = sheet;
            return sheet;
        }

        public BigInteger GetTokenBalance(Token token, Address address)
        {
            if (token.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = GetTokenBalances(token);
                return balances.Get(address);
            }
            else
            {
                var ownerships = GetTokenOwnerships(token);
                var items = ownerships.Get(address);
                return items.Count();
            }

            /*            var contract = this.FindContract(token);
                        Throw.IfNull(contract, "contract not found");

                        var tokenABI = Chain.FindABI(NativeABI.Token);
                        Throw.IfNot(contract.ABI.Implements(tokenABI), "invalid contract");

                        var balance = (LargeInteger)tokenABI["BalanceOf"].Invoke(contract, account);
                        return balance;*/
        }

        public IEnumerable<BigInteger> GetOwnedTokens(Token token, Address address)
        {
            var ownership = GetTokenOwnerships(token);
            return ownership.Get(address);
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

        public T FindContract<T>(string contractName) where T: SmartContract
        {
            Throw.IfNullOrEmpty(contractName, nameof(contractName));

            if (_contracts.ContainsKey(contractName))
            {
                return (T)_contracts[contractName];
            }

            return null;
        }

        internal ExecutionContext GetContractContext(SmartContract contract)
        {
            if (_contractContexts.ContainsKey(contract.Name))
            {
                return _contractContexts[contract.Name];
            }

            return null;
        }

        public object InvokeContract(string contractName, string methodName, params object[] args)
        {
            var contract = FindContract<SmartContract>(contractName);
            Throw.IfNull(contract, nameof(contract));

            var script = ScriptUtils.BeginScript().CallContract(contractName, methodName, args).EndScript();
            var changeSet = new StorageChangeSetContext(this.Storage);
            var vm = new RuntimeVM(script, this, null, null, changeSet, true);

            contract.SetRuntimeData(vm);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                throw new ChainException($"Invocation of method '{methodName}' of contract '{contractName}' failed with state: " + state);
            }

            var result = vm.Stack.Pop();

            return result.ToObject();
        }

        #region FEES 
        public BigInteger GetBlockReward(Block block)
        {
            BigInteger total = 0;
            foreach (var hash in block.TransactionHashes)
            {
                var events = block.GetEventsForTransaction(hash);
                foreach (var evt in events)
                {
                    if (evt.Kind == EventKind.GasPayment)
                    {
                        var gasInfo = evt.GetContent<GasEventData>();
                        total += gasInfo.price * gasInfo.amount;
                    }
                }
            }

            return total;
        }

        public BigInteger GetTransactionFee(Transaction tx)
        {
            Throw.IfNull(tx, nameof(tx));
            return GetTransactionFee(tx.Hash);
        }

        public BigInteger GetTransactionFee(Hash hash)
        {
            Throw.IfNull(hash, nameof(hash));

            BigInteger fee = 0;

            var block = FindTransactionBlock(hash);
            Throw.IfNull(block, nameof(block));

            var events = block.GetEventsForTransaction(hash);
            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.GasPayment)
                {
                    var info = evt.GetContent<GasEventData>();
                    fee += info.amount * info.price;
                }
            }

            return fee;
        }
        #endregion

        #region EPOCH
        public bool IsCurrentValidator(Address address)
        {
            if (CurrentEpoch != null)
            {
                return CurrentEpoch.ValidatorAddress == address;
            }

            var firstValidator = Nexus.GetValidatorByIndex(0);
            return address == firstValidator;
        }

        private void GenerateEpoch()
        {
            Address nextValidator;

            uint epochIndex;

            if (CurrentEpoch != null)
            {
                epochIndex = CurrentEpoch.Index + 1;

                var currentIndex = Nexus.GetIndexOfValidator(CurrentEpoch.ValidatorAddress);
                currentIndex++;

                var validatorCount = Nexus.GetValidatorCount();

                if (currentIndex >= validatorCount)
                {
                    currentIndex = 0;
                }

                nextValidator = Nexus.GetValidatorByIndex(currentIndex);
            }
            else
            {
                epochIndex = 0;
                nextValidator = Nexus.GetValidatorByIndex(0);
            }

            var epoch = new Epoch(epochIndex, Timestamp.Now, nextValidator, CurrentEpoch != null ? CurrentEpoch.Hash : Hash.Null);

            CurrentEpoch = epoch;
        }
        #endregion

        #region NFT
        internal BigInteger CreateNFT(Token token, byte[] data)
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

                var content = new TokenContent(data);
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
                        return contents[tokenID];
                    }
                }
            }

            return null;
        }
        #endregion
    }
}
