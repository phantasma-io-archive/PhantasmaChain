using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Cryptography;
using Phantasma.Domain;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class TokenTransactionsPlugin : IChainPlugin
    {
        private Dictionary<string, HashSet<Hash>> _transactions = new Dictionary<string, HashSet<Hash>>();

        public TokenTransactionsPlugin()
        {
        }

        public override void OnTransaction(Chain chain, Block block, Transaction transaction)
        {
            var evts = block.GetEventsForTransaction(transaction.Hash);

            foreach (var evt in evts)
            {
                if (evt.Kind == EventKind.TokenReceive || evt.Kind == EventKind.TokenSend)
                {
                    var info = evt.GetContent<TokenEventData>();
                    RegisterTransaction(info.symbol, transaction);
                }
            }
        }

        private void RegisterTransaction(string symbol, Transaction tx)
        {
            HashSet<Hash> set;
            if (_transactions.ContainsKey(symbol))
            {
                set = _transactions[symbol];
            }
            else
            {
                set = new HashSet<Hash>();
                _transactions[symbol] = set;
            }

            set.Add(tx.Hash);
        }

        public IEnumerable<Hash> GetTokenTransactions(string symbol)
        {
            if (_transactions.ContainsKey(symbol))
            {
                return _transactions[symbol];
            }
            else
            {
                return Enumerable.Empty<Hash>();
            }

        }
    }
}
