using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class ChainAddressesPlugin : IChainPlugin
    {
        private Dictionary<Address, HashSet<Address>> _transactions = new Dictionary<Address, HashSet<Address>>();

        public ChainAddressesPlugin()
        {
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            var evts = block.GetEventsForTransaction(transaction.Hash);

            foreach (var evt in evts)
            {
                RegisterTransaction(chain, evt.Address);
            }
        }

        private void RegisterTransaction(Chain chain, Address address)
        {
            HashSet<Address> set;
            if (_transactions.ContainsKey(chain.Address))
            {
                set = _transactions[chain.Address];
            }
            else
            {
                set = new HashSet<Address>();
                _transactions[chain.Address] = set;
            }

            set.Add(address);
        }

        public IEnumerable<Address> GetChainAddresses(Chain chain)
        {
            if (_transactions.ContainsKey(chain.Address))
            {
                return _transactions[chain.Address];
            }
            else
            {
                return Enumerable.Empty<Address>();
            }

        }
    }
}
