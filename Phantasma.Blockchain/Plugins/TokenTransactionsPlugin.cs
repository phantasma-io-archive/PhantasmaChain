using Phantasma.Blockchain.Contracts.Native;
using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Plugins
{
    public class TokenTransactionsPlugin : INexusPlugin
    {
        public Nexus Nexus { get; private set; }

        private Dictionary<Token, HashSet<Hash>> _transactions = new Dictionary<Token, HashSet<Hash>>();

        public TokenTransactionsPlugin(Nexus nexus)
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
                if (evt.Kind == Contracts.EventKind.TokenReceive || evt.Kind == Contracts.EventKind.TokenSend)
                {
                    var info = evt.GetContent<TokenEventData>();
                    var token = Nexus.FindTokenBySymbol(info.symbol);
                    if (token != null)
                    {
                        RegisterTransaction(token, transaction);
                    }
                }
            }
        }

        private void RegisterTransaction(Token token, Transaction tx)
        {
            HashSet<Hash> set;
            if (_transactions.ContainsKey(token))
            {
                set = _transactions[token];
            }
            else
            {
                set = new HashSet<Hash>();
                _transactions[token] = set;
            }

            set.Add(tx.Hash);
        }

        public IEnumerable<Transaction> GetTokenTransactions(Token token)
        {
            if (_transactions.ContainsKey(token))
            {
                return _transactions[token].Select( hash => Nexus.FindTransactionByHash(hash));
            }
            else
            {
                return Enumerable.Empty<Transaction>();
            }

        }
    }
}
