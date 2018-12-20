using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class AddressTransactionsPlugin : IChainPlugin
    {
        private Dictionary<Address, HashSet<Hash>> _transactions = new Dictionary<Address, HashSet<Hash>>();

        public AddressTransactionsPlugin()
        {
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
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

        public IEnumerable<Hash> GetAddressTransactions(Address address)
        {
            if (_transactions.ContainsKey(address))
            {
                return _transactions[address];
            }
            else
            {
                return Enumerable.Empty<Hash>();
            }

        }
    }
}
