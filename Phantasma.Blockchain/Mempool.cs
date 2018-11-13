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
        public Chain chain;
        public Transaction transaction;
        public Timestamp timestamp;
    }

    public class Mempool
    {
        private Dictionary<Hash, string> _hashMap = new Dictionary<Hash, string>();
        private Dictionary<string, List<MempoolEntry>> _entries = new Dictionary<string, List<MempoolEntry>>();

        public bool Submit(Chain chain, Transaction tx, Func<Transaction, bool> validator = null)
        {
            Throw.IfNull(chain, nameof(chain));
            Throw.IfNull(tx, nameof(tx));

            if (_hashMap.ContainsKey(tx.Hash))
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

            var entry = new MempoolEntry() { chain = chain, transaction = tx, timestamp = Timestamp.Now };

            List<MempoolEntry> list;

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

            return true;
        }

        public bool Discard(Transaction tx)
        {
            if (_hashMap.ContainsKey(tx.Hash))
            {
                var chainName = _hashMap[tx.Hash];
                _hashMap.Remove(tx.Hash);

                if (_entries.ContainsKey(chainName))
                {
                    var list = _entries[chainName];
                    list.RemoveAll(x => x.transaction.Hash == tx.Hash);
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
    }
}
