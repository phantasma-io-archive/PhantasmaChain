using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class AddressTransactionsPlugin : INexusPlugin
    {
        public Nexus Nexus { get; private set; }

        private Dictionary<Address, HashSet<Hash>> _transactions = new Dictionary<Address, HashSet<Hash>>();

        public AddressTransactionsPlugin(Nexus nexus)
        {
            this.Nexus = nexus;
        }

        public void OnNewBlock(Chain chain, Block block)
        {
        }

        public void OnNewChain(Chain chain)
        {
        }

        public void OnNewTransaction(Chain chain, Block block, Transaction transaction)
        {
            var evts = block.GetEventsForTransaction(transaction.Hash);

            foreach (var evt in evts)
            {
                RegisterTransaction(evt.Address, transaction);
            }
        }

        private void RegisterTransaction(Address address, Transaction tx)
        {
            HashSet<Hash> set;
            if (_transactions.ContainsKey(address))
            {
                set = _transactions[address];
            }
            else
            {
                set = new HashSet<Hash>();
                _transactions[address] = set;
            }

            set.Add(tx.Hash);
        }

        public IEnumerable<Transaction> GetAddressTransactions(Address address)
        {
            if (_transactions.ContainsKey(address))
            {
                return _transactions[address].Select( hash => Nexus.FindTransactionByHash(hash));
            }
            else
            {
                return Enumerable.Empty<Transaction>();
            }

        }
    }
}
