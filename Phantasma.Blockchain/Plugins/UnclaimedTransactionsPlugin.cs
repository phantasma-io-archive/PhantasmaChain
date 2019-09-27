using Phantasma.Cryptography;
using Phantasma.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class UnclaimedTransactionsPlugin : IChainPlugin
    {
        private Dictionary<Address, HashSet<Hash>> _transactions = new Dictionary<Address, HashSet<Hash>>();

        public UnclaimedTransactionsPlugin()
        {
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            var evts = block.GetEventsForTransaction(transaction.Hash);

            foreach (var evt in evts)
            {
                switch (evt.Kind)                
                {
                    case EventKind.TokenSend:
                        {
                            var info = evt.GetContent<TokenEventData>();
                            if (info.chainAddress != chain.Address)
                            {
                                AddUnclaimedTransaction(evt.Address, transaction);
                            }

                            break;
                        }

                    case EventKind.TokenReceive:
                        {
                            var info = evt.GetContent<TokenEventData>();
                            if (info.chainAddress != chain.Address)
                            {
                                RemoveUnclaimedTransaction(evt.Address, transaction);
                            }

                            break;
                        }
                }
            }
        }

        private void AddUnclaimedTransaction(Address address, Transaction tx)
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

        private void RemoveUnclaimedTransaction(Address address, Transaction tx)
        {
            if (!_transactions.ContainsKey(address))
            {
                return;
            }

            var set = _transactions[address];
            set.Remove(tx.Hash);
        }

        public IEnumerable<Hash> GetAddressUnclaimedTransactions(Address address)
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
