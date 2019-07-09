using Phantasma.Blockchain.Tokens;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System;
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

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, from, Runtime.Chain.Address, amount), "transfer failed");

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
                unlockTime = Runtime.Time + TimeSpan.FromSeconds(duration),
            };
            list.Add(entry);
        }

        public void UnlockTokens(Address from, string symbol)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            Runtime.Expect(Runtime.Nexus.TokenExists(symbol), "invalid token");
            var tokenInfo = this.Runtime.Nexus.GetTokenInfo(symbol);
            Runtime.Expect(tokenInfo.Flags.HasFlag(TokenFlags.Fungible), "token must be fungible");

            Runtime.Expect(_entries.ContainsKey(from), "address not in vault");

            var list = _entries[from];

            BigInteger amount = 0;

            foreach (var entry in list)
            {
                if (entry.unlockTime <= Runtime.Time)
                {
                    amount += entry.amount;
                }
            }
            Runtime.Expect(amount > 0, "available amount must be greater than zero");

            list = list.Where(x => x.unlockTime > Runtime.Time).ToList();
            if (list.Count > 0)
            {
                _entries[from] = list;
            }
            else
            {
                _entries.Remove(from);
            }

            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, symbol, Runtime.Chain.Address, from, amount), "transfer failed");
        }
    }
}
