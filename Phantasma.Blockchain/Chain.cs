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
using Phantasma.Contracts;
using Phantasma.Contracts.Native;

namespace Phantasma.Blockchain
{
    public sealed class Chain : IChain
    {
        private const string TransactionHashMapTag = ".txs";
        private const string BlockHashMapTag = ".blocks";
        private const string BlockHeightListTag = ".height";
        private const string TxBlockHashMapTag = ".txblmp";
        private const string AddressBlockHashMapTag = ".adblmp";

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
            return addresses.Select(x => Nexus.GetContractByAddress(Nexus.RootStorage, x)).ToArray();
        }

        public override string ToString()
        {
            return $"{Name} ({Address})";
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
                    if (evt.Address.IsSystem)
                    {
                        continue;
                    }

                    addresses.Add(evt.Address);
                }

                var addressTxMap = new StorageMap(AddressBlockHashMapTag, this.Storage);
                foreach (var address in addresses)
                {
                    var addressList = addressTxMap.Get<Address, StorageList>(address);
                    addressList.Add<Hash>(transaction.Hash);
                }
            }

            using (var m = new ProfileMarker("Nexus.PluginTriggerBlock"))
                Nexus.PluginTriggerBlock(this, block);
        }

        public StorageChangeSetContext ValidateBlock(Block block, IEnumerable<Transaction> transactions, BigInteger minimumFee)
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
#if DEBUG
                    tx.IsValid(this);
#endif
                    throw new InvalidTransactionException(tx.Hash, $"invalid transaction with hash {tx.Hash}");
                }
            }

            var changeSet = new StorageChangeSetContext(this.Storage);

            var oracle = Nexus.GetOracleReader();
            oracle.Clear();

            block.CleanUp();

            Address expectedValidator;
            using (var m = new ProfileMarker("GetValidator"))
                expectedValidator  = Nexus.HasGenesis ? GetValidator(Nexus.RootStorage, block.Timestamp) : Nexus.GetGenesisAddress(Nexus.RootStorage);

            if (block.Validator != expectedValidator && !expectedValidator.IsNull)
            {
                throw new BlockGenerationException($"unexpected validator {block.Validator}, expected {expectedValidator}");
            }

            int txIndex = 0; 
            foreach (var tx in transactions)
            {
                byte[] result;
                try
                {
                    using (var m = new ProfileMarker("ExecuteTransaction"))
                    {
                        if (ExecuteTransaction(txIndex, tx, block.Timestamp, changeSet, block.Notify, oracle, minimumFee, out result))
                        {
                            if (result != null)
                            {
                                block.SetResultForHash(tx.Hash, result);
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
                    if (e.InnerException != null)
                    {
                        e = e.InnerException;
                    }

                    if (tx == null)
                    {
                        throw new BlockGenerationException(e.Message);
                    }

                    throw new InvalidTransactionException(tx.Hash, e.Message);
                }

                txIndex++;
            }

            // TODO avoid fetching this every time
            var expectedProtocol = Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);
            if (block.Protocol != expectedProtocol)
            {
                throw new BlockGenerationException($"invalid protocol number {block.Protocol}, expected protocol {expectedProtocol}");
            }

            using (var m = new ProfileMarker("CloseBlock"))
            {
                CloseBlock(block, changeSet);
            }

            if (oracle.Entries.Any())
            {
                block.MergeOracle(oracle);
            }

            return changeSet;
        }

        private bool ExecuteTransaction(int index, Transaction transaction, Timestamp time, StorageChangeSetContext changeSet, Action<Hash, Event> onNotify, OracleReader oracle, BigInteger minimumFee, out byte[] result)
        {
            result = null;

            RuntimeVM runtime;
            using (var m = new ProfileMarker("new RuntimeVM"))
                runtime = new RuntimeVM(index, transaction.Script, this, time, transaction, changeSet, oracle, false);
            runtime.MinimumFee = minimumFee;
            runtime.ThrowOnFault = true;

            ExecutionState state;
            using (var m = new ProfileMarker("runtime.Execute"))
                state = runtime.Execute();

            if (state != ExecutionState.Halt)
            {
                return false;
            }

            var cost = runtime.UsedGas;

            using (var m = new ProfileMarker("runtime.Events"))
            {
                foreach (var evt in runtime.Events)
                {
                    using (var m2 = new ProfileMarker(evt.ToString()))
                        onNotify(transaction.Hash, evt);
                }
            }

            if (runtime.Stack.Count > 0)
            {
                var obj = runtime.Stack.Pop();
                result = Serialization.Serialize(obj);
            }

            return true;
        }

        // NOTE should never be used directly from a contract, instead use Runtime.GetBalance!
        public BigInteger GetTokenBalance(StorageContext storage, IToken token, Address address)
        {
            if (token.Flags.HasFlag(TokenFlags.Fungible))
            {
                var balances = new BalanceSheet(token.Symbol);
                return balances.Get(storage, address);
            }
            else
            {
                var ownerships = new OwnershipSheet(token.Symbol);
                var items = ownerships.Get(storage, address);
                return items.Length;
            }
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

        public TokenContent ReadToken(StorageContext storage, string symbol, BigInteger tokenID)
        {
            var key = Nexus.GetKeyForNFT(symbol);
            var nftMap = new StorageMap(key, storage);

            Hash tokenHash = tokenID;
            if (!nftMap.ContainsKey<Hash>(tokenHash))
            {
                throw new ChainException($"nft {tokenID} / {symbol} does not exist");
            }

            return nftMap.Get<Hash, TokenContent>(tokenHash);
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
            var contract = Nexus.GetContractByName(storage, contractName);
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
            var oracle = Nexus.GetOracleReader();
            var changeSet = new StorageChangeSetContext(storage);
            var vm = new RuntimeVM(-1, script, this, time, null, changeSet, oracle, true);

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

        private byte[] GetContractDeploymentKey(Address contractAddress)
        {
            var bytes = Encoding.ASCII.GetBytes("deploy.");
            var key = ByteArrayUtils.ConcatBytes(bytes, contractAddress.ToByteArray());
            return key;
        }

        public bool IsContractDeployed(StorageContext storage, string name)
        {
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

            var key = GetContractDeploymentKey(contractAddress);
            return storage.Has(key);
        }

        private bool DeployContractScript(StorageContext storage, Address contractAddress, byte[] script)
        {
            var key = GetContractDeploymentKey(contractAddress);
            if (storage.Has(key))
            {
                return false;
            }

            storage.Put(key, script);

            var contractList = new StorageList(GetContractListKey(), storage);
            contractList.Add<Address>(contractAddress);                       

            return true;
        }

        public bool DeployNativeContract(StorageContext storage, Address contractAddress)
        {
            var contract = Nexus.GetContractByAddress(storage, contractAddress);
            if (contract == null)
            {
                return false;
            }

            DeployContractScript(storage, contractAddress, new byte[] { (byte)Opcode.RET });
            return true;
        }

        public bool DeployContract(StorageContext storage, byte[] script)
        {
            var contractAddress = Address.FromHash(script);
            DeployContractScript(storage, contractAddress, script);
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

            if (height > this.Height)
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
                var bytes =txMap.Get<Hash, byte[]>(hash);
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
            var addressTxMap = new StorageMap(AddressBlockHashMapTag, this.Storage);
            var addressList = addressTxMap.Get<Address, StorageList>(address);
            return addressList.All<Hash>();
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

        public void CloseBlock(Block block, StorageContext storage)
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

            var balance = new BalanceSheet(DomainSettings.FuelTokenSymbol);
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
    }
}
