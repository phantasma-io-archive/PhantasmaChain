using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Blockchain.Contracts.Native
{
    internal struct VaultEntry
    {
        public BigInteger amount;
        public uint unlockTime;
    }

    public sealed class VaultContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Vault;

        private Dictionary<Address, List<VaultEntry>> _entries = new Dictionary<Address, List<VaultEntry>>();

        public VaultContract() : base()
        {
        }

        public void LockTokens(Address from, string symbol, BigInteger amount, uint duration)
        {
            Expect(amount > 0);
            Expect(duration >= 86400); // minimum 1 day
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(balances.Subtract(from, amount));

            List<VaultEntry> list;

            if (_entries.ContainsKey(from))
            {
                list = _entries[from];
            }
            else
            {
                list = new List<VaultEntry>();
                _entries[from] = list;
            }

            var entry = new VaultEntry()
            {
                amount = amount,
                unlockTime = Runtime.Block.Timestamp.Value + duration,
            };
            list.Add(entry);
        }

        public void UnlockTokens(Address from, string symbol)
        {
            Expect(IsWitness(from));

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Expect(token != null);

            Expect(_entries.ContainsKey(from));

            var list = _entries[from];

            BigInteger amount = 0;

            foreach (var entry in list)
            {
                if (entry.unlockTime <= Runtime.Block.Timestamp.Value)
                {
                    amount += entry.amount;
                }
            }
            Expect(amount > 0);

            list = list.Where(x => x.unlockTime > Runtime.Block.Timestamp.Value).ToList();
            if (list.Count > 0)
            {
                _entries[from] = list;
            }
            else
            {
                _entries.Remove(from);
            }

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Expect(balances.Add(from, amount));
        }
    }
}
