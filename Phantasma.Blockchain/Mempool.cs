using System;
using System.Collections.Generic;
using System.Linq;
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

    public class Mempool: Runnable
    {
        private Dictionary<Hash, string> _hashMap = new Dictionary<Hash, string>();
        private Dictionary<string, List<MempoolEntry>> _entries = new Dictionary<string, List<MempoolEntry>>();

        private KeyPair _minerKeys;

        public Nexus Nexus { get; private set; }
        public Address MinerAddress => _minerKeys.Address;

        public static readonly int MaxExpirationTimeDifferenceInSeconds = 3600; // 1 hour

        public Mempool(KeyPair minerKeys, Nexus nexus)
        {
            this._minerKeys = minerKeys;
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
            lock (_entries)
            {
                foreach (var chainName in _entries.Keys)
                {
                    var chain = Nexus.FindChainByName(chainName);

                    var transactions = GetNextTransactions(chain);
                    if (transactions.Any())
                    {
                        var hashes = transactions.Select(tx => tx.Hash);
                        var block = new Block(chain.LastBlock.Height + 1, chain.Address, MinerAddress, Timestamp.Now, hashes, chain.LastBlock.Hash);
                        var success = chain.AddBlock(block, transactions);
                    }
                }

                return true;
            }
        }
    }
}
