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
        internal BigInteger _lastSeason;

        public BombContract() : base()
        {
        }

        public void Initialize(Address from)
        {
            Runtime.Expect(from == Runtime.GenesisAddress, "must be genesis address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, SESLeaderboardName, 100, 0);
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, BPLeaderboardName, 10, 0);
            _lastSeason = 0;
        }

        public BigInteger GetSeason()
        {
            var diff = Runtime.Time - Runtime.GetGenesisTime();
            var season = diff / (SecondsInDay * 90);
            return season;
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

            var currentSeason = GetSeason();
            if (currentSeason != _lastSeason)
            {
                var balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, this.Address);
                var halfSupply = Runtime.GetTokenSupply(DomainSettings.FuelTokenSymbol) / 2;
                var victory = balance >= halfSupply;

                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, this.Address, balance);

                Runtime.CallContext(NativeContractKind.Ranking, "ResetLeaderboard", this.Address, leaderboardName);
                Runtime.CallContext(NativeContractKind.Stake, "UpdateRate", victory);
                _lastSeason = currentSeason;
            }

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
