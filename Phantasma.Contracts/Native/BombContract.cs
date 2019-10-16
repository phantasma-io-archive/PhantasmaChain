using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Numerics;
using Phantasma.Storage.Context;

namespace Phantasma.Contracts.Native
{
    public struct BombEntry
    {
        public BigInteger amount;
        public BigInteger round;
    }

    public sealed class BombContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Bomb;

        public const string SESLeaderboardName = "ses";
        public const string BPLeaderboardName = "sesbp";

        internal StorageMap _entries;

        public BombContract() : base()
        {
        }

        public void Initialize(Address from)
        {
            Runtime.Expect(from == Runtime.GenesisAddress, "must be genesis address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, SESLeaderboardName, 100, 0);
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, BPLeaderboardName, 10, 0);
        }

        public void OnReceive(Address source, Address destination, string symbol, BigInteger amount)
        {
            if (!Runtime.HasGenesis)
            {
                return;
            }

            Runtime.Expect(symbol == DomainSettings.FuelTokenSymbol, "cannot accept this token");
            Runtime.Expect(amount > 0 , "invalid amount");

            Runtime.Expect(source.IsUser, "can only accept fuel from user addresses");

            string leaderboardName = SESLeaderboardName;
            var leaderboard = (Leaderboard)Runtime.CallContext(NativeContractKind.Ranking, "GetLeaderboard", leaderboardName).ToObject();

            BombEntry entry;
            if (_entries.ContainsKey<Address>(source))
            {
                entry = _entries.Get<Address, BombEntry>(source);
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
            _entries.Set<Address, BombEntry>(source, entry);

            Runtime.CallContext(NativeContractKind.Ranking, "InsertScore", this.Address, source, leaderboardName, entry.amount);
        }
    }
}
