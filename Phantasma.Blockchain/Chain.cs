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
using Phantasma.Storage.Context;
using Phantasma.Domain;
using Phantasma.Core.Utils;
using Phantasma.Core.Performance;
using Phantasma.Storage.Utils;

namespace Phantasma.Blockchain
{
    public sealed class Chain : IChain
    {
        private const string TransactionHashMapTag = ".txs";
        private const string BlockHashMapTag = ".blocks";
        private const string BlockHeightListTag = ".height";
        private const string TxBlockHashMapTag = ".txblmp";
        private const string AddressTxHashMapTag = ".adblmp";
        private const string TaskListTag = ".tasks";

        #region PUBLIC
        public static readonly uint InitialHeight = 1;

        public Nexus Nexus { get; private set; }

        public string Name { get; private set; }
        public Address Address { get; private set; }

        public BigInteger Height => GetBlockHeight();

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

        public IContract[] GetContracts(StorageContext storage)
        {
            var contractList = new StorageList(GetContractListKey(), storage);
            var addresses = contractList.All<Address>();
            return addresses.Select(x => this.GetContractByAddress(storage, x)).ToArray();
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
        }

        private bool VerifyBlockBeforeAdd(Block block)
        {
            if (block.TransactionCount > DomainSettings.MaxTxPerBlock)
            {
                return false;
            }

            if (block.OracleData.Count() > DomainSettings.MaxOracleEntriesPerBlock)
            {
                return false;
            }

            if (block.Events.Count() > DomainSettings.MaxEventsPerBlock)
            {
                return false;
            }

            foreach (var txHash in block.TransactionHashes)
            {
                var evts = block.GetEventsForTransaction(txHash);
                if (evts.Count() > DomainSettings.MaxEventsPerTx)
                {
                    return false;
                }
            }

            return true;
        }

        public void AddBlock(Block block, IEnumerable<Transaction> transactions, BigInteger minimumFee, StorageChangeSetContext changeSet)
        {
            if (!block.IsSigned)
            {
                throw new BlockGenerationException($"block must be signed");
            }

            var unsignedBytes = block.ToByteArray(false);
            if (!block.Signature.Verify(unsignedBytes, block.Validator))
            {
                throw new BlockGenerationException($"block signature does not match validator {block.Validator.Text}");
            }

            if (!VerifyBlockBeforeAdd(block))
            {
                throw new BlockGenerationException($"block verification failed, would have overflown, hash:{block.Hash}");
            }

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            var expectedBlockHeight = hashList.Count() + 1;
            if (expectedBlockHeight != block.Height)
            {
                throw new ChainException("unexpected block height");
            }

            // from here on, the block is accepted
            using (var m = new ProfileMarker("changeSet.Execute"))
                changeSet.Execute();

            hashList.Add<Hash>(block.Hash);

            using (var m = new ProfileMarker("Compress"))
            {
                var blockMap = new StorageMap(BlockHashMapTag, this.Storage);
                var blockBytes = block.ToByteArray(true);
                blockBytes = CompressionUtils.Compress(blockBytes);
                blockMap.Set<Hash, byte[]>(block.Hash, blockBytes);

                var txMap = new StorageMap(TransactionHashMapTag, this.Storage);
                var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);
                foreach (Transaction tx in transactions)
                {
                    var txBytes = tx.ToByteArray(true);
                    txBytes = CompressionUtils.Compress(txBytes);
                    txMap.Set<Hash, byte[]>(tx.Hash, txBytes);
                    txBlockMap.Set<Hash, Hash>(tx.Hash, block.Hash);
                }
            }

            using (var m = new ProfileMarker("AddressBlockHashMapTag"))
                foreach (var transaction in transactions)
                {
                    var addresses = new HashSet<Address>();
                    var events = block.GetEventsForTransaction(transaction.Hash);

                    foreach (var evt in events)
                    {
                        if (evt.Contract == "gas" && (evt.Address.IsSystem || evt.Address == block.Validator))
                        {
                            continue;
                        }

                        addresses.Add(evt.Address);
                    }

                    var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
                    foreach (var address in addresses)
                    {
                        var addressList = addressTxMap.Get<Address, StorageList>(address);
                        addressList.Add<Hash>(transaction.Hash);
                    }
                }

            using (var m = new ProfileMarker("Nexus.PluginTriggerBlock"))
                Nexus.PluginTriggerBlock(this, block);
        }

        // signingKeys should be null if the block should not be modified
        public StorageChangeSetContext ProcessTransactions(Block block, IEnumerable<Transaction> transactions
                , OracleReader oracle, BigInteger minimumFee, out Transaction inflationTx, PhantasmaKeys signingKeys)
        {
            bool allowModify = signingKeys != null;

            if (allowModify)
            {
                block.CleanUp();
            }

            var changeSet = new StorageChangeSetContext(this.Storage);

            transactions = ProcessPendingTasks(block, oracle, minimumFee, changeSet, allowModify).Concat(transactions);

            int txIndex = 0;
            foreach (var tx in transactions)
            {
                VMObject vmResult;
                try
                {
                    using (var m = new ProfileMarker("ExecuteTransaction"))
                    {
                        if (ExecuteTransaction(txIndex, tx, tx.Script, block.Validator, block.Timestamp, changeSet, block.Notify, oracle, ChainTask.Null, minimumFee, out vmResult, allowModify))
                        {
                            if (vmResult != null)
                            {
                                if (allowModify)
                                {
                                    var resultBytes = Serialization.Serialize(vmResult);
                                    block.SetResultForHash(tx.Hash, resultBytes);
                                }
                            }
                        }
                        else
                        {
                            throw new InvalidTransactionException(tx.Hash, "script execution failed");
                        }
                    }
                }
                catch (Exception e)
                {
                    e = e.ExpandInnerExceptions();

                    // log original exception, throwing it again kills the call stack!
                    Log.Error($"Exception while transactions of block {block.Height}: " + e);

                    if (tx == null)
                    {
                        throw new BlockGenerationException(e.Message);
                    }

                    throw new InvalidTransactionException(tx.Hash, e.Message);
                }

                txIndex++;
            }

	        inflationTx = null;

            if (this.IsRoot && allowModify)
            {
                var inflationReady = NativeContract.LoadFieldFromStorage<bool>(changeSet, NativeContractKind.Gas, nameof(GasContract._inflationReady));
                if (inflationReady)
                {
                    var script = new ScriptBuilder()
                        .AllowGas(block.Validator, Address.Null, minimumFee, 999999)
                        .CallContract(NativeContractKind.Gas, nameof(GasContract.ApplyInflation), block.Validator)
                        .SpendGas(block.Validator)
                        .EndScript();

                    var transaction = new Transaction(this.Nexus.Name, this.Name, script, block.Timestamp.Value + 1, "SYSTEM");

                    transaction.Sign(signingKeys);

                    VMObject vmResult;

                    if (!ExecuteTransaction(-1, transaction, transaction.Script, block.Validator, block.Timestamp, changeSet, block.Notify, oracle, ChainTask.Null, minimumFee, out vmResult, allowModify))
                    {
                        throw new ChainException("failed to execute inflation transaction");
                    }

		            inflationTx = transaction;
                    block.AddTransactionHash(transaction.Hash);
                }
            }

            if (block.Protocol > DomainSettings.LatestKnownProtocol)
            {
                throw new BlockGenerationException($"unexpected protocol number {block.Protocol}, maybe software update required?");
            }

            // Only check protocol version if block is created on this node, no need to check if it's a non validator node.
            if (allowModify)
            {
                var expectedProtocol = Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
                if (block.Protocol != expectedProtocol)
                {
                    throw new BlockGenerationException($"invalid protocol number {block.Protocol}, expected protocol {expectedProtocol}");
                }

                using (var m = new ProfileMarker("CloseBlock"))
                {
                    CloseBlock(block, changeSet);
                }
            }

            return changeSet;
        }

        public StorageChangeSetContext ProcessBlock(Block block, IEnumerable<Transaction> transactions, BigInteger minimumFee, out Transaction inflationTx, PhantasmaKeys signingKeys)
        {

            if (!block.Validator.IsUser)
            {
                throw new BlockGenerationException($"block validator must be user address");
            }

            Block lastBlock;
            using (var m = new ProfileMarker("GetLastBlock"))
            {
                var lastBlockHash = GetLastBlockHash();
                lastBlock = GetBlockByHash(lastBlockHash);
            }

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

                if (block.Timestamp < lastBlock.Timestamp)
                {
                    throw new BlockGenerationException($"timestamp of block {block.Timestamp} should be greater than {lastBlock.Timestamp}");
                }
            }

            var inputHashes = new HashSet<Hash>(transactions.Select(x => x.Hash).Distinct());

            var txBlockMap = new StorageMap(TxBlockHashMapTag, this.Storage);

            var diff = transactions.Count() - inputHashes.Count;
            if (diff > 0)
            {
                var temp = new HashSet<Hash>();
                foreach (var tx in transactions)
                {
                    if (temp.Contains(tx.Hash))
                    {
                        throw new DuplicatedTransactionException(tx.Hash, $"transaction {tx.Hash} appears more than once in the block being minted");
                    }
                    else
                    if (txBlockMap.ContainsKey<Hash>(tx.Hash))
                    {
                        var previousBlockHash = txBlockMap.Get<Hash, Hash>(tx.Hash);
                        throw new DuplicatedTransactionException(tx.Hash, $"transaction {tx.Hash} already added to previous block {previousBlockHash}");
                    }
                    else
                    {

                        temp.Add(tx.Hash);
                    }
                }                
            }

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
#if DEBUG
                    tx.IsValid(this);
#endif
                    throw new InvalidTransactionException(tx.Hash, $"invalid transaction with hash {tx.Hash}");
                }
            }

            var oracle = Nexus.GetOracleReader();

            block.CleanUp();

            Address expectedValidator;
            using (var m = new ProfileMarker("GetValidator"))
                expectedValidator = Nexus.HasGenesis ? GetValidator(Nexus.RootStorage, block.Timestamp) : Nexus.GetGenesisAddress(Nexus.RootStorage);

            if (block.Validator != expectedValidator && !expectedValidator.IsNull)
            {
                throw new BlockGenerationException($"unexpected validator {block.Validator}, expected {expectedValidator}");
            }

            var changeSet = ProcessTransactions(block, transactions, oracle, minimumFee, out inflationTx, signingKeys);

            if (oracle.Entries.Any())
            {
                block.MergeOracle(oracle);
                oracle.Clear();
            }

            return changeSet;
        }

        private bool ExecuteTransaction(int index, Transaction transaction, byte[] script, Address validator, Timestamp time, StorageChangeSetContext changeSet
                , Action<Hash, Event> onNotify, OracleReader oracle, ITask task, BigInteger minimumFee, out VMObject result, bool allowModify = true)
        {
            if (!transaction.HasSignatures)
            {
                throw new ChainException("Cannot execute unsigned transaction");
            }

            result = null;

            uint offset = 0;

            RuntimeVM runtime;
            using (var m = new ProfileMarker("new RuntimeVM"))
            {
                runtime = new RuntimeVM(index, script, offset, this, validator, time, transaction, changeSet, oracle, task, false);
            }
            runtime.MinimumFee = minimumFee;

            ExecutionState state;
            using (var m = new ProfileMarker("runtime.Execute"))
                state = runtime.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            //var cost = runtime.UsedGas;

            using (var m = new ProfileMarker("runtime.Events"))
            {
                foreach (var evt in runtime.Events)
                {
                    using (var m2 = new ProfileMarker(evt.ToString()))
                        if (allowModify)
                        {
                            onNotify(transaction.Hash, evt);
                        }
                }
            }

            if (runtime.Stack.Count > 0)
            {
                result = runtime.Stack.Pop();
            }

            // merge transaction oracle data 
            oracle.MergeTxData();

            return true;
        }

        // NOTE should never be used directly from a contract, instead use Runtime.GetBalance!
        public BigInteger GetTokenBalance(StorageContext storage, IToken token, Address address)
        {
            if (token.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = new BalanceSheet(token);
                return balances.Get(storage, address);
            }
            else
            {
                var ownerships = new OwnershipSheet(token.Symbol);
                var items = ownerships.Get(storage, address);
                return items.Length;
            }
        }

        public BigInteger GetTokenBalance(StorageContext storage, string symbol, Address address)
        {
            var token = Nexus.GetTokenInfo(storage, symbol);
            return GetTokenBalance(storage, token, address);
        }

        public BigInteger GetTokenSupply(StorageContext storage, string symbol)
        {
            var supplies = new SupplySheet(symbol, this, Nexus);
            return supplies.GetTotal(storage);
        }

        // NOTE this lists only nfts owned in this chain
        public BigInteger[] GetOwnedTokens(StorageContext storage, string tokenSymbol, Address address)
        {
            var ownership = new OwnershipSheet(tokenSymbol);
            return ownership.Get(storage, address).ToArray();
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

            var context = new ChainExecutionContext(contract);
            return context;
        }

        public VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return InvokeContractAtTimestamp(storage, time, nativeContract.GetContractName(), methodName, args);
        }

        public VMObject InvokeContract(StorageContext storage, NativeContractKind nativeContract, string methodName, params object[] args)
        {
            return InvokeContract(storage, nativeContract.GetContractName(), methodName, args);
        }

        public VMObject InvokeContract(StorageContext storage, string contractName, string methodName, params object[] args)
        {
            return InvokeContractAtTimestamp(storage, Timestamp.Now, contractName, methodName, args);
        }

        public VMObject InvokeContractAtTimestamp(StorageContext storage, Timestamp time, string contractName, string methodName, params object[] args)
        {
            var script = ScriptUtils.BeginScript().CallContract(contractName, methodName, args).EndScript();

            var result = InvokeScript(storage, script, time);

            if (result == null)
            {
                throw new ChainException($"Invocation of method '{methodName}' of contract '{contractName}' failed");
            }

            return result;
        }

        public VMObject InvokeScript(StorageContext storage, byte[] script)
        {
            return InvokeScript(storage, script, Timestamp.Now);
        }

        public VMObject InvokeScript(StorageContext storage, byte[] script, Timestamp time)
        {
            var oracle = Nexus.GetOracleReader();
            var changeSet = new StorageChangeSetContext(storage);
            uint offset = 0;
            var vm = new RuntimeVM(-1, script, offset, this, Address.Null, time, null, changeSet, oracle, ChainTask.Null, true);

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

        // generates incremental ID (unique to this chain)
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
            var lastTxHash = block.TransactionHashes[block.TransactionHashes.Length - 1];
            var evts = block.GetEventsForTransaction(lastTxHash);

            BigInteger total = 0;
            foreach (var evt in evts)
            {
                if (evt.Kind == EventKind.TokenClaim && evt.Contract == "block")
                {
                    var data = evt.GetContent<TokenEventData>();
                    total += data.Value;
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
                if (evt.Kind == EventKind.GasPayment && evt.Contract == "gas")
                {
                    var info = evt.GetContent<GasEventData>();
                    fee += info.amount * info.price;
                }
            }

            return fee;
        }
        #endregion

        #region Contracts
        private byte[] GetContractListKey()
        {
            return Encoding.ASCII.GetBytes("contracts.");
        }

        private byte[] GetContractKey(Address contractAddress, string field)
        {
            var bytes = Encoding.ASCII.GetBytes(field);
            var key = ByteArrayUtils.ConcatBytes(bytes, contractAddress.ToByteArray());
            return key;
        }

        public bool IsContractDeployed(StorageContext storage, string name)
        {
            if (ValidationUtils.IsValidTicker(name))
            {
                return Nexus.TokenExists(storage, name);
            }

            return IsContractDeployed(storage, SmartContract.GetAddressForName(name));
        }

        public bool IsContractDeployed(StorageContext storage, Address contractAddress)
        {
            if (contractAddress == SmartContract.GetAddressForName(Nexus.GasContractName))
            {
                return true;
            }

            if (contractAddress == SmartContract.GetAddressForName(Nexus.BlockContractName))
            {
                return true;
            }

            var key = GetContractKey(contractAddress, "script");
            if (storage.Has(key))
            {
                return true;
            }

            var token = Nexus.GetTokenInfo(storage, contractAddress);
            return (token != null);
        }

        public bool DeployContractScript(StorageContext storage, Address contractOwner, string name, Address contractAddress, byte[] script, ContractInterface abi)
        {
            var scriptKey = GetContractKey(contractAddress, "script");
            if (storage.Has(scriptKey))
            {
                return false;
            }

            storage.Put(scriptKey, script);

            var ownerBytes = contractOwner.ToByteArray();
            var ownerKey = GetContractKey(contractAddress, "owner");
            storage.Put(ownerKey, ownerBytes);

            var abiBytes = abi.ToByteArray();
            var abiKey = GetContractKey(contractAddress, "abi");
            storage.Put(abiKey, abiBytes);

            var nameBytes = Encoding.ASCII.GetBytes(name);
            var nameKey = GetContractKey(contractAddress, "name");
            storage.Put(nameKey, nameBytes);

            var contractList = new StorageList(GetContractListKey(), storage);
            contractList.Add<Address>(contractAddress);

            return true;
        }

        public SmartContract GetContractByAddress(StorageContext storage, Address contractAddress)
        {
            var nameKey = GetContractKey(contractAddress, "name");

            if (storage.Has(nameKey))
            {
                var nameBytes = storage.Get(nameKey);

                var name = Encoding.ASCII.GetString(nameBytes);
                return GetContractByName(storage, name);
            }

            var symbols = Nexus.GetTokens(storage);
            foreach (var symbol in symbols)
            {
                var tokenAddress = TokenUtils.GetContractAddress(symbol);

                if (tokenAddress == contractAddress)
                {
                    var token = Nexus.GetTokenInfo(storage, symbol);
                    return new CustomContract(token.Symbol, token.Script, token.ABI);
                }
            }

            return Nexus.GetNativeContractByAddress(contractAddress);
        }

        public SmartContract GetContractByName(StorageContext storage, string name)
        {
            if (Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                return Nexus.GetContractByName(storage, name);
            }

            var address = SmartContract.GetAddressForName(name);
            var scriptKey = GetContractKey(address, "script");
            if (!storage.Has(scriptKey))
            {
                return null;
            }

            var script = storage.Get(scriptKey);

            var abiKey = GetContractKey(address, "abi");
            var abiBytes = storage.Get(abiKey);
            var abi = ContractInterface.FromBytes(abiBytes);

            return new CustomContract(name, script, abi);
        }

        public void UpgradeContract(StorageContext storage, string name, byte[] script, ContractInterface abi)
        {
            if (Nexus.IsNativeContract(name) || ValidationUtils.IsValidTicker(name))
            {
                throw new ChainException($"Cannot upgrade this type of contract: {name}");
            }

            if (!IsContractDeployed(storage, name))
            {
                throw new ChainException($"Cannot upgrade non-existing contract: {name}");
            }

            var address = SmartContract.GetAddressForName(name);

            var scriptKey = GetContractKey(address, "script");
            storage.Put(scriptKey, script);

            var abiKey = GetContractKey(address, "abi");
            var abiBytes = abi.ToByteArray();
            storage.Put(abiKey, abiBytes);
        }

        public Address GetContractOwner(StorageContext storage, Address contractAddress)
        {
            if (contractAddress.IsSystem)
            {
                var ownerKey = GetContractKey(contractAddress, "owner");
                var bytes = storage.Get(ownerKey);
                if (bytes != null)
                {
                    return Address.FromBytes(bytes);
                }

                var token = Nexus.GetTokenInfo(storage, contractAddress);
                if (token != null)
                {
                    return token.Owner;
                }
            }

            return Address.Null;
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

            if (height > this.Height)
            {
                return Hash.Null;
            }

            var hashList = new StorageList(BlockHeightListTag, this.Storage);
            // NOTE chain heights start at 1, but list index start at 0
            var hash = hashList.Get<Hash>(height - 1);
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
                var bytes = blockMap.Get<Hash, byte[]>(hash);
                bytes = CompressionUtils.Decompress(bytes);
                var block = Block.Unserialize(bytes);

                if (block.Hash != hash)
                {
                    throw new ChainException("data corruption on block: " + hash);
                }

                return block;
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
                var bytes = txMap.Get<Hash, byte[]>(hash);
                bytes = CompressionUtils.Decompress(bytes);
                var tx = Transaction.Unserialize(bytes);

                if (tx.Hash != hash)
                {
                    throw new ChainException("data corruption on transaction: " + hash);
                }

                return tx;
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

        public Hash[] GetTransactionHashesForAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            return addressList.All<Hash>();
        }

        public Timestamp GetLastActivityOfAddress(Address address)
        {
            var addressTxMap = new StorageMap(AddressTxHashMapTag, this.Storage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            var count = addressList.Count();
            if (count <=0)
            {
                return Timestamp.Null;
            }

            var lastTxHash = addressList.Get<Hash>(count - 1);
            var blockHash = GetBlockHashOfTransaction(lastTxHash);

            var block = GetBlockByHash(blockHash);

            if (block == null) // should never happen
            {
                return Timestamp.Null;
            }

            return block.Timestamp;
        }

        #region SWAPS
        private StorageList GetSwapListForAddress(StorageContext storage, Address address)
        {
            var key = ByteArrayUtils.ConcatBytes(Encoding.UTF8.GetBytes(".swapaddr"), address.ToByteArray());
            return new StorageList(key, storage);
        }

        private StorageMap GetSwapMap(StorageContext storage)
        {
            var key = Encoding.UTF8.GetBytes(".swapmap");
            return new StorageMap(key, storage);
        }

        public void RegisterSwap(StorageContext storage, Address from, ChainSwap swap)
        {
            var list = GetSwapListForAddress(storage, from);
            list.Add<Hash>(swap.sourceHash);

            var map = GetSwapMap(storage);
            map.Set<Hash, ChainSwap>(swap.sourceHash, swap);
        }

        public ChainSwap GetSwap(StorageContext storage, Hash sourceHash)
        {
            var map = GetSwapMap(storage);

            if (map.ContainsKey<Hash>(sourceHash))
            {
                return map.Get<Hash, ChainSwap>(sourceHash);
            }

            throw new ChainException("invalid chain swap hash: " + sourceHash);
        }

        public Hash[] GetSwapHashesForAddress(StorageContext storage, Address address)
        {
            var list = GetSwapListForAddress(storage, address);
            return list.All<Hash>();
        }
        #endregion

        #region TASKS
        private byte[] GetTaskKey(BigInteger taskID, string field)
        {
            var bytes = Encoding.ASCII.GetBytes(field);
            var key = ByteArrayUtils.ConcatBytes(bytes, taskID.ToUnsignedByteArray());
            return key;
        }

        public ITask StartTask(StorageContext storage, Address from, string contractName, ContractMethod method, uint frequency, uint delay, TaskFrequencyMode mode, BigInteger gasLimit)
        {
            if (!IsContractDeployed(storage, contractName))
            {
                return null;
            }

            var taskID = GenerateUID(storage);
            var task = new ChainTask(taskID, from, contractName, method.name, frequency, delay, mode, gasLimit, this.Height + 1, true);

            var taskKey = GetTaskKey(taskID, "task_info");

            var taskBytes = task.ToByteArray();

            storage.Put(taskKey, taskBytes);

            var taskList = new StorageList(TaskListTag, this.Storage);
            taskList.Add<BigInteger>(taskID);

            return task;
        }

        public bool StopTask(StorageContext storage, BigInteger taskID)
        {
            var taskKey = GetTaskKey(taskID, "task_info");

            if (this.Storage.Has(taskKey))
            {
                this.Storage.Delete(taskKey);

                taskKey = GetTaskKey(taskID, "task_run");
                if (this.Storage.Has(taskKey))
                {
                    this.Storage.Delete(taskKey);
                }

                var taskList = new StorageList(TaskListTag, this.Storage);
                taskList.Remove<BigInteger>(taskID);

                return true;
            }

            return false;
        }

        public ChainTask GetTask(StorageContext storage, BigInteger taskID)
        {
            var taskKey = GetTaskKey(taskID, "task_info");

            var taskBytes = this.Storage.Get(taskKey);

            var task = ChainTask.FromBytes(taskID, taskBytes);

            return task;

        }

        private IEnumerable<Transaction> ProcessPendingTasks(Block block, OracleReader oracle, BigInteger minimumFee, StorageChangeSetContext changeSet, bool allowModify)
        {
            var taskList = new StorageList(TaskListTag, changeSet);
            var taskCount = taskList.Count();

            List<Transaction> transactions = null;

            int i = 0;
            while (i < taskCount)
            {
                var taskID = taskList.Get<BigInteger>(i);
                var task = GetTask(changeSet, taskID);

                Transaction tx;

                var taskResult = ProcessPendingTask(block, oracle, minimumFee, changeSet, allowModify, task, out tx);
                if (taskResult == TaskResult.Running) 
                {
                    i++;
                }
                else
                {
                    taskList.RemoveAt(i);
                }

                if (tx != null)
                {
                    if (transactions == null)
                    {
                        transactions = new List<Transaction>();
                    }

                    transactions.Add(tx);
                }
            }

            if (transactions != null)
            {
                return transactions;
            }

            return Enumerable.Empty<Transaction>();
        }

        private BigInteger GetTaskTimeFromBlock(TaskFrequencyMode mode, Block block)
        {
            switch (mode)
            {
                case TaskFrequencyMode.Blocks:
                    {
                        return block.Height;
                    }

                case TaskFrequencyMode.Time:
                    {
                        return block.Timestamp.Value;
                    }

                default:
                    throw new ChainException("Unknown task mode: " + mode);
            }
        }

        private TaskResult ProcessPendingTask(Block block, OracleReader oracle, BigInteger minimumFee, StorageChangeSetContext changeSet, bool allowModify, ChainTask task, out Transaction transaction)
        {
            transaction = null;

            BigInteger currentRun = GetTaskTimeFromBlock(task.Mode, block);
            var taskKey = GetTaskKey(task.ID, "task_run");

            if (task.Mode != TaskFrequencyMode.Always)
            {
                bool isFirstRun = !changeSet.Has(taskKey);                

                if (isFirstRun)
                {
                    var taskBlockHash = GetBlockHashAtHeight(task.Height);
                    var taskBlock = GetBlockByHash(taskBlockHash);

                    BigInteger firstRun = GetTaskTimeFromBlock(task.Mode, taskBlock) + task.Delay;

                    if (currentRun < firstRun)
                    {
                        return TaskResult.Skipped; // skip execution for now
                    }
                }
                else
                {
                    BigInteger lastRun = isFirstRun ? changeSet.Get<BigInteger>(taskKey) : 0;

                    var diff = currentRun - lastRun;
                    if (diff < task.Frequency)
                    {
                        return TaskResult.Skipped; // skip execution for now
                    }
                }
            }
            else
            {
                currentRun = 0;
            }

            using (var m = new ProfileMarker("ExecuteTask"))
            {
                var taskScript = new ScriptBuilder()
                    .AllowGas(task.Owner, Address.Null, minimumFee, task.GasLimit)
                    .CallContract(task.ContextName, task.Method)
                    .SpendGas(task.Owner)
                    .EndScript();

                transaction = new Transaction(this.Nexus.Name, this.Name, taskScript, block.Timestamp.Value + 1, "TASK");

                VMObject vmResult;

                if (ExecuteTransaction(-1, transaction, transaction.Script, block.Validator, block.Timestamp, changeSet, block.Notify, oracle, task, minimumFee, out vmResult, allowModify))
                {
                    var resultBytes = Serialization.Serialize(vmResult);
                    block.SetResultForHash(transaction.Hash, resultBytes);

                    // update last_run value in storage
                    if (currentRun > 0)
                    {
                        changeSet.Put<BigInteger>(taskKey, currentRun);
                    }

                    var shouldStop = vmResult.AsBool();
                    return shouldStop ? TaskResult.Halted : TaskResult.Running;
                }
                else
                {
                    transaction = null;
                    return TaskResult.Crashed;
                }
            }
        }
        #endregion

        #region block validation
        public Address GetValidator(StorageContext storage, Timestamp targetTime)
        {
            var rootStorage = this.IsRoot ? storage : Nexus.RootStorage;

            if (!Nexus.HasGenesis)
            {
                return Nexus.GetGenesisAddress(rootStorage);
            }

            var slotDuration = (int)Nexus.GetGovernanceValue(rootStorage, ValidatorContract.ValidatorRotationTimeTag);

            var genesisHash = Nexus.GetGenesisHash(rootStorage);
            var genesisBlock = Nexus.RootChain.GetBlockByHash(genesisHash);

            Timestamp validationSlotTime = genesisBlock.Timestamp;

            var diff = targetTime - validationSlotTime;

            int validatorIndex = (int)(diff / slotDuration);
            var validatorCount = Nexus.GetPrimaryValidatorCount();
            var chainIndex = Nexus.GetIndexOfChain(this.Name);

            if (chainIndex < 0)
            {
                return Address.Null;
            }

            validatorIndex += chainIndex;
            validatorIndex = validatorIndex % validatorCount;

            var currentIndex = validatorIndex;

            do
            {
                var validator = Nexus.GetValidatorByIndex(validatorIndex);
                if (validator.type == ValidatorType.Primary && !validator.address.IsNull)
                {
                    return validator.address;
                }

                validatorIndex++;
                if (validatorIndex >= validatorCount)
                {
                    validatorIndex = 0;
                }
            } while (currentIndex != validatorIndex);

            // should never reached here, failsafe
            return Nexus.GetGenesisAddress(rootStorage);
        }

        public void CloseBlock(Block block, StorageChangeSetContext storage)
        {
            var rootStorage = this.IsRoot ? storage : Nexus.RootStorage;

            if (block.Height > 1)
            {
                var prevBlock = GetBlockByHash(block.PreviousHash);

                if (prevBlock.Validator != block.Validator)
                {
                    block.Notify(new Event(EventKind.ValidatorSwitch, block.Validator, "block", Serialization.Serialize(prevBlock)));
                }
            }

            var tokenStorage = this.Name == DomainSettings.RootChainName ? storage : Nexus.RootStorage;
            var token = this.Nexus.GetTokenInfo(tokenStorage, DomainSettings.FuelTokenSymbol);
            var balance = new BalanceSheet(token);
            var blockAddress = Address.FromHash("block");
            var totalAvailable = balance.Get(storage, blockAddress);

            var targets = new List<Address>();

            if (Nexus.HasGenesis)
            {
                var validators = Nexus.GetValidators();

                var totalValidators = Nexus.GetPrimaryValidatorCount();

                for (int i = 0; i < totalValidators; i++)
                {
                    var validator = validators[i];
                    if (validator.type != ValidatorType.Primary)
                    {
                        continue;
                    }

                    targets.Add(validator.address);
                }
            }
            else
            if (totalAvailable > 0)
            {
                targets.Add(Nexus.GetGenesisAddress(rootStorage));
            }

            if (targets.Count > 0)
            {
                if (!balance.Subtract(storage, blockAddress, totalAvailable))
                {
                    throw new BlockGenerationException("could not subtract balance from block address");
                }

                var amountPerValidator = totalAvailable / targets.Count;
                var leftOvers = totalAvailable - (amountPerValidator * targets.Count);

                foreach (var address in targets)
                {
                    BigInteger amount = amountPerValidator;

                    if (address == block.Validator)
                    {
                        amount += leftOvers;
                    }

                    // TODO this should use triggers when available...
                    if (!balance.Add(storage, address, amount))
                    {
                        throw new BlockGenerationException($"could not add balance to {address}");
                    }

                    var eventData = Serialization.Serialize(new TokenEventData(DomainSettings.FuelTokenSymbol, amount, this.Name));
                    block.Notify(new Event(EventKind.TokenClaim, address, "block", eventData));
                }
            }
        }
        #endregion

        public Address LookUpName(StorageContext storage, string name)
        {
            if (IsContractDeployed(storage, name))
            {
                return SmartContract.GetAddressForName(name);
            }

            return this.Nexus.LookUpName(storage, name);
        }

        public string GetNameFromAddress(StorageContext storage, Address address)
        {
            if (address.IsNull)
            {
                return ValidationUtils.NULL_NAME;
            }

            if (address.IsSystem)
            {
                if (address == DomainSettings.InfusionAddress)
                {
                    return DomainSettings.InfusionName;
                }

                var contract = this.GetContractByAddress(storage, address);
                if (contract != null)
                {
                    return contract.Name;
                }
                else
                {
                    var tempChain = Nexus.GetChainByAddress(address);
                    if (tempChain != null)
                    {
                        return tempChain.Name;
                    }

                    var org = Nexus.GetOrganizationByAddress(storage, address);
                    if (org != null)
                    {
                        return org.ID;
                    }

                    return ValidationUtils.ANONYMOUS_NAME;
                }
            }

            return Nexus.RootChain.InvokeContract(storage, Nexus.AccountContractName, nameof(AccountContract.LookUpAddress), address).AsString();
        }

    }
}
