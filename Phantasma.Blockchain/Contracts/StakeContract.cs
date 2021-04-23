using System;
using Phantasma.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Linq;
using Phantasma.Domain;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Blockchain.Contracts
{
    public struct EnergyStake
    {
        public BigInteger stakeAmount;
        public Timestamp stakeTime;
    }

    public struct EnergyClaim
    {
        public BigInteger stakeAmount;
        public Timestamp claimDate;
        public bool isNew;
    }

    public struct VotingLogEntry
    {
        public Timestamp timestamp;
        public BigInteger amount;
    }

    public sealed class StakeContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Stake;

        private StorageMap _stakeMap; // <Address, EnergyStake>
        private StorageMap _claimMap; // <Address, List<EnergyClaim>>
        private StorageMap _leftoverMap; // <Address, BigInteger>

        private StorageMap _masterAgeMap; // <Address, Timestamp>

        private StorageMap _masterClaims; // <Address, Timestamp>
        private Timestamp _lastMasterClaim;

        private BigInteger _currentEnergyRatioDivisor;

        private StorageMap _voteHistory; // <Address, List<StakeLog>>

        public static readonly BigInteger DefaultMasterThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        public readonly static BigInteger MasterClaimGlobalAmount = UnitConversion.ToBigInteger(125000, DomainSettings.StakingTokenDecimals);
        public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        public const string MasterStakeThresholdTag = "stake.master.threshold";
        public const string VotingStakeThresholdTag = "stake.vote.threshold";
        public const string StakeSingleBonusPercentTag = "stake.bonus.percent";
        public const string StakeMaxBonusPercentTag = "stake.bonus.max";

        public readonly static BigInteger MaxVotingPowerBonus = 1000;
        public readonly static BigInteger DailyVotingBonus = 1;

        public const uint DefaultEnergyRatioDivisor = 500; // used as 1/500, will initially generate 0.002 per staked token

        public StakeContract() : base()
        {
        }

        public void Initialize(Address from)
        {
            _currentEnergyRatioDivisor = DefaultEnergyRatioDivisor; // used as 1/500, will initially generate 0.002 per staked token
        }

        public BigInteger GetMasterThreshold()
        {
            if (Runtime.HasGenesis)
            {
                var amount = Runtime.GetGovernanceValue(MasterStakeThresholdTag);
                return amount;
            }

            return DefaultMasterThreshold;
        }

        public bool IsMaster(Address address)
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.IsMember(address);
        }

        public BigInteger GetMasterCount()
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.Size;
        }

        public Address[] GetMasterAddresses()
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);
            return masters.GetMembers();
        }

        //verifies how many valid masters are in the condition to claim the reward for a specific master claim date, assuming no changes in their master status in the meantime
        public BigInteger GetClaimMasterCount(Timestamp claimDate)
        {
            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);

            DateTime requestedClaimDate = new DateTime(((DateTime)claimDate).Year, ((DateTime)claimDate).Month, 1);

            var addresses = masters.GetMembers();
            var count = addresses.Length;
            var result = count;

            for (int i = 0; i < count; i++)
            {
                var addr = addresses[i];
                var currentMasterClaimDate = (DateTime)_masterClaims.Get<Address, Timestamp>(addr);
                if (currentMasterClaimDate > requestedClaimDate)
                {
                    result--;
                }
            }

            return result;
        }

        public Timestamp GetMasterClaimDate(BigInteger claimDistance)
        {
            return GetMasterClaimDateFromReference(claimDistance, default(Timestamp));
        }

        public Timestamp GetMasterDate(Address target)
        {
            if (_masterAgeMap.ContainsKey<Address>(target))
            {
                return _masterAgeMap.Get<Address, Timestamp>(target);
            }

            return new Timestamp(0);
        }

        public Timestamp GetMasterClaimDateFromReference(BigInteger claimDistance, Timestamp referenceTime)
        {
            DateTime referenceDate;
            if (referenceTime.Value != 0)
            {
                referenceDate = referenceTime;
            }
            else
            if (_lastMasterClaim.Value == 0)
            {
                if (Runtime.HasGenesis)
                {
                    Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                    var referenceBlock = Runtime.GetBlockByHeight(1);
                    referenceDate = referenceBlock.Timestamp;
                }
                else
                {
                    referenceDate = Runtime.Time;
                }
                referenceDate = referenceDate.AddMonths(-1);
            }
            else
            {
                referenceDate = _lastMasterClaim;
            }

            var nextMasterClaim = (Timestamp)(new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0, DateTimeKind.Utc)).AddMonths((int)claimDistance);
            var dateTimeClaim = (DateTime)nextMasterClaim;

            if (dateTimeClaim.Hour == 23)
                nextMasterClaim = dateTimeClaim.AddHours(1);
            if (dateTimeClaim.Hour == 1)
                nextMasterClaim = dateTimeClaim.AddHours(-1);

            //Allow a claim once per month starting on the 1st day of each month
            return nextMasterClaim;
        }

        public BigInteger GetMasterRewards(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            var thisClaimDate = _masterClaims.Get<Address, Timestamp>(from);
            var totalAmount = MasterClaimGlobalAmount;
            var validMasterCount = GetClaimMasterCount(thisClaimDate);
            var individualAmount = totalAmount / validMasterCount;
            var leftovers = totalAmount % validMasterCount;
            individualAmount += leftovers;

            return individualAmount;
        }

        // migrates the full stake from one address to other
        public void Migrate(Address from, Address to)
        {
            Runtime.Expect(Runtime.PreviousContext.Name == "account", "invalid context");

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(to.IsUser, "destination must be user address");

            var targetStake = GetStake(to);
            Runtime.Expect(targetStake == 0, "Tried to migrate to an account that's already staking");

            var unclaimed = GetUnclaimed(from);
            Runtime.Expect(unclaimed == 0, "claim before migrating");

            _stakeMap.Migrate<Address, EnergyStake>(from, to);
            _masterClaims.Migrate<Address, Timestamp>(from, to);
            _masterAgeMap.Migrate<Address, Timestamp>(from, to);
            _voteHistory.Migrate<Address, StorageList>(from, to);
            _leftoverMap.Migrate<Address, Timestamp>(from, to);
            _claimMap.Migrate<Address, StorageList>(from, to);

            Runtime.Notify(EventKind.AddressMigration, to, from);
        }

        public void MasterClaim(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), $"{from} is no SoulMaster");

            var thisClaimDate = _masterClaims.Get<Address, Timestamp>(from);
            Runtime.Expect(Runtime.Time >= thisClaimDate, "not enough time waited");

            var symbol = DomainSettings.StakingTokenSymbol;
            var token = Runtime.GetToken(symbol);

            var totalAmount = MasterClaimGlobalAmount;
            Runtime.MintTokens(token.Symbol, this.Address, this.Address, totalAmount);

            var masters = Runtime.GetOrganization(DomainSettings.MastersOrganizationName);

            var validMasterCount = GetClaimMasterCount(Runtime.Time);

            var individualAmount = totalAmount / validMasterCount;
            var leftovers = totalAmount % validMasterCount;

            var nextClaim = GetMasterClaimDateFromReference(1, thisClaimDate);

            var addresses = masters.GetMembers();
            for (int i = 0; i < addresses.Length; i++)
            {
                var addr = addresses[i];
                var claimDate = _masterClaims.Get<Address, Timestamp>(addr);

                if (claimDate > thisClaimDate)
                {
                    continue;
                }

                var transferAmount = individualAmount;
                if (addr == from)
                {
                    transferAmount += leftovers;
                }

                Runtime.TransferTokens(token.Symbol, this.Address, addr, transferAmount);
                totalAmount -= transferAmount;

                _masterClaims.Set<Address, Timestamp>(addr, nextClaim);
            }

            Runtime.Expect(totalAmount == 0, $"{totalAmount} something failed");

            _lastMasterClaim = Runtime.Time;
        }

        public void Stake(Address from, BigInteger stakeAmount)
        {
            Runtime.Expect(stakeAmount >= MinimumValidStake, "invalid amount");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var balance = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, from);

            if (stakeAmount > balance) 
            {
                var diff = stakeAmount - balance;
                throw new BalanceException(Runtime.GetToken("SOUL"), from, diff);
                balance = stakeAmount; // debug mode only, otherwise a exception will prevent it from reaching here
            }

            Runtime.Expect(balance >= stakeAmount, $"balance: {balance} stake: {stakeAmount} not enough balance to stake at " + from);

            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, from, this.Address, stakeAmount);

            EnergyStake stake;

            if (_stakeMap.ContainsKey<Address>(from))
            {
                stake = _stakeMap.Get<Address, EnergyStake>(from);
            }
            else
            {
                stake = new EnergyStake()
                {
                    stakeTime = new Timestamp(0),
                    stakeAmount = 0,
                };
            }

            stake.stakeTime = Runtime.Time;
            stake.stakeAmount += stakeAmount;
            _stakeMap.Set<Address, EnergyStake>(from, stake);

            Runtime.AddMember(DomainSettings.StakersOrganizationName, this.Address, from);

            var claimList = _claimMap.Get<Address, StorageList>(from);
            var claimEntry = new EnergyClaim()
            {
                stakeAmount = stakeAmount,
                claimDate = this.Runtime.Time,
                isNew = true,
            };
            claimList.Add(claimEntry);

            var logEntry = new VotingLogEntry()
            {
                timestamp = this.Runtime.Time,
                amount = stakeAmount
            };
            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);
            votingLogbook.Add(logEntry);

            // masters membership
            var masterAccountThreshold = GetMasterThreshold();
            if (stake.stakeAmount >= masterAccountThreshold && !IsMaster(from))
            {
                var nextClaim = GetMasterClaimDate(2);

                Runtime.AddMember(DomainSettings.MastersOrganizationName, this.Address, from);
                _masterClaims.Set<Address, Timestamp>(from, nextClaim);

                _masterAgeMap.Set<Address, Timestamp>(from, Runtime.Time);
            }
        }

        public void Unstake(Address from, BigInteger unstakeAmount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(unstakeAmount >= MinimumValidStake, "invalid amount");

            Runtime.Expect(_stakeMap.ContainsKey<Address>(from), "nothing to unstake");

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            Runtime.Expect(stake.stakeAmount > 0, "nothing to unstake");

            Runtime.Expect(stake.stakeTime.Value > 0, "something weird happened in unstake"); // failsafe, should never happen

            Runtime.Expect(Runtime.Time >= stake.stakeTime, "Negative time diff");

            var stakedDiff = Runtime.Time - stake.stakeTime;
            var stakedDays = stakedDiff / SecondsInDay; // convert seconds to days

            Runtime.Expect(stakedDays >= 1, "waiting period required");

            var token = Runtime.GetToken(DomainSettings.StakingTokenSymbol);
            var balance = Runtime.GetBalance(token.Symbol, this.Address);
            Runtime.Expect(balance >= unstakeAmount, "not enough balance to unstake");

            var availableStake = stake.stakeAmount;
            availableStake -= GetStorageStake(from);
            Runtime.Expect(availableStake >= unstakeAmount, "tried to unstake more than what was staked");

            //if this is a partial unstake
            if (availableStake - unstakeAmount > 0)
            {
                Runtime.Expect(availableStake - unstakeAmount >= MinimumValidStake, "leftover stake would be below minimum staking amount");
            }

            Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, this.Address, from, unstakeAmount);

            stake.stakeAmount -= unstakeAmount;

            if (stake.stakeAmount == 0)
            {
                _stakeMap.Remove(from);
                _voteHistory.Remove(from);

                Runtime.RemoveMember(DomainSettings.StakersOrganizationName, this.Address, from);

                var name = Runtime.GetAddressName(from);
                if (name != ValidationUtils.ANONYMOUS_NAME)
                {
                    Runtime.CallNativeContext(NativeContractKind.Account, "UnregisterName", from);
                }
            }
            else
            {
                _stakeMap.Set<Address, EnergyStake>(from, stake);

                RemoveVotingPower(from, unstakeAmount);
            }

            var masterAccountThreshold = GetMasterThreshold();

            if (stake.stakeAmount < masterAccountThreshold)
            {
                Runtime.RemoveMember(DomainSettings.MastersOrganizationName, this.Address, from);

                if (_masterClaims.ContainsKey<Address>(from))
                {
                    _masterClaims.Remove<Address>(from);
                }

                if (_masterAgeMap.ContainsKey<Address>(from))
                {
                    _masterAgeMap.Remove<Address>(from);
                }
            }

            var claimList = _claimMap.Get<Address, StorageList>(from);
            var count = claimList.Count();

            BigInteger leftovers = 0;

            while (unstakeAmount > 0)
            {
                int bestIndex = -1;
                var bestTime = new Timestamp(0);

                // find the oldest stake
                for (int i = 0; i < count; i++)
                {
                    var temp = claimList.Get<EnergyClaim>(i);
                    if (bestIndex == -1 || temp.claimDate < bestTime)
                    {
                        bestTime = temp.claimDate;
                        bestIndex = i;
                    }
                }

                Runtime.Expect(bestIndex >= 0, "something went wrong with unstake");

                var entry = claimList.Get<EnergyClaim>(bestIndex);

                BigInteger subtractedAmount;

                if (entry.stakeAmount > unstakeAmount)
                {
                    subtractedAmount = unstakeAmount;
                    entry.stakeAmount -= subtractedAmount;
                    claimList.Replace<EnergyClaim>(bestIndex, entry);
                }
                else
                {
                    subtractedAmount = entry.stakeAmount;
                    claimList.RemoveAt(bestIndex);
                    count--;
                }


                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var claimDays = (claimDiff / SecondsInDay);
                    if (!entry.isNew && claimDays > 0)
                    {
                        claimDays--;  // unless new (meaning was never claimed) we subtract the initial day due to instant claim
                    }

                    if (claimDays >= 1)
                    {
                        var amount = StakeToFuel(subtractedAmount);
                        amount *= claimDays;
                        leftovers += amount;
                    }
                }

                unstakeAmount -= subtractedAmount;
            }

            if (leftovers > 0)
            {
                if (_leftoverMap.ContainsKey<Address>(from))
                {
                    leftovers += _leftoverMap.Get<Address, BigInteger>(from);
                }

                _leftoverMap.Set<Address, BigInteger>(from, leftovers);
            }
        }

        public BigInteger GetTimeBeforeUnstake(Address from)
        {
            if (!_stakeMap.ContainsKey<Address>(from))
            {
                return 0;
            }

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            return SecondsInDay - (Runtime.Time - stake.stakeTime);
        }

        public Timestamp GetStakeTimestamp(Address from)
        {
            if (!_stakeMap.ContainsKey(from))
            {
                return 0;
            }

            var stake = _stakeMap.Get<Address, EnergyStake>(from);
            return stake.stakeTime;
        }

        private void RemoveVotingPower(Address from, BigInteger amount)
        {
            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);

            var listSize = votingLogbook.Count();

            for (var i = listSize - 1; i >= 0 && amount > 0; i--)
            {
                var votingEntry = votingLogbook.Get<VotingLogEntry>(i);

                if (votingEntry.amount > amount)
                {
                    votingEntry.amount -= amount;
                    votingLogbook.Replace(i, votingEntry);

                    amount = 0;
                }
                else
                {
                    amount -= votingEntry.amount;
                    votingLogbook.RemoveAt(i);
                }
            }
        }

        public BigInteger GetUnclaimed(Address from)
        {
            BigInteger total = 0;

            var claimList = _claimMap.Get<Address, StorageList>(from);

            uint[] crownDays;


            if (Runtime.ProtocolVersion >= 5)
            {
                var crowns = Runtime.GetOwnerships(DomainSettings.RewardTokenSymbol, from);

                // calculate how many days each CROWN is hold at current address and use older ones first
                crownDays = crowns.Select(id => (Runtime.Time - Runtime.ReadToken(DomainSettings.RewardTokenSymbol, id).Timestamp) / SecondsInDay).OrderByDescending(k => k).ToArray();
            }
            else
            {
                crownDays = new uint[0];
            }


            var bonusPercent = (int)Runtime.GetGovernanceValue(StakeSingleBonusPercentTag);
            var maxPercent = (int)Runtime.GetGovernanceValue(StakeMaxBonusPercentTag);

            var count = claimList.Count();
            for (int i = 0; i < count; i++)
            {
                var entry = claimList.Get<EnergyClaim>(i);

                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var claimDays = (claimDiff / SecondsInDay);
                    if (entry.isNew)
                    {
                        claimDays++;
                    }

                    if (claimDays >= 1)
                    {
                        var amount = StakeToFuel(entry.stakeAmount);
                        amount *= claimDays;
                        total += amount;

                        int bonusAccum = 0;
                        var bonusAmount = (amount * bonusPercent) / 100;

                        var dailyBonus = bonusAmount / claimDays;

                        foreach (var bonusDays in crownDays)
                        {
                            if (bonusDays >= 1)
                            {
                                bonusAccum += bonusPercent;
                                if (bonusAccum > maxPercent)
                                {
                                    break;
                                }

                                var maxBonusDays = bonusDays > claimDays ? claimDays : bonusDays;
                                total += dailyBonus * maxBonusDays;                                
                            }
                        }
                    }
                }
            }


            if (_leftoverMap.ContainsKey<Address>(from))
            {
                var leftover = _leftoverMap.Get<Address, BigInteger>(from);
                total += leftover;
            }

            return total;
        }

        public void Claim(Address from, Address stakeAddress)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var unclaimedAmount = GetUnclaimed(stakeAddress);

            if (Runtime.ProtocolVersion < 5)
            {
                var crownCount = Runtime.GetBalance(DomainSettings.RewardTokenSymbol, from);

                var bonusPercent = Runtime.GetGovernanceValue(StakeSingleBonusPercentTag);
                var maxPercent = Runtime.GetGovernanceValue(StakeMaxBonusPercentTag);

                bonusPercent *= crownCount;
                if (bonusPercent > maxPercent)
                {
                    bonusPercent = maxPercent;
                }

                var bonusAmount = (unclaimedAmount * bonusPercent) / 100;
                unclaimedAmount += bonusAmount;
            }

            Runtime.Expect(unclaimedAmount > 0, "nothing unclaimed");

            var fuelAmount = unclaimedAmount;

            // if the transaction comes from someone other than the stake owner, must be a contract / organizataion
            if (from != stakeAddress)
            {
                Runtime.Expect(stakeAddress.IsSystem, "must claim from a system address");
            }

            Runtime.MintTokens(DomainSettings.FuelTokenSymbol, this.Address, stakeAddress, fuelAmount);

            var claimList = _claimMap.Get<Address, StorageList>(stakeAddress);
            var count = claimList.Count();

            // update the date of everything that was claimed
            for (int i = 0; i < count; i++)
            {
                var entry = claimList.Get<EnergyClaim>(i);

                if (Runtime.Time >= entry.claimDate)
                {
                    var claimDiff = Runtime.Time - entry.claimDate;
                    var clamDays = (claimDiff / SecondsInDay);
                    if (entry.isNew)
                    {
                        clamDays++;
                    }

                    if (clamDays >= 1)
                    {
                        entry.claimDate = Runtime.Time;
                        entry.isNew = false;
                        claimList.Replace<EnergyClaim>(i, entry);
                    }
                }
            }

            // remove any leftovers
            if (_leftoverMap.ContainsKey<Address>(stakeAddress))
            {
                _leftoverMap.Remove<Address>(stakeAddress);
            }

            // mark date to prevent imediate unstake
            if (Runtime.Time >= ContractPatch.UnstakePatch)
            {
                Runtime.Expect(_stakeMap.ContainsKey<Address>(stakeAddress), "invalid stake address");
                var stake = _stakeMap.Get<Address, EnergyStake>(stakeAddress);
                stake.stakeTime = Runtime.Time;
                _stakeMap.Set<Address, EnergyStake>(stakeAddress, stake);
            }
        }

        public BigInteger GetStake(Address address)
        {
            BigInteger stake = 0;

            if (_stakeMap.ContainsKey(address))
            {
                stake = _stakeMap.Get<Address, EnergyStake>(address).stakeAmount;
            }

            return stake;
        }

        public BigInteger GetStorageStake(Address address)
        {
            var usedStorageSize = Runtime.CallNativeContext( NativeContractKind.Storage, "GetUsedSpace", address).AsNumber();
            var usedStake = usedStorageSize * UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);

            var kilobytesPerStake = (int)Runtime.GetGovernanceValue(StorageContract.KilobytesPerStakeTag);
            usedStake = usedStake / (kilobytesPerStake * 1024);

            return usedStake;
        }

        public BigInteger FuelToStake(BigInteger fuelAmount)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * _currentEnergyRatioDivisor, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals);
        }

        public BigInteger StakeToFuel(BigInteger stakeAmount)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _currentEnergyRatioDivisor;
        }

        public static BigInteger FuelToStake(BigInteger fuelAmount, uint _BaseEnergyRatioDivisor)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * _BaseEnergyRatioDivisor, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals);
        }

        public static BigInteger StakeToFuel(BigInteger stakeAmount, uint _BaseEnergyRatioDivisor)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / _BaseEnergyRatioDivisor;
        }

        public BigInteger GetAddressVotingPower(Address address)
        {
            var requiredVotingThreshold = Runtime.GetGovernanceValue(VotingStakeThresholdTag);
            if (GetStake(address) < requiredVotingThreshold)
            {
                return 0;
            }

            var votingLogbook = _voteHistory.Get<Address, StorageList>(address);
            BigInteger power = 0;

            var listSize = votingLogbook.Count();
            var time = Runtime.Time;

            for (int i = 0; i < listSize; i++)
            {
                var entry = votingLogbook.Get<VotingLogEntry>(i);

                if (i > 0)
                    Runtime.Expect(votingLogbook.Get<VotingLogEntry>(i - 1).timestamp <= entry.timestamp, "Voting list became unsorted!");

                power += CalculateEntryVotingPower(entry, time);
            }

            return power;
        }

        private BigInteger CalculateEntryVotingPower(VotingLogEntry entry, Timestamp currentTime)
        {
            BigInteger baseMultiplier = 100;

            BigInteger votingMultiplier = baseMultiplier;
            var diff = (currentTime - entry.timestamp) / 86400;

            var votingBonus = diff < MaxVotingPowerBonus ? diff : MaxVotingPowerBonus;

            votingMultiplier += DailyVotingBonus * votingBonus;

            var votingPower = (entry.amount * votingMultiplier) / 100;

            return votingPower;
        }

        public void UpdateRate(BigInteger rate)
        {
            var bombAddress = GetAddressForName("bomb");
            Runtime.Expect(Runtime.IsWitness(bombAddress), "must be called from bomb address");

            Runtime.Expect(rate > 0, "invalid rate");
            _currentEnergyRatioDivisor = rate;
        }

        public BigInteger GetRate()
        {
            return _currentEnergyRatioDivisor;
        }
    }
}
