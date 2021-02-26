using System.Numerics;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Storage.Context;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class RankingContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Ranking;

        internal StorageMap _leaderboards; // name, Leaderboard
        internal StorageMap _rows; // name, List<LeaderboardEntry>

        public RankingContract() : base()
        {
        }

        public bool Exists(string name)
        {
            return _leaderboards.ContainsKey<string>(name);
        }

        public void CreateLeaderboard(Address from, string name, BigInteger size)
        {
            Runtime.Expect(size >= 5, "size invalid");
            Runtime.Expect(size <= 1000, "size too large");

            Runtime.Expect(!from.IsInterop, "address cannot be interop");

            Runtime.Expect(!Exists(name), "leaderboard already exists");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(ValidationUtils.IsValidIdentifier(name), "invalid name");

            var leaderboard = new Leaderboard()
            {
                name = name,
                owner = from,
                size = size,
                round = 0,
            };
            _leaderboards.Set<string, Leaderboard>(name, leaderboard);

            Runtime.Notify(EventKind.LeaderboardCreate, from, name);
        }

        public void ResetLeaderboard(Address from, string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            var leaderboard = _leaderboards.Get<string, Leaderboard>(name);

            Runtime.Expect(from == leaderboard.owner, "invalid leaderboard owner");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            leaderboard.round++;

            var rows = _rows.Get<string, StorageList>(name);
            rows.Clear();

            Runtime.Notify(EventKind.LeaderboardReset, from, name);
        }

        public Leaderboard GetLeaderboard(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            return _leaderboards.Get<string, Leaderboard>(name);
        }

        public LeaderboardRow[] GetRows(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            var rows = _rows.Get<string, StorageList>(name);

            return rows.All<LeaderboardRow>();
        }

        public BigInteger GetScoreByAddress(string name, Address target)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            for (int i = 0; i < count; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.address == target)
                {
                    return entry.score;
                }
            }

            return 0;
        }

        public BigInteger GetScoreByIndex(string name, BigInteger index)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            if (index < 0 || index >= count)
            {
                return 0;
            }

            var entry = rows.Get<LeaderboardRow>(index);
            return entry.score;
        }

        public Address GetAddressByIndex(string name, BigInteger index)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();

            if (index < 0 || index >= count)
            {
                return Address.Null;
            }

            var entry = rows.Get<LeaderboardRow>(index);
            return entry.address;
        }

        public BigInteger GetSize(string name)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");

            var rows = _rows.Get<string, StorageList>(name);
            return rows.Count();
        }

        public void InsertScore(Address from, Address target, string name, BigInteger score)
        {
            Runtime.Expect(Exists(name), "invalid leaderboard");
            
            var leaderboard = _leaderboards.Get<string, Leaderboard>(name);

            Runtime.Expect(from == leaderboard.owner, "invalid leaderboard owner");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var rows = _rows.Get<string, StorageList>(name);
            var count = rows.Count();
            var oldCount = count;

            int oldIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                if (entry.address == target)
                {
                    if (entry.score > score)
                    {
                        return;
                    }
                    oldIndex = i;
                    break;
                }
            }

            if (oldIndex >= 0)
            {
                count--;

                for (int i = oldIndex; i <= count - 1; i++)
                {
                    var entry = rows.Get<LeaderboardRow>(i + 1);
                    rows.Replace<LeaderboardRow>(i, entry);
                }

                rows.RemoveAt(count);
            }

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
                rows = _rows.Get<string, StorageList>(name);
                count = rows.Count();
                for (int i = 0; i < count; i++)
                {
                    var entry = rows.Get<LeaderboardRow>(i);
                    Runtime.Expect(entry.score >= score, "leaderboard bug");
                }

                return;
            }

            /*for (int i = lastIndex; i > bestIndex; i--)
            {
                var entry = rows.Get<LeaderboardRow>(i - 1);
                rows.Replace<LeaderboardRow>(i, entry);
            }*/

            var newRow = new LeaderboardRow()
            {
                address = target,
                score = score
            };

            if (bestIndex < count)
            {
                if (count < leaderboard.size)
                {
                    rows.Add<LeaderboardRow>(newRow);
                    for (int i = (int)count; i > bestIndex; i--)
                    {
                        var entry = rows.Get<LeaderboardRow>(i - 1);
                        rows.Replace<LeaderboardRow>(i, entry);
                    }
                }

                rows.Replace(bestIndex, newRow);
            }
            else
            {
                Runtime.Expect(bestIndex == count, "invalid insertion index");
                rows.Add<LeaderboardRow>(newRow);
            }

            rows = _rows.Get<string, StorageList>(name);
            count = rows.Count();
            for (int i = 0; i < bestIndex; i++)
            {
                var entry = rows.Get<LeaderboardRow>(i);
                Runtime.Expect(entry.score >= score, "leaderboard bug");
            }

            Runtime.Expect(count >= oldCount, "leaderboard bug");

            Runtime.Notify(EventKind.LeaderboardInsert, target, newRow);
        }
    }
}
