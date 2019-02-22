using Phantasma.Blockchain.Tokens;
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

    public sealed class VaultContract : SmartContract
    {
        public override string Name => "vault";

        private Dictionary<Address, List<VaultEntry>> _entries = new Dictionary<Address, List<VaultEntry>>();

        public VaultContract() : base()
        {
        }

        public void LockTokens(Address from, string symbol, BigInteger amount, uint duration)
        {
            Runtime.Expect(amount > 0, "amount must be greater than zero");
            Runtime.Expect(duration >= 86400, "minimum duration should be one day"); // minimum 1 day
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            var balances = this.Runtime.Chain.GetTokenBalances(token);
            Runtime.Expect(token.Transfer(this.Storage, balances, from, Runtime.Chain.Address, amount), "transfer failed");

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
            Runtime.Expect(IsWitness(from), "invalid witness");

            var token = this.Runtime.Nexus.FindTokenBySymbol(symbol);
            Runtime.Expect(token != null, "invalid token");
            Runtime.Expect(token.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(_entries.ContainsKey(from), "address not in vault");

            var list = _entries[from];

            BigInteger amount = 0;

            foreach (var entry in list)
            {
                if (entry.unlockTime <= Runtime.Block.Timestamp.Value)
                {
                    amount += entry.amount;
                }
            }
            Runtime.Expect(amount > 0, "available amount must be greater than zero");

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
            Runtime.Expect(token.Transfer(this.Storage, balances, Runtime.Chain.Address, from, amount), "transfer failed");
        }
    }
}
