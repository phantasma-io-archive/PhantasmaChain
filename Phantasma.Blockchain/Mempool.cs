using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phantasma.Core;
using Phantasma.Core.Types;
using Phantasma.Cryptography;

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
        public static readonly int MaxTransactionsPerBlock = 20000;

        private Dictionary<Hash, string> _hashMap = new Dictionary<Hash, string>();
        private Dictionary<string, List<MempoolEntry>> _entries = new Dictionary<string, List<MempoolEntry>>();

        // TODO this dictionary should not accumulate stuff forever, we need to have it cleaned once in a while
        private Dictionary<Hash, string> _rejections = new Dictionary<Hash, string>();

        private KeyPair _validatorKeys;

        public Nexus Nexus { get; private set; }
        public Address ValidatorAddress => _validatorKeys.Address;

        public static readonly int MaxExpirationTimeDifferenceInSeconds = 3600; // 1 hour

        public event MempoolEventHandler OnTransactionAdded;
        public event MempoolEventHandler OnTransactionRemoved;
        public event MempoolEventHandler OnTransactionFailed;

        private int _size = 0;
        public int Size => _size;

        public readonly int BlockTime; // in seconds

        public Mempool(KeyPair validatorKeys, Nexus nexus, int blockTime)
        {
            Throw.If(blockTime < MinimumBlockTime, "invalid block time");

            this._validatorKeys = validatorKeys;
            this.Nexus = nexus;
            this.BlockTime = blockTime;
        }

        public void Submit(Transaction tx)
        {
           Throw.IfNull(tx, nameof(tx));

            var chain = Nexus.FindChainByName(tx.ChainName);
            Throw.IfNull(chain, nameof(chain));

            if (_hashMap.ContainsKey(tx.Hash))
            {
                throw new MempoolSubmissionException("already in mempool");
            }

            var currentTime = Timestamp.Now;
            if (tx.Expiration <= currentTime)
            {
                throw new MempoolSubmissionException("already expired");
            }

            var diff = tx.Expiration - currentTime;
            if (diff > MaxExpirationTimeDifferenceInSeconds)
            {
                throw new MempoolSubmissionException("expire date too big");
            }

            if (tx.NexusName != this.Nexus.Name)
            {
                throw new MempoolSubmissionException("invalid nexus name");
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
        }

        public bool Discard(Transaction tx)
        {
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
                OnTransactionRemoved?.Invoke(tx);
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

        private List<Transaction> GetNextTransactions(Chain chain)
        {
            var list = _entries[chain.Name];
            if (list.Count == 0)
            {
                return null;
            }

            var currentTime = Timestamp.Now;
            list.RemoveAll(entry => entry.transaction.Expiration < currentTime);

            var transactions = new List<Transaction>();

            while (transactions.Count < MaxTransactionsPerBlock && list.Count > 0)
            {
                var entry = list[0];
                list.RemoveAt(0);
                var tx = entry.transaction;
                transactions.Add(tx);
                _hashMap.Remove(tx.Hash);
            }

            return transactions;
        }

        private HashSet<Chain> _pendingBlocks = new HashSet<Chain>();

        protected override bool Run()
        {
            // we must be a staked validator to do something...
            if (!Nexus.IsValidator(this.ValidatorAddress))
            {
                Thread.Sleep(1000);
                return true;
            }
            
            lock (_entries)
            {
                foreach (var chainName in _entries.Keys)
                {
                    var chain = Nexus.FindChainByName(chainName);

                    if (_pendingBlocks.Contains(chain))
                    {
                        continue;
                    }

                    // we must be the validator of the current epoch to do something with this chain...
                    if (!chain.IsCurrentValidator(this.ValidatorAddress))
                    {
                        continue;
                    }

                    var lastBlockTime = chain.LastBlock != null ? chain.LastBlock.Timestamp : new Timestamp(0);
                    var timeDiff = TimeSpan.FromSeconds(Timestamp.Now - lastBlockTime).TotalSeconds;
                    if (timeDiff < this.BlockTime)
                    {
                        continue;
                    }

                    var transactions = GetNextTransactions(chain);
                    if (transactions != null)
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

            var isFirstBlock = chain.LastBlock == null;

            while (hashes.Count > 0)
            {
                var block = new Block(isFirstBlock ? 1 : (chain.LastBlock.Height + 1), chain.Address, Timestamp.Now, hashes, isFirstBlock ? Hash.Null : chain.LastBlock.Hash);

                try
                {
                    chain.AddBlock(block, transactions);
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

                    OnTransactionFailed?.Invoke(tx);
                    continue;
                }

                foreach (var tx in transactions)
                {
                    Interlocked.Decrement(ref _size);
                    OnTransactionRemoved?.Invoke(tx);
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

            if (_hashMap.ContainsKey(hash))
            {
                reason = null;
                return MempoolTransactionStatus.Pending;
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
    }
}
