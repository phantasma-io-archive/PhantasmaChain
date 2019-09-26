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
using Phantasma.Domain;
using Phantasma.Core.Utils;

namespace Phantasma.Blockchain
{
    public partial class Chain 
    {
        #region PRIVATE
        private KeyValueStore<Hash, Transaction> _transactions;
        private KeyValueStore<Hash, Block> _blocks;
        private KeyValueStore<Hash, Hash> _transactionBlockMap;
        private KeyValueStore<BigInteger, Hash> _blockHeightMap;

        private Dictionary<Hash, StorageChangeSetContext> _blockChangeSets = new Dictionary<Hash, StorageChangeSetContext>();

        #endregion

        #region PUBLIC
        public static readonly uint InitialHeight = 1;

        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public BigInteger BlockHeight => _blocks.Count;

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

            this.Address = Address.FromHash(this.Name);

            // init stores
            _transactions = new KeyValueStore<Hash, Transaction>(Nexus.GetChainStorage(this.Name, ChainStorageShard.Transactions));
            _blocks = new KeyValueStore<Hash, Block>(Nexus.GetChainStorage(this.Name, ChainStorageShard.Blocks));
            _transactionBlockMap = new KeyValueStore<Hash, Hash>(Nexus.GetChainStorage(this.Name, ChainStorageShard.TxBlockMap));
            _blockHeightMap = new KeyValueStore<BigInteger, Hash>(Nexus.GetChainStorage(this.Name, ChainStorageShard.Heights));

            this.Storage = new KeyStoreStorage(Nexus.GetChainStorage(this.Name, ChainStorageShard.Data));

            this.Log = Logger.Init(log);
        }

        public string[] GetContracts()
        {
            // TODO improve this
            return new string[] { Nexus.GasContractName, Nexus.BlockContractName, Nexus.TokenContractName };
            /*var list = new string[(int)_contracts.Count];
            int index = 0;
            _contracts.Visit((contract, _) =>
            {
                list[index] = contract;
                index++;
            });
            return list;*/
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

        public void BakeBlock(ref Block block, ref List<Transaction> transactions, BigInteger minimumFee, KeyPair validator, Timestamp time)
        {
            if (transactions.Count <= 0)
            {
                throw new ChainException("not enough transactions in block");
            }

            byte[] script;

            script = ScriptUtils.BeginScript().CallContract("block", "OpenBlock", validator.Address).EndScript();
            var firstTx = new Transaction(Nexus.Name, this.Name, script, new Timestamp(time.Value + 100));
            firstTx.Sign(validator);

            script = ScriptUtils.BeginScript().CallContract("block", "CloseBlock", validator.Address).EndScript();
            var lastTx = new Transaction(Nexus.Name, this.Name, script, new Timestamp(time.Value + 100));
            lastTx.Sign(validator);

            var hashes = new List<Hash>();
            hashes.Add(firstTx.Hash);
            hashes.AddRange(block.TransactionHashes);
            hashes.Add(lastTx.Hash);

            var txs = new List<Transaction>();
            txs.Add(firstTx);
            txs.AddRange(transactions);
            txs.Add(lastTx);

            transactions = txs;
            block = new Block(block.Height, block.ChainAddress, block.Timestamp, hashes, block.PreviousHash, block.Payload);
        }

        public void AddBlock(Block block, IEnumerable<Transaction> transactions, BigInteger minimumFee)
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

            var oracle = Nexus.CreateOracleReader();

            foreach (var tx in transactions)
            {
                byte[] result;
                try
                {
                    if (ExecuteTransaction(tx, block.Timestamp, changeSet, block.Notify, oracle, minimumFee, out result))
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

            foreach (Transaction tx in transactions)
            {
                _transactions[tx.Hash] = tx;
                _transactionBlockMap[tx.Hash] = block.Hash;
            }

            var blockValidator = GetValidatorForBlock(block);
            if (blockValidator.IsNull)
            {
                throw new BlockGenerationException("no validator for this block");
            }

            Nexus.PluginTriggerBlock(this, block);
        }

        private bool ExecuteTransaction(Transaction transaction, Timestamp time, StorageChangeSetContext changeSet, Action<Hash, Event> onNotify, OracleReader oracle, BigInteger minimumFee, out byte[] result)
        {
            result = null;

            var runtime = new RuntimeVM(transaction.Script, this, time, transaction, changeSet, oracle, false);
            runtime.MinimumFee = minimumFee;
            runtime.ThrowOnFault = true;

            var state = runtime.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            var cost = runtime.UsedGas;

            foreach (var evt in runtime.Events)
            {
                onNotify(transaction.Hash, evt);
            }

            if (runtime.Stack.Count > 0)
            {
                var obj = runtime.Stack.Pop();
                result = Serialization.Serialize(obj);
            }

            return true;
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

        // NOTE should never be used directly from a contract, instead use Runtime.GetBalance!
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
                return items.Length;
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

        internal ExecutionContext GetContractContext(StorageContext storage, SmartContract contract)
        {
            if (!IsContractDeployed(storage, contract.Address))
            {
                throw new ChainException($"contract {contract.Name} not deployed on {Name} chain");
            }

            // TODO this needs to suport non-native contexts too..
            var context = new NativeExecutionContext(contract);
            return context;
        }

        public VMObject InvokeContract(StorageContext storage, string contractName, string methodName, Timestamp time, params object[] args)
        {
            var contract = Nexus.AllocContractByName(contractName);
            Throw.IfNull(contract, nameof(contract));

            var script = ScriptUtils.BeginScript().CallContract(contractName, methodName, args).EndScript();
            
            var result = InvokeScript(storage, script, time);

            if (result == null)
            {
                throw new ChainException($"Invocation of method '{methodName}' of contract '{contractName}' failed");
            }

            return result;
        }

        public VMObject InvokeContract(StorageContext storage, string contractName, string methodName, params object[] args)
        {
            return InvokeContract(storage, contractName, methodName, Timestamp.Now, args);
        }

        public VMObject InvokeScript(StorageContext storage, byte[] script)
        {
            return InvokeScript(storage, script, Timestamp.Now);
        }

        public VMObject InvokeScript(StorageContext storage, byte[] script, Timestamp time)
        {
            var oracle = Nexus.CreateOracleReader();
            var changeSet = new StorageChangeSetContext(storage);
            var vm = new RuntimeVM(script, this, time, null, changeSet, oracle, true);

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

        #region validators
        public Address GetCurrentValidator(StorageContext storage)
        {
            return InvokeContract(storage, Nexus.BlockContractName, "GetCurrentValidator").AsAddress();
        }

        public Address GetValidatorForBlock(Hash hash)
        {
            return GetValidatorForBlock(FindBlockByHash(hash));
        }

        public Address GetValidatorForBlock(Block block)
        {
            if (block.TransactionCount == 0)
            {
                return Address.Null;
            }

            var firstTxHash = block.TransactionHashes.First();
            var events = block.GetEventsForTransaction(firstTxHash);

            foreach (var evt in events)
            {
                if (evt.Kind == EventKind.BlockCreate && evt.Contract == Nexus.BlockContractName)
                {
                    return evt.Address;
                }
            }

            return Address.Null;
        }
        #endregion

        #region Contracts
        private byte[] GetContractListKey()
        {
            return Encoding.ASCII.GetBytes("contracts.");
        }

        private byte[] GetContractDeploymentKey(Address contractAddress)
        {
            var bytes = Encoding.ASCII.GetBytes("deploy.");
            var key = ByteArrayUtils.ConcatBytes(bytes, contractAddress.PublicKey);
            return key;
        }

        public bool IsContractDeployed(StorageContext storage, string name)
        {
            return IsContractDeployed(storage, SmartContract.GetAddressForName(name));
        }

        public bool IsContractDeployed(StorageContext storage, Address contractAddress)
        {
            var key = GetContractDeploymentKey(contractAddress);
            return storage.Has(key);
        }

        private void AddContractToDeployedList(StorageContext storage, Address contractAddress)
        {
            var contractList = new StorageList(GetContractListKey(), storage);
            contractList.Add<Address>(contractAddress);
        }

        public bool DeployNativeContract(StorageContext storage, Address contractAddress)
        {
            var key = GetContractDeploymentKey(contractAddress);
            if (storage.Has(key))
            {
                return false;
            }

            storage.Put(key, new byte[] { (byte)Opcode.RET });
            AddContractToDeployedList(storage, contractAddress);
            return true;
        }

        public bool DeployContract(StorageContext storage, byte[] script)
        {
            var contractAddress = Address.FromScript(script);
            var key = GetContractDeploymentKey(contractAddress);
            if (storage.Has(key))
            {
                return false;
            }

            storage.Put(key, script);
            AddContractToDeployedList(storage, contractAddress);
            return true;
        }
        #endregion
    }
}
