using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phantasma.Core;
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

    public delegate void MempoolEventHandler(Transaction tx);

    public class Mempool: Runnable
    {
        public static readonly int MinimumBlockTime = 2; // in seconds
        public static readonly int MaxTransactionsPerBlock = 5000;

        private Dictionary<Hash, string> _hashMap = new Dictionary<Hash, string>();
        private HashSet<Hash> _pendingSet = new HashSet<Hash>();
        private Dictionary<string, List<MempoolEntry>> _entries = new Dictionary<string, List<MempoolEntry>>();

        // TODO this dictionary should not accumulate stuff forever, we need to have it cleaned once in a while
        private Dictionary<Hash, string> _rejections = new Dictionary<Hash, string>();

        private PhantasmaKeys _validatorKeys;

        public Nexus Nexus { get; private set; }
        public Address ValidatorAddress => _validatorKeys.Address;

        public static readonly int MaxExpirationTimeDifferenceInSeconds = 3600; // 1 hour

        public event MempoolEventHandler OnTransactionAdded;
        public event MempoolEventHandler OnTransactionDiscarded;
        public event MempoolEventHandler OnTransactionFailed;
        public event MempoolEventHandler OnTransactionCommitted;

        public uint MinimumProofOfWork => (uint)CalculateCurrentPoW() + defaultPoW;

        private int _size = 0;
        public int Size => _size;

        public BigInteger MinimumFee { get; private set; }

        public readonly int BlockTime; // in seconds
        private uint defaultPoW;

        public Mempool(PhantasmaKeys validatorKeys, Nexus nexus, int blockTime, BigInteger minimumFee, uint defaultPoW = 0)
        {
            Throw.If(blockTime < MinimumBlockTime, "invalid block time");

            this._validatorKeys = validatorKeys;
            this.Nexus = nexus;
            this.BlockTime = blockTime;
            this.MinimumFee = minimumFee;
            this.defaultPoW = defaultPoW;
        }

        private void RejectTransaction(Transaction tx, string reason)
        {
            lock (_rejections)
            {
                _rejections[tx.Hash] = reason;
            }

            throw new MempoolSubmissionException(reason);
        }

        public bool Submit(Transaction tx)
        {
            if (this.CurrentState != State.Running)
            {
                return false;
            }

           Throw.IfNull(tx, nameof(tx));

            var requiredPoW = MinimumProofOfWork;
            if (requiredPoW > 0 && tx.Hash.GetDifficulty() < requiredPoW)
            {
                RejectTransaction(tx, $"should be mined with difficulty of {requiredPoW} or more");
            }

            var chain = Nexus.GetChainByName(tx.ChainName);
            if (chain == null)
            {
                RejectTransaction(tx, "invalid chain name");
            }

            if (tx.Signatures == null || tx.Signatures.Length < 1)
            {
                RejectTransaction(tx, "at least one signature required");
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

            if (_hashMap.ContainsKey(tx.Hash))
            {
                throw new MempoolSubmissionException("already in mempool");
            }

            var entry = new MempoolEntry() { transaction = tx, timestamp = Timestamp.Now };

            List<MempoolEntry> list;

            lock (_entries)
            {
                if (_entries.ContainsKey(chain.Name))
                {
                    list = _entries[chain.Name];
                }
                else
                {
                    list = new List<MempoolEntry>();
                    _entries[chain.Name] = list;
                }

                list.Add(entry);
                _hashMap[tx.Hash] = chain.Name;
            }

            Interlocked.Increment(ref _size);
            OnTransactionAdded?.Invoke(tx);
            return true;
        }

        public bool Discard(Transaction tx)
        {
            if (this.CurrentState != State.Running)
            {
                return false;
            }

            if (_hashMap.ContainsKey(tx.Hash))
            {
                var chainName = _hashMap[tx.Hash];
                _hashMap.Remove(tx.Hash);

                lock (_entries)
                {
                    if (_entries.ContainsKey(chainName))
                    {
                        var list = _entries[chainName];
                        list.RemoveAll(x => x.transaction.Hash == tx.Hash);
                    }
                }

                Interlocked.Decrement(ref _size);
                OnTransactionDiscarded?.Invoke(tx);
                return true;
            }

            return false;
        }

        public IEnumerable<Transaction> GetTransactionsForChain(Chain chain)
        {
            if (_entries.ContainsKey(chain.Name))
            {
                return _entries[chain.Name].Select(x => x.transaction);
            }

            return Enumerable.Empty<Transaction>();
        }

        public IEnumerable<Transaction> GetTransactions()
        {
            var result = new List<Transaction>();
            foreach (var entry in _entries.Values)
            {
                result.AddRange( entry.Select(x => x.transaction));
            }

            return result;
        }

        // NOTE this is called inside a lock(_entries) block
        private List<Transaction> GetNextTransactions(Chain chain)
        {
            var list = _entries[chain.Name];
            if (list.Count == 0)
            {
                return null;
            }

            var currentTime = Timestamp.Now;
            List<Transaction> expiredTransactions = null;
            for (int i=0; i<list.Count; i++)
            {
                var entry = list[i];
                if (entry.transaction.Expiration < currentTime)
                {
                    if (expiredTransactions != null)
                    {
                        expiredTransactions = new List<Transaction>(list.Count);
                    }
                    expiredTransactions.Add(entry.transaction);
                }
            }

            if (expiredTransactions != null)
            {
                foreach (var tx in expiredTransactions)
                {
                    Discard(tx);
                }
            }

            var transactions = new List<Transaction>();

            while (transactions.Count < MaxTransactionsPerBlock && list.Count > 0)
            {
                var entry = list[0];
                list.RemoveAt(0);
                var tx = entry.transaction;
                transactions.Add(tx);
                _hashMap.Remove(tx.Hash);
                _pendingSet.Add(tx.Hash);
            }

            return transactions;
        }

        private HashSet<Chain> _pendingBlocks = new HashSet<Chain>();

        protected override bool Run()
        {
            Thread.Sleep(BlockTime * 1000);

            // we must be a staked validator to do something...
            if (!Nexus.IsPrimaryValidator(this.ValidatorAddress))
            {
                return true;
            }
            
            lock (_entries)
            {
                foreach (var chainName in _entries.Keys)
                {
                    var chain = Nexus.GetChainByName(chainName);

                    if (_pendingBlocks.Contains(chain))
                    {
                        continue;
                    }

                    // we must be the validator of the current epoch to do something with this chain...
                    if (chain.GetCurrentValidator(chain.Storage) != this.ValidatorAddress)
                    {
                        continue;
                    }

                    var lastBlockHash = chain.GetLastBlockHash();
                    var lastBlock = chain.GetBlockByHash(lastBlockHash);
                    var lastBlockTime = lastBlock != null ? lastBlock.Timestamp : new Timestamp(0);
                    var timeDiff = TimeSpan.FromSeconds(Timestamp.Now - lastBlockTime).TotalSeconds;
                    if (timeDiff < this.BlockTime)
                    {
                        continue;
                    }

                    var transactions = GetNextTransactions(chain);
                    if (transactions != null && transactions.Any())
                    {
                        lock (_pendingBlocks)
                        {
                            _pendingBlocks.Add(chain);
                        }
                        Task.Run(() => { MintBlock(transactions, chain); });
                    }
                }

                return true;
            }
        }

        private void MintBlock(List<Transaction> transactions, Chain chain)
        {
            var hashes = new HashSet<Hash>(transactions.Select(tx => tx.Hash));

            var lastBlockHash = chain.GetLastBlockHash();
            var lastBlock = chain.GetBlockByHash(lastBlockHash);
            var isFirstBlock = lastBlock == null;

            var protocol = (uint)Nexus.GetGovernanceValue(Nexus.RootStorage, Nexus.NexusProtocolVersionTag);

            while (hashes.Count > 0)
            {
                var block = new Block(isFirstBlock ? 1 : (lastBlock.Height + 1), chain.Address, Timestamp.Now, hashes, isFirstBlock ? Hash.Null : lastBlock.Hash, protocol);

                try
                {
                    chain.BakeBlock(ref block, ref transactions, MinimumFee, _validatorKeys, Timestamp.Now);
                    chain.AddBlock(block, transactions, MinimumFee);
                }
                catch (InvalidTransactionException e)
                {
                    var tx = transactions.First(x => x.Hash == e.Hash);
                    Interlocked.Decrement(ref _size);
                    hashes.Remove(e.Hash);

                    lock (_rejections)
                    {
                        _rejections[e.Hash] = e.Message;
                    }

                    transactions.Remove(tx);
                    OnTransactionFailed?.Invoke(tx);
                    continue;
                }

                lock (_entries)
                {
                    foreach (var tx in transactions)
                    {
                        _pendingSet.Remove(tx.Hash);
                    }
                }

                foreach (var tx in transactions)
                {
                    Interlocked.Decrement(ref _size);
                    OnTransactionCommitted?.Invoke(tx);
                }

                break;
            }

            lock (_pendingBlocks)
            {
                _pendingBlocks.Remove(chain);
            }
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

            lock (_entries)
            {
                if (_hashMap.ContainsKey(hash) || _pendingSet.Contains(hash))
                {
                    reason = null;
                    return MempoolTransactionStatus.Pending;
                }
            }

            reason = null;
            return MempoolTransactionStatus.Unknown;
        }

        public bool RejectTransaction(Hash hash)
        {
            lock (_entries)
            {
                if (_hashMap.ContainsKey(hash))
                {
                    var chainName = _hashMap[hash];
                    var list = _entries[chainName];
                    return list.RemoveAll(x => x.transaction.Hash == hash) > 0;
                }
            }

            return false;
        }

        private ProofOfWork CalculateCurrentPoW()
        {
            if (Size > 10000)
            {
                return ProofOfWork.Heavy;
            }
            else
            if (Size > 5000)
            {
                return ProofOfWork.Hard;
            }
            else
            if (Size > 1000)
            {
                return ProofOfWork.Moderate;
            }
            else
            if (Size > 500)
            {
                return ProofOfWork.Minimal;
            }
            else
            {
                return ProofOfWork.None;
            }
        }

    }
}
