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

    public struct BombSeason
    {
        public BigInteger burned;
        public BigInteger total;
    }

    public sealed class BombContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Bomb;

        public const string SESLeaderboardName = "ses";
        public const string PointsLeaderboardName = "ses2";

        internal StorageMap _entries;
        internal StorageMap _seasons;
        internal BigInteger _lastSeason;

        public BombContract() : base()
        {
        }

        public void Initialize(Address from)
        {
            Runtime.Expect(from == Runtime.GenesisAddress, "must be genesis address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, SESLeaderboardName, 100, 0);
            Runtime.CallContext(NativeContractKind.Ranking, "CreateLeaderboard", this.Address, PointsLeaderboardName, 50, 0);
            _lastSeason = -1;
        }

        public BigInteger GetSeason()
        {
            var diff = Runtime.Time - Runtime.GetGenesisTime();
            var season = diff / (SecondsInDay * 90);
            return season;
        }

        private void FinishSeason()
        {
            var balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, this.Address);
            var halfSupply = Runtime.GetTokenSupply(DomainSettings.FuelTokenSymbol) / 2;
            var victory = balance >= halfSupply;

            var amountToBurn = balance;
            if (!victory)
            {
                amountToBurn /= 2;
            }

            Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, this.Address, amountToBurn);

            var rows = Runtime.CallContext(NativeContractKind.Ranking, "GetRows", SESLeaderboardName).AsInterop<LeaderboardRow[]>();

            int maxEntries = rows.Length;
            if (maxEntries > 50)
            {
                maxEntries = 50;
            }

            for (int i = 0; i < maxEntries; i++)
            {
                var points = (50 - i) * 5;
                var target = rows[i].address;

                var score = Runtime.CallContext(NativeContractKind.Ranking, "GetScore", target, PointsLeaderboardName).AsNumber();
                score += points;

                Runtime.CallContext(NativeContractKind.Ranking, "InsertScore", this.Address, target, PointsLeaderboardName, score);
            }

            Runtime.CallContext(NativeContractKind.Ranking, "ResetLeaderboard", this.Address, SESLeaderboardName);
            
            var currentRate = Runtime.CallContext(NativeContractKind.Stake, "GetRate").AsNumber();

            if (victory)
            {
                currentRate /= 2;
            }
            else
            {
                currentRate *= 2;
            }

            Runtime.CallContext(NativeContractKind.Stake, "UpdateRate", currentRate);

            var season = new BombSeason()
            {
                burned = balance,
                total = halfSupply,
            };
            _seasons.Set<BigInteger, BombSeason>(_lastSeason, season);

            ApplyInflation();

            _lastSeason = GetSeason();
        }

        private void ApplyInflation()
        {
            var currentSupply = Runtime.GetTokenSupply(DomainSettings.StakingTokenSymbol);

            // NOTE this gives an approximate inflation of 3% per year (0.75% per season)
            var mintAmount = currentSupply / 133;
            Runtime.Expect(mintAmount > 0, "invalid inflation amount");

            var phantomOrg = Runtime.GetOrganization(DomainSettings.PhantomForceOrganizationName);
            if (phantomOrg != null)
            {
                var phantomFunding = mintAmount / 3;
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, phantomOrg.Address, phantomFunding);
                mintAmount -= phantomFunding;
            }

            var bpOrg = Runtime.GetOrganization(DomainSettings.ValidatorsOrganizationName);
            if (bpOrg != null)
            {
                Runtime.MintTokens(DomainSettings.StakingTokenSymbol, this.Address, bpOrg.Address, mintAmount);
            }
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
                FinishSeason();
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
