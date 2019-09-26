using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct BombEntry
    {
        public BigInteger amount;
        public BigInteger round;
    }

    public sealed class BombContract : SmartContract
    {
        public override string Name => Nexus.BombContractName;

        public const string SESLeaderboardName = "ses";
        public const string BPLeaderboardName = "sesbp";

        internal StorageMap _entries;

        public BombContract() : base()
        {
        }

        public void Initialize(Address from)
        {
            Runtime.Expect(from == Runtime.Nexus.GenesisAddress, "must be genesis address");
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.CallContext(Nexus.RankingContractName, "CreateLeaderboard", this.Address, SESLeaderboardName, 100, 0);
            Runtime.CallContext(Nexus.RankingContractName, "CreateLeaderboard", this.Address, BPLeaderboardName, 10, 0);
        }

        public void OnReceive(Address from, string symbol, BigInteger amount)
        {
            if (!Runtime.Nexus.HasGenesis)
            {
                return;
            }

            Runtime.Expect(symbol == Nexus.FuelTokenSymbol, "cannot accept this token");
            Runtime.Expect(amount > 0 , "invalid amount");

            string leaderboardName = SESLeaderboardName;
            var leaderboard = (Leaderboard)Runtime.CallContext(Nexus.RankingContractName, "GetLeaderboard", leaderboardName).ToObject();

            BombEntry entry;
            if (_entries.ContainsKey<Address>(from))
            {
                entry = _entries.Get<Address, BombEntry>(from);
                if (entry.round > leaderboard.round)
                {
                    entry.amount = 0;
                    entry.round = leaderboard.round;
                }
                else
                {
                    Runtime.Expect(entry.round == leaderboard.round, "invalid round on bomb entry");
                }
            }
            else
            {
                entry = new BombEntry()
                {
                    amount = 0,
                    round = leaderboard.round
                };
            }

            entry.amount += amount;
            _entries.Set<Address, BombEntry>(from, entry);

            Runtime.CallContext(Nexus.RankingContractName, "InsertScore", this.Address, from, leaderboardName, entry.amount);
        }
    }
}
