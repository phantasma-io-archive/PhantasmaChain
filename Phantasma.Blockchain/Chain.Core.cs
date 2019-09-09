using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.VM.Utils;
using Phantasma.Core.Types;
using Phantasma.Blockchain.Contracts;
using Phantasma.Cryptography;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain
{
    public class BlockGenerationException : Exception
    {
        public BlockGenerationException(string msg) : base(msg)
        {

        }
    }

    public class InvalidTransactionException : Exception
    {
        public readonly Hash Hash;

        public InvalidTransactionException(Hash hash, string msg) : base(msg)
        {
            this.Hash = hash;
        }
    }

    public partial class Chain 
    {
        #region PRIVATE
        private KeyValueStore<Hash, Transaction> _transactions;
        private KeyValueStore<Hash, Block> _blocks;
        private KeyValueStore<Hash, Hash> _transactionBlockMap;
        private KeyValueStore<Hash, Epoch> _epochMap;
        private KeyValueStore<string, bool> _contracts;
        private KeyValueStore<BigInteger, Hash> _blockHeightMap;

        private Dictionary<Hash, StorageChangeSetContext> _blockChangeSets = new Dictionary<Hash, StorageChangeSetContext>();

        private Dictionary<string, ExecutionContext> _contractContexts = new Dictionary<string, ExecutionContext>();
        #endregion

        #region PUBLIC
        public static readonly uint InitialHeight = 1;

        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public Epoch CurrentEpoch { get; private set; }

        public uint BlockHeight => (uint)_blocks.Count;

        public Block LastBlock => FindBlockByHeight(BlockHeight);

        public readonly Logger Log;

        public StorageContext Storage { get; private set; }

        public uint TransactionCount => (uint)_transactions.Count;

        public bool IsRoot => this.Name == Nexus.RootChainName;
        #endregion

        public Chain(Nexus nexus, string name, Logger log = null)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;

            var bytes = System.Text.Encoding.UTF8.GetBytes(name.ToLower());
            var hash = CryptoExtensions.SHA256(bytes);

            this.Address = new Address(hash);

            // init stores
            _transactions = new KeyValueStore<Hash, Transaction>(Nexus.CreateKeyStoreAdapter(this.Address, "txs"));
            _blocks = new KeyValueStore<Hash, Block>(Nexus.CreateKeyStoreAdapter(this.Address, "blocks"));
            _transactionBlockMap = new KeyValueStore<Hash, Hash>(Nexus.CreateKeyStoreAdapter(this.Address, "txbk"));
            _epochMap = new KeyValueStore<Hash, Epoch>(Nexus.CreateKeyStoreAdapter(this.Address, "epoch"));
            _contracts = new KeyValueStore<string, bool>(Nexus.CreateKeyStoreAdapter(this.Address, "contracts"));
            _blockHeightMap = new KeyValueStore<BigInteger, Hash>(Nexus.CreateKeyStoreAdapter(this.Address, "heights"));

            this.Storage = new KeyStoreStorage(Nexus.CreateKeyStoreAdapter( this.Address, "data"));

            this.Log = Logger.Init(log);
        }

        public string[] GetContracts()
        {
            var list = new string[(int)_contracts.Count];
            int index = 0;
            _contracts.Visit((contract, _) =>
            {
                list[index] = contract;
                index++;
            });
            return list;
        }

        public bool HasContract(string contractName)
        {
            return _contracts.ContainsKey(contractName);
        }

        internal void DeployContracts(HashSet<string> contractNames)
        {
            Throw.If(contractNames == null || !contractNames.Any(), "contracts required");
            Throw.If(!contractNames.Contains(Nexus.GasContractName), "gas contract required");
            Throw.If(!contractNames.Contains(Nexus.TokenContractName), "token contract required");

            foreach (var contractName in contractNames)
            {
                this._contracts[contractName] = true;
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

            return _blocks.ContainsKey(hash);
        }

        public IEnumerable<Transaction> GetBlockTransactions(Block block)
        {
            return block.TransactionHashes.Select(hash => FindTransactionByHash(hash));
        }

        public void AddBlock(Block block, IEnumerable<Transaction> transactions)
        {
            /*if (CurrentEpoch != null && CurrentEpoch.IsSlashed(Timestamp.Now))
            {
                return false;
            }*/

            if (LastBlock != null)
            {
                if (LastBlock.Height != block.Height - 1)
                {
                    throw new BlockGenerationException($"height of block should be {LastBlock.Height + 1}");
                }

                if (block.PreviousHash != LastBlock.Hash)
                {
                    throw new BlockGenerationException($"previous hash should be {LastBlock.PreviousHash}");
                }
            }

            var inputHashes = new HashSet<Hash>(transactions.Select(x => x.Hash));
            foreach (var hash in block.TransactionHashes)
            {
                if (!inputHashes.Contains(hash))
                {
                    throw new BlockGenerationException($"missing in inputs transaction with hash {hash}");
                }
            }

            var outputHashes = new HashSet<Hash>(block.TransactionHashes);
            foreach (var tx in transactions)
            {
                if (!outputHashes.Contains(tx.Hash))
                {
                    throw new BlockGenerationException($"missing in outputs transaction with hash {tx.Hash}");
                }
            }

            foreach (var tx in transactions)
            {
                if (!tx.IsValid(this))
                {
                    throw new InvalidTransactionException(tx.Hash, $"invalid transaction with hash {tx.Hash}");
                }
            }

            var changeSet = new StorageChangeSetContext(this.Storage);

            var targetEpoch = CurrentEpoch != null ? CurrentEpoch : GenerateEpoch();

            var oracle = Nexus.CreateOracleReader();

            foreach (var tx in transactions)
            {
                byte[] result;
                try
                {
                    if (tx.Execute(this, targetEpoch, block.Timestamp, changeSet, block.Notify, oracle, out result))
                    {
                        if (result != null)
                        {
                            block.SetResultForHash(tx.Hash, result);
                        }
                    }
                    else
                    {
                        throw new InvalidTransactionException(tx.Hash, $"execution failed");
                    }
                }
                catch (Exception e)
                {
                    if (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    throw new InvalidTransactionException(tx.Hash, e.Message);
                }
            }

            block.MergeOracle(oracle);

            // from here on, the block is accepted
            _blockHeightMap[block.Height] = block.Hash;
            _blocks[block.Hash] = block;
            _blockChangeSets[block.Hash] = changeSet;

            changeSet.Execute();

            CurrentEpoch = targetEpoch;
            CurrentEpoch.AddBlockHash(block.Hash);
            CurrentEpoch.UpdateHash();

            Dictionary<string, BigInteger> synchMap = null;

            foreach (Transaction tx in transactions)
            {
                _transactions[tx.Hash] = tx;
                _transactionBlockMap[tx.Hash] = block.Hash;

                var evts = block.GetEventsForTransaction(tx.Hash);
                foreach (var evt in evts)
                {
                    if (evt.Kind == EventKind.TokenMint || evt.Kind == EventKind.TokenBurn || evt.Kind == EventKind.TokenReceive || evt.Kind == EventKind.TokenSend)
                    {
                        if (synchMap == null)
                        {
                            synchMap = new Dictionary<string, BigInteger>();

                            var eventData = evt.GetContent<TokenEventData>();
                            var balance = synchMap.ContainsKey(eventData.symbol) ? synchMap[eventData.symbol] : 0;

                            if (evt.Kind == EventKind.TokenBurn || evt.Kind == EventKind.TokenSend)
                            {
                                balance -= eventData.value;
                            }
                            else
                            {
                                balance += eventData.value;
                            }

                            synchMap[eventData.symbol] = balance;
                        }
                    }
                }
            }

            if (synchMap != null)
            {
                SynchronizeSupplies(synchMap);
            }

            Nexus.PluginTriggerBlock(this, block);
        }

        private void SynchronizeSupplies(Dictionary<string, BigInteger> synchMap)
        {
            foreach (var entry in synchMap)
            {
                var symbol = entry.Key;
                var balance = entry.Value;

                if (balance == 0) // usually will happen due to token receive and send in same transaction
                {
                    continue;
                }

                var token = Nexus.GetTokenInfo(symbol);

                if (!token.IsFungible)
                {
                    // TODO support this
                    continue;
                }

                var parentName = Nexus.GetParentChainByName(this.Name);
                var parentChain = Nexus.FindChainByName(parentName);
                if (parentChain != null)
                {
                    var parentSupplies = new SupplySheet(symbol, parentChain, Nexus);
                    parentSupplies.Synch(parentChain.Storage, this.Name, balance);
                }

                var childrenNames = this.Nexus.GetChildChainsByName(this.Name);
                foreach (var childName in childrenNames)
                {
                    var childChain = Nexus.FindChainByName(childName);
                    var childSupplies = new SupplySheet(symbol, childChain, Nexus);
                    childSupplies.Synch(childChain.Storage, this.Name, balance);
                }
            }

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
            if (_transactionBlockMap.ContainsKey(hash))
            {
                var blockHash = _transactionBlockMap[hash];
                return FindBlockByHash(blockHash);
            }

            return null;
        }

        public Block FindBlockByHash(Hash hash)
        {
            return _blocks.ContainsKey(hash) ? _blocks[hash] : null;
        }

        public Block FindBlockByHeight(BigInteger height)
        {
            if (_blockHeightMap.ContainsKey(height))
            {
                var hash = _blockHeightMap[height];
                return FindBlockByHash(hash);
            }

            return null; // TODO Should thrown an exception?
        }

        // NOTE should never be used directly from a contract!
        public BigInteger GetTokenBalance(string tokenSymbol, Address address)
        {
            var tokenInfo = Nexus.GetTokenInfo(tokenSymbol);
            if (tokenInfo.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = new BalanceSheet(tokenSymbol);
                return balances.Get(Storage, address);
            }
            else
            {
                var ownerships = new OwnershipSheet(tokenSymbol);
                var items = ownerships.Get(this.Storage, address);
                return items.Count();
            }
        }

        // NOTE this only works if the token is curently on this chain
        public Address GetTokenOwner(string tokenSymbol, BigInteger tokenID)
        {
            var tokenInfo = Nexus.GetTokenInfo(tokenSymbol);
            Throw.If(tokenInfo.IsFungible, "non fungible required");

            var ownerships = new OwnershipSheet(tokenSymbol);
            return ownerships.GetOwner(this.Storage, tokenID);
        }

        // NOTE this lists only nfts owned in this chain
        public IEnumerable<BigInteger> GetOwnedTokens(string tokenSymbol, Address address)
        {
            var ownership = new OwnershipSheet(tokenSymbol);
            return ownership.Get(this.Storage, address);
        }

        // TODO move this along with other name validations to a common file
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
                _blocks.Remove(currentBlock.Hash);

                currentBlock = FindBlockByHash(currentBlock.PreviousHash);

                if (currentBlock.PreviousHash == targetHash)
                {
                    break;
                }
            }
        }

        internal ExecutionContext GetContractContext(SmartContract contract)
        {
            if (_contractContexts.ContainsKey(contract.Name))
            {
                return _contractContexts[contract.Name];
            }

            if (HasContract(contract.Name))
            {
                // TODO this needs to suport non-native contexts too..
                var context = new NativeExecutionContext(contract);
                this._contractContexts[contract.Name] = context;
                return context;
            }

            return null;
        }

        public object InvokeContract(string contractName, string methodName, Timestamp time, params object[] args)
        {
            var contract = Nexus.FindContract(contractName);
            Throw.IfNull(contract, nameof(contract));

            var script = ScriptUtils.BeginScript().CallContract(contractName, methodName, args).EndScript();
            
            var result = InvokeScript(script, time);

            if (result == null)
            {
                throw new ChainException($"Invocation of method '{methodName}' of contract '{contractName}' failed");
            }

            return result.ToObject(); // TODO remove ToObject and let the callers convert as they want
        }

        public object InvokeContract(string contractName, string methodName, params object[] args)
        {
            return InvokeContract(contractName, methodName, Timestamp.Now, args);
        }

        public VMObject InvokeScript(byte[] script)
        {
            return InvokeScript(script, Timestamp.Now);
        }

        public VMObject InvokeScript(byte[] script, Timestamp time)
        {
            var oracle = Nexus.CreateOracleReader();
            var changeSet = new StorageChangeSetContext(this.Storage);
            var vm = new RuntimeVM(script, this, null,  time, null, changeSet, oracle, true);

            var state = vm.Execute();

            if (state != ExecutionState.Halt)
            {
                return null;
            }

            if (vm.Stack.Count == 0)
            {
                throw new ChainException($"No result, vm stack is empty");
            }

            var result = vm.Stack.Pop();

            return result;
        }

        public BigInteger GenerateUID(StorageContext storage)
        {
            var key = Encoding.ASCII.GetBytes("_uid");

            var lastID = storage.Has(key) ? storage.Get<BigInteger>(key) : 0;

            lastID++;
            storage.Put<BigInteger>(key, lastID);

            return lastID;
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

        private Epoch GenerateEpoch()
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
            if (BlockHeight == 0)
            {
                epochIndex = 0;
                nextValidator = Nexus.GenesisAddress;
            }
            else
            {
                epochIndex = 0;
                nextValidator = Nexus.GetValidatorByIndex(0);
            }

            var epoch = new Epoch(epochIndex, Timestamp.Now, nextValidator, CurrentEpoch != null ? CurrentEpoch.Hash : Hash.Null);
            return epoch;
        }
        #endregion

    }
}
