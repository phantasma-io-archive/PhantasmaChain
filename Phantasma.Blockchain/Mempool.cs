using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Phantasma.Core;
using Phantasma.Core.Log;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;

namespace Phantasma.Blockchain
{
    public struct MempoolEntry
    {
        public Transaction transaction;
        public Timestamp timestamp;
    }

    public class MempoolSubmissionException: Exception
    {
        public MempoolSubmissionException(string msg): base(msg)
        {

        }
    }

    public enum MempoolTransactionStatus
    {
        Unknown,
        Pending,
        Rejected,
    }

    public delegate void MempoolEventHandler(Hash hash);

    public class ChainPool
    {
        public readonly Mempool Mempool;
        public readonly Chain Chain;
        public Nexus Nexus => Mempool.Nexus;

        public bool Busy => _txMap.Count > 0 || _pending.Count > 0;

        private object _phone = new object();

        private Dictionary<Hash, MempoolEntry> _txMap = new Dictionary<Hash, MempoolEntry>();
        private HashSet<Hash> _pending = new HashSet<Hash>();

        public ChainPool(Mempool mempool, Chain chain)
        {
            this.Mempool = mempool;
            this.Chain = chain;
        }

        public bool IsEnabled()
        {
            // TODO support for calculation of proper validator
            return true;
        }

        private ProofOfWork CalculateCurrentPoW()
        {
            int size  = _txMap.Count;

            if (size > 10000)
            {
                return ProofOfWork.Heavy;
            }
            else
            if (size > 5000)
            {
                return ProofOfWork.Hard;
            }
            else
            if (size > 1000)
            {
                return ProofOfWork.Moderate;
            }
            else
            if (size > 500)
            {
                return ProofOfWork.Minimal;
            }
            else
            {
                return ProofOfWork.None;
            }
        }

        public bool Submit(Transaction tx)
        {
            lock (_txMap)
            {
                var requiredPoW = (uint)CalculateCurrentPoW() + Mempool.DefaultPoW;

                if (requiredPoW > 0 && tx.Hash.GetDifficulty() < requiredPoW)
                {
                    Mempool.RejectTransaction(tx, $"should be mined with difficulty of {requiredPoW} or more");
                }

                if (_txMap.ContainsKey(tx.Hash))
                {
                    throw new MempoolSubmissionException("already in mempool");
                }

                var entry = new MempoolEntry() { transaction = tx, timestamp = Timestamp.Now };
                _txMap[tx.Hash] = entry;
            }

            this.Mempool.OnTransactionAdded?.Invoke(tx.Hash);

            var lastBlockHash = Chain.GetLastBlockHash();
            var lastBlock = Chain.GetBlockByHash(lastBlockHash);
            var lastBlockTime = lastBlock != null ? lastBlock.Timestamp : new Timestamp(0);
            var timeDiff = TimeSpan.FromSeconds(Timestamp.Now - lastBlockTime).TotalSeconds;
            if (timeDiff >= Mempool.BlockTime / 2)
            {
                this.AwakeUp();
            }

            return true;
        }

        public bool Discard(Hash hash)
        {
            lock (_txMap)
            {
                if (_txMap.ContainsKey(hash))
                {
                    _txMap.Remove(hash);
                    this.Mempool.OnTransactionDiscarded?.Invoke(hash);
                    return true;
                }
            }

            return false;
        }

        public void AwakeUp()
        {
            lock (_phone)
            {
                Monitor.Pulse(_phone);
            }
        }

        internal void Run()
        {
            lock (_phone)
            {
                while (Mempool.Running)
                {
                    Monitor.Wait(_phone);

                    if (!IsEnabled())
                    {
                        continue;
                    }

                    var nexus = Mempool.Nexus;

                    // we must be a staked validator to do something...
                    if (!nexus.HasGenesis)
                    {
                        continue;
                    }

                    /*
                    // we must be the validator of the current epoch to do something with this chain...
                    if (Chain.GetCurrentValidator(Chain.Storage) != Mempool.ValidatorAddress)
                    {
                        return true;
                    }*/

                    List<Hash> expiredHashes = null;
                    List<Transaction> readyTransactions = null;

                    var currentTime = Timestamp.Now;

                    lock (_txMap)
                    {
                        foreach (var entry in _txMap.Values)
                        {
                            if (entry.transaction.Expiration < currentTime)
                            {
                                if (expiredHashes == null)
                                {
                                    expiredHashes = new List<Hash>();
                                }

                                expiredHashes.Add(entry.transaction.Hash);

                                continue;
                            }

                            if (readyTransactions == null)
                            {
                                readyTransactions = new List<Transaction>();
                            }

                            readyTransactions.Add(entry.transaction);

                            if (readyTransactions.Count >= Mempool.MaxTransactionsPerBlock)
                            {
                                break;
                            }
                        }
                    }


                    if (expiredHashes != null)
                    {
                        foreach (var hash in expiredHashes)
                        {
                            Discard(hash);
                        }
                    }

                    if (readyTransactions != null)
                    {

                        lock (_txMap)
                        {
                            lock (_pending)
                            {
                                foreach (var tx in readyTransactions)
                                {
                                    _txMap.Remove(tx.Hash);
                                    _pending.Add(tx.Hash);
                                }
                            }
                        }

                        //var readyToMint = timeDiff >= Mempool.BlockTime * 2 || readyTransactions.Count >= 3;
                        MintBlock(readyTransactions);
                    }
                }
            }
        }

        private void MintBlock(List<Transaction> transactions)
        {
            var lastBlockHash = Chain.GetLastBlockHash();
            var lastBlock = Chain.GetBlockByHash(lastBlockHash);
            var isFirstBlock = lastBlock == null;

            var protocol = (uint)Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);

            var minFee = Mempool.MinimumFee;

            Mempool.Logger.Message($"Minting new block with {transactions.Count} potential transactions");

            while (transactions.Count > 0)
            {
                var block = new Block(isFirstBlock ? 1 : (lastBlock.Height + 1), Chain.Address, Timestamp.Now, transactions.Select(x => x.Hash), isFirstBlock ? Hash.Null : lastBlock.Hash, protocol);

                try
                {
                    Chain.ValidateBlock(block, transactions, minFee);
                }
                catch (InvalidTransactionException e)
                {
                    int index = -1;

                    for (int i=0; i<transactions.Count; i++)
                    {
                        if (transactions[i].Hash == e.Hash)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index >= 0)
                    {
                        transactions.RemoveAt(index);
                    }

                    lock (_pending)
                    {
                        _pending.Remove(e.Hash);
                    }

                    Mempool.RegisterRejectionReason(e.Hash, e.Message);
                    Mempool.OnTransactionFailed?.Invoke(e.Hash);
                    continue;
                }

                try
                {
                    Chain.BakeBlock(ref block, ref transactions, minFee, Mempool.ValidatorKeys, Timestamp.Now);
                    Chain.AddBlock(block, transactions, minFee);
                }
                catch (Exception e)
                {
                    Mempool.Logger.Error(e.ToString());
                }

                lock (_pending)
                {
                    _pending.Clear();
                }

                foreach (var tx in transactions)
                {
                    Mempool.OnTransactionCommitted?.Invoke(tx.Hash);
                }

                return;
            }
        }

        public bool ContainsTransaction(Hash hash)
        {
            lock (_txMap)
            {
                if (_txMap.ContainsKey(hash))
                {
                    return true;
                }
            }

            lock (_pending)
            {
                if (_pending.Contains(hash))
                {
                    return true;
                }
            }

            return false;
        }

        public List<Transaction> GetTransactions()
        {
            var transactions = new List<Transaction>();
            lock (_txMap)
            {
                foreach (var entry in _txMap.Values)
                {
                    transactions.Add(entry.transaction);
                }
            }

            return transactions;
        }
    }

    public class Mempool : Runnable
    {
        public bool Running => CurrentState == State.Running;

        public static readonly int MinimumBlockTime = 2; // in seconds
        public static readonly int MaxTransactionsPerBlock = 5000;

        // TODO this dictionary should not accumulate stuff forever, we need to have it cleaned once in a while
        private Dictionary<Hash, string> _rejections = new Dictionary<Hash, string>();

        private Dictionary<string, ChainPool> _chains = new Dictionary<string, ChainPool>();

        public Nexus Nexus { get; private set; }

        internal PhantasmaKeys ValidatorKeys { get; private set; }
        public Address ValidatorAddress => ValidatorKeys.Address;

        public static readonly int MaxExpirationTimeDifferenceInSeconds = 3600; // 1 hour

        public MempoolEventHandler OnTransactionAdded;
        public MempoolEventHandler OnTransactionDiscarded;
        public MempoolEventHandler OnTransactionFailed;
        public MempoolEventHandler OnTransactionCommitted;

        public BigInteger MinimumFee { get; private set; }

        public readonly int BlockTime; // in seconds
        public readonly uint DefaultPoW;

        public Logger Logger { get; }

        public Mempool(PhantasmaKeys validatorKeys, Nexus nexus, int blockTime, BigInteger minimumFee, uint defaultPoW = 0, Logger logger = null)
        {
            Throw.If(blockTime < MinimumBlockTime, "invalid block time");

            this.ValidatorKeys = validatorKeys;
            this.Nexus = nexus;
            this.BlockTime = blockTime;
            this.MinimumFee = minimumFee;
            this.DefaultPoW = defaultPoW;
            this.Logger = logger;

            Logger?.Message($"Starting mempool with block time of {blockTime} seconds.");
        }

        public void SetKeys(PhantasmaKeys keys)
        {
            this.ValidatorKeys = keys;
        }

        internal void RejectTransaction(Transaction tx, string reason)
        {
            RejectTransaction(tx.Hash, reason);
        }

        internal void RejectTransaction(Hash hash, string reason)
        {
            RegisterRejectionReason(hash, reason);
            throw new MempoolSubmissionException(reason);
        }

        internal void RegisterRejectionReason(Hash hash, string reason)
        {
            lock (_rejections)
            {
                _rejections[hash] = reason;
            }
        }

        public bool Submit(Transaction tx)
        {
            if (!Running)
            {
                return false;
            }

           Throw.IfNull(tx, nameof(tx));

            var chain = Nexus.GetChainByName(tx.ChainName);
            if (chain == null)
            {
                RejectTransaction(tx, "invalid chain name");
            }

            if (tx.Signatures == null || tx.Signatures.Length < 1)
            {
                RejectTransaction(tx, "at least one signature required");
            }

            if (tx.Payload.Length < 4)
            {
                RejectTransaction(tx, "expected payload identifier");

            }

            var currentTime = Timestamp.Now;
            if (tx.Expiration <= currentTime)
            {
                RejectTransaction(tx, "already expired");
            }

            var diff = tx.Expiration - currentTime;
            if (diff > MaxExpirationTimeDifferenceInSeconds)
            {
                RejectTransaction(tx, "expire date too big");
            }

            if (tx.NexusName != this.Nexus.Name)
            {
                RejectTransaction(tx, "invalid nexus name");
            }

            var chainPool = GetPoolForChain(chain);
            chainPool.Submit(tx);
            return true;
        }

        private ChainPool GetPoolForChain(Chain chain)
        {
            ChainPool chainPool;

            lock (_chains)
            {
                if (_chains.ContainsKey(chain.Name))
                {
                    chainPool = _chains[chain.Name];
                }
                else
                {
                    chainPool = new ChainPool(this, chain);
                    _chains[chain.Name] = chainPool;

                    new Thread(() =>
                    {
                        chainPool.Run();
                    }).Start();
                }
            }

            return chainPool;
        }

        public bool Discard(Hash hash)
        {
            lock (_chains)
            {
                foreach (var chain in _chains.Values)
                {
                    if (chain.ContainsTransaction(hash))
                    {
                        chain.Discard(hash);
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Discard(Transaction tx)
        {
            if (!Running)
            {
                return false;
            }

            var chain = Nexus.GetChainByName(tx.ChainName);
            if (chain == null)
            {
                return false;
            }

            var chainPool = GetPoolForChain(chain);
            return chainPool.Discard(tx.Hash);
        }

        public MempoolTransactionStatus GetTransactionStatus(Hash hash, out string reason)
        {
            lock (_rejections)
            {
                if (_rejections.ContainsKey(hash))
                {
                    reason = _rejections[hash];
                    return MempoolTransactionStatus.Rejected;
                }
            }

            lock (_chains)
            {
                foreach (var pool in _chains.Values)
                {
                    if (pool.ContainsTransaction(hash))
                    {
                        reason = null;
                        return MempoolTransactionStatus.Pending;
                    }
                }
            }

            reason = null;
            return MempoolTransactionStatus.Unknown;
        }

        protected override bool Run()
        {
            Thread.Sleep(BlockTime * 1000);

            lock (_chains)
            {
                foreach (var pool in _chains.Values)
                {
                    pool.AwakeUp();
                }
            }

            return true;
        }

        public List<Transaction> GetTransactions()
        {
            var transactions = new List<Transaction>();

            lock (_chains)
            {
                foreach (var pool in _chains.Values)
                {
                    transactions.AddRange(pool.GetTransactions());
                }
            }

            return transactions;
        }

        public uint GettMinimumProofOfWork()
        {
            uint min = DefaultPoW;

            /*
            lock (_chains)
            {
                foreach (var pool in _chains.Values)
                {
                }
            }*/

            return min;
        }

        public bool IsEmpty()
        {
            lock (_chains)
            {
                foreach (var pool in _chains.Values)
                {
                    if (pool.Busy)
                    {
                        return false;
                    }
                }
            }

            return true;
       }
    }
}
