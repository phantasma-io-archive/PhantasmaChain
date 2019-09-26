using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct LeaderboardRow
    {
        public Address address;
        public BigInteger score;
    }

    public struct Leaderboard
    {
        public string name;
        public Address owner;
        public BigInteger size;
        public BigInteger period;
        public BigInteger round;
    }

    public sealed class RankingContract : SmartContract
    {
        public override string Name => Nexus.RankingContractName;

        internal StorageMap _leaderboards; // name, Leaderboard
        internal StorageMap _rows; // name, List<LeaderboardEntry>

        public RankingContract() : base()
        {
        }

        public void CreateLeaderboard(Address from, string name, BigInteger size, BigInteger period)
        {
            Runtime.Expect(size >= 5, "size invalid");
            Runtime.Expect(size <= 1000, "size too large");

            Runtime.Expect(!from.IsInterop, "address cannot be interop");

            Runtime.Expect(!_leaderboards.ContainsKey<string>(name), "leaderboard already exists");

            if (period != 0)
            {
                Runtime.Expect(period >= 60 * 30, "period invalid");
            }

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(ValidationUtils.ValidateName(name), "invalid name");

            var leaderboard = new Leaderboard()
            {
                name = name,
                owner = from,
                size = size,
                period = period,
                round = 0,
            };
            _leaderboards.Set<string, Leaderboard>(name, leaderboard);

            Runtime.Notify(EventKind.LeaderboardCreate, from, name);
        }

        public Leaderboard GetLeaderboard(string name)
        {
            Runtime.Expect(_leaderboards.ContainsKey<string>(name), "invalid leaderboard");
            return _leaderboards.Get<string, Leaderboard>(name);
        }

        public void InsertScore(Address from, Address target, string name, BigInteger score)
        {
            Runtime.Expect(_leaderboards.ContainsKey<string>(name), "invalid leaderboard");
            var leaderboard = _leaderboards.Get<string, Leaderboard>(name);

            Runtime.Expect(from == leaderboard.owner, "invalid leaderboard owner");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            var newRow = new LeaderboardRow()
            {
                address = target,
                score = score
            };

            int bestIndex = 0;

            var lastIndex = (int)(count - 1);
            for (int i = lastIndex; i >= 0; i--)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.score >= score)
                {
                    bestIndex = i + 1;
                    break;
                }
            }

            if (bestIndex >= leaderboard.size)
            {
                return;
            }

            for (int i = lastIndex; i > bestIndex; i--)
            {
                var entry = rows.Get<LeaderboardRow>(i - 1);
                rows.Replace<LeaderboardRow>(i, entry);
            }

            if (bestIndex < count)
            {
                rows.Replace(bestIndex, newRow);
            }
            else
            {
                Runtime.Expect(bestIndex == count, "invalid insertion index");
                rows.Add<LeaderboardRow>(newRow);
            }

            Runtime.Notify(EventKind.LeaderboardInsert, from, newRow);
        }
    }
}
