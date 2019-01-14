using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public delegate void MempoolEventHandler(Transaction tx);

    public class Mempool: Runnable
    {
        private Dictionary<Hash, string> _hashMap = new Dictionary<Hash, string>();
        private Dictionary<string, List<MempoolEntry>> _entries = new Dictionary<string, List<MempoolEntry>>();

        private KeyPair _validatorKeys;

        public Nexus Nexus { get; private set; }
        public Address ValidatorAddress => _validatorKeys.Address;

        public static readonly int MaxExpirationTimeDifferenceInSeconds = 3600; // 1 hour

        public event MempoolEventHandler OnTransactionAdded;
        public event MempoolEventHandler OnTransactionRemoved;
        public event MempoolEventHandler OnTransactionFailed;

        private int _size = 0;
        public int Size => _size;

        public Mempool(KeyPair validatorKeys, Nexus nexus)
        {
            this._validatorKeys = validatorKeys;
            this.Nexus = nexus;
        }

        public bool Submit(Transaction tx, Func<Transaction, bool> validator = null)
        {
           Throw.IfNull(tx, nameof(tx));

            var chain = Nexus.FindChainByName(tx.ChainName);
            Throw.IfNull(chain, nameof(chain));

            if (_hashMap.ContainsKey(tx.Hash))
            {
                return false;
            }

            var currentTime = Timestamp.Now;
            if (tx.Expiration <= currentTime)
            {
                return false;
            }

            var diff = tx.Expiration - currentTime;
            if (diff > MaxExpirationTimeDifferenceInSeconds)
            {
                return false;
            }

            if (validator != null)
            {
                if (!validator(tx))
                {
                    return false;
                }
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

        private IEnumerable<Transaction> GetNextTransactions(Chain chain)
        {
            var list = _entries[chain.Name];
            if (list.Count == 0)
            {
                return Enumerable.Empty<Transaction>();
            }

            var currentTime = Timestamp.Now;
            list.RemoveAll(entry => entry.transaction.Expiration < currentTime);

            var transactions = new List<Transaction>();

            while (transactions.Count < 20 && list.Count > 0)
            {
                var entry = list[0];
                list.RemoveAt(0);
                transactions.Add(entry.transaction);
            }

            return transactions;
        }

        protected override bool Run()
        {
            // we must be a staked validator to do something...
            if (!Nexus.IsValidator(this.ValidatorAddress))
            {
                return false;
            }

            lock (_entries)
            {
                foreach (var chainName in _entries.Keys)
                {
                    var chain = Nexus.FindChainByName(chainName);

                    // we must be the validator of the current epoch to do something with this chain...
                    if (!chain.IsCurrentValidator(this.ValidatorAddress))
                    {
                        continue;
                    }

                    var transactions = GetNextTransactions(chain);
                    if (transactions.Any())
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
                    }
                }

                return true;
            }
        }
    }
}
