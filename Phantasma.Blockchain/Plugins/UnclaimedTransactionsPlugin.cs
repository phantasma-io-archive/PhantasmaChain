using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class UnclaimedTransactionsPlugin : INexusPlugin
    {
        public Nexus Nexus { get; private set; }

        private Dictionary<Address, HashSet<Hash>> _transactions = new Dictionary<Address, HashSet<Hash>>();

        public UnclaimedTransactionsPlugin(Nexus nexus)
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
            foreach (var evt in transaction.Events)
            {
                switch (evt.Kind)                
                {
                    case EventKind.TokenSend:
                        {
                            var info = evt.GetContent<TokenEventData>();
                            var token = Nexus.FindTokenBySymbol(info.symbol);
                            if (token != null && info.chainAddress != chain.Address)
                            {
                                AddUnclaimedTransaction(evt.Address, transaction);
                            }

                            break;
                        }

                    case EventKind.TokenReceive:
                        {
                            var info = evt.GetContent<TokenEventData>();
                            var token = Nexus.FindTokenBySymbol(info.symbol);
                            if (token != null && info.chainAddress != chain.Address)
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

        public IEnumerable<Transaction> GetAddressUnclaimedTransactions(Address address)
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
