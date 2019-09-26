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
    public sealed class Chain 
    {
        private const string TransactionHashMapTag = ".txs";
        private const string TxBlockHashMapTag = ".txblmp";
        private const string BlockHashMapTag = ".blocks";
        private const string BlockHeightListTag = ".height";

        #region PRIVATE
        /*        private KeyValueStore<Hash, Transaction> _transactions;
                private KeyValueStore<Hash, Block> _blocks;
                private KeyValueStore<Hash, Hash> _transactionBlockMap;
                private KeyValueStore<BigInteger, Hash> _blockHeightMap;

                private Dictionary<Hash, StorageChangeSetContext> _blockChangeSets = new Dictionary<Hash, StorageChangeSetContext>();
                */

        #endregion

        #region PUBLIC
        public static readonly uint InitialHeight = 1;

        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public BigInteger BlockHeight => GetBlockHeight();

        public readonly Logger Log;

        public StorageContext Storage { get; private set; }

        public bool IsRoot => this.Name == DomainSettings.RootChainName;
        #endregion

        public Chain(Nexus nexus, string name, Logger log = null)
        {
            Throw.IfNull(nexus, "nexus required");

            this.Name = name;
            this.Nexus = nexus;

            this.Address = Address.FromHash(this.Name);

            this.Storage = new KeyStoreStorage(Nexus.GetChainStorage(this.Name));

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
            block = new Block(block.Height, block.ChainAddress, block.Timestamp, hashes, block.PreviousHash, block.Protocol);
        }

        public void AddBlock(Block block, IEnumerable<Transaction> transactions, BigInteger minimumFee)
        {
            /*if (CurrentEpoch != null && CurrentEpoch.IsSlashed(Timestamp.Now))
            {
                return false;
            }*/

            var lastBlockHash = GetLastBlockHash();
            var lastBlock = GetBlockByHash(lastBlockHash);

            if (lastBlock != null)
            {
                if (lastBlock.Height != block.Height - 1)
                {
                    throw new BlockGenerationException($"height of block should be {lastBlock.Height + 1}");
                }

                if (block.PreviousHash != lastBlock.Hash)
                {
                    throw new BlockGenerationException($"previous hash should be {lastBlock.PreviousHash}");
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

            // TODO avoid fetching this every time
            var expectedProtocol = Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
            if (block.Protocol != expectedProtocol)
            {
                throw new BlockGenerationException($"invalid protocol number {block.Protocol}, expected protocol {expectedProtocol}");
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

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            var expectedBlockHeight = hashList.Count() + 1;
            if (expectedBlockHeight != block.Height)
            {
                throw new ChainException("unexpected block height");
            }

            // from here on, the block is accepted
            changeSet.Execute();

            hashList.Add<Hash>(block.Hash);

            var blockMap = new StorageMap(BlockHashMapTag, this.Storage);
            blockMap.Set<Hash, Block>(block.Hash, block);

            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);
            foreach (Transaction tx in transactions)
            {
                txMap.Set<Hash, Transaction>(tx.Hash, tx);
                txBlockMap.Set<Hash, Hash>(tx.Hash, block.Hash);
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
        /*
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
        }*/

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

        public BigInteger GetTransactionFee(Hash transactionHash)
        {
            Throw.IfNull(transactionHash, nameof(transactionHash));

            BigInteger fee = 0;

            var blockHash = GetBlockHashOfTransaction(transactionHash);
            var block = GetBlockByHash(blockHash);
            Throw.IfNull(block, nameof(block));

            var events = block.GetEventsForTransaction(transactionHash);
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
            return GetValidatorForBlock(GetBlockByHash(hash));
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

        private BigInteger GetBlockHeight()
        {
            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            return hashList.Count();
        }

        public Hash GetLastBlockHash()
        {
            var lastHeight = GetBlockHeight();
            if (lastHeight <= 0)
            {
                return Hash.Null;
            }

            return GetBlockHashAtHeight(lastHeight);
        }

        public Hash GetBlockHashAtHeight(BigInteger height)
        {
            if (height <= 0)
            {
                throw new ChainException("invalid block height");
            }

            if (height > this.BlockHeight)
            {
                return Hash.Null;
            }

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            // NOTE chain heights start at 1, but list index start at 0
            var hash = hashList.Get<Hash>(height-1); 
            return hash;
        }

        public Block GetBlockByHash(Hash hash)
        {
            if (hash == Hash.Null)
            {
                return null;
            }

            var blockMap = new StorageMap(BlockHashMapTag, this.Storage);

            if (blockMap.ContainsKey<Hash>(hash))
            {
                return blockMap.Get<Hash, Block>(hash);
            }

            return null;
        }

        public bool ContainsBlockHash(Hash hash)
        {
            if (hash == null)
            {
                return false;
            }

            return GetBlockByHash(hash) != null;
        }

        public BigInteger GetTransactionCount()
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            return txMap.Count();
        }

        public bool ContainsTransaction(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            return txMap.ContainsKey(hash);
        }

        public Transaction GetTransactionByHash(Hash hash)
        {
            var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
            if (txMap.ContainsKey<Hash>(hash))
            {
                return txMap.Get<Hash, Transaction>(hash);
            }

            return null;
        }

        public Hash GetBlockHashOfTransaction(Hash transactionHash)
        {
            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);

            if (txBlockMap.ContainsKey(transactionHash))
            {
                var blockHash = txBlockMap.Get<Hash, Hash>(transactionHash);
                return blockHash;
            }

            return Hash.Null;
        }

        public IEnumerable<Transaction> GetBlockTransactions(Block block)
        {
            return block.TransactionHashes.Select(hash => GetTransactionByHash(hash));
        }

    }
}
