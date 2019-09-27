using System;
using Phantasma.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using System.Linq;
using Phantasma.Domain;

namespace Phantasma.Contracts.Native
{
    public struct EnergyAction
    {
        public BigInteger unclaimedPartials;
        public BigInteger totalAmount;
        public Timestamp timestamp;
    }

    public struct VotingLogEntry
    {
        public Timestamp timestamp;
        public BigInteger amount;
    }

    public struct EnergyProxy
    {
        public Address address;
        public BigInteger percentage;
    }

    public struct EnergyMaster
    {
        public Address address;
        public Timestamp claimDate;
    }

    public sealed class StakeContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Stake;

        private StorageMap _stakes; // <Address, EnergyAction>
        private StorageMap _proxyStakersMap; // <Address, List<EnergyProxy>>
        private StorageMap _proxyReceiversMap; // <Address, List<Address>>
        private StorageMap _claims; // <Address, EnergyAction>
        private StorageList _mastersList; // <Address>
        private Timestamp _lastMasterClaim;
        private StorageMap _masterMemory; // <Address, Timestamp>
        private StorageMap _voteHistory; // <Address, List<StakeLog>>
        private uint _masterClaimCount;

        private Timestamp genesisTimestamp = 0;

        public static readonly BigInteger DefaultMasterThreshold = UnitConversion.ToBigInteger(50000, DomainSettings.StakingTokenDecimals);
        public readonly static BigInteger MasterClaimGlobalAmount = UnitConversion.ToBigInteger(125000, DomainSettings.StakingTokenDecimals);

        public readonly static BigInteger BaseEnergyRatioDivisor = 500; // used as 1/500, will generate 0.002 per staked token
        public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(DomainSettings.StakingTokenDecimals);

        public const string MasterStakeThresholdTag = "stake.master.threshold";
        public const string VotingStakeThresholdTag = "stake.vote.threshold";

        public readonly static BigInteger MaxVotingPowerBonus = 1000;
        public readonly static BigInteger DailyVotingBonus = 1;

        public StakeContract() : base()
        {
        }

        public BigInteger GetMasterThreshold()
        {
            if (Runtime.Nexus.HasGenesis)
            {
                var amount = Runtime.GetGovernanceValue(MasterStakeThresholdTag);
                return amount;
            }

            return DefaultMasterThreshold;
        }

        public EnergyMaster GetMaster(Address address)
        {
            var count = _mastersList.Count();
            for (int i = 0; i < count; i++)
            {
                var master = _mastersList.Get<EnergyMaster>(i);
                if (master.address == address)
                {
                    return master;
                }
            }

            return new EnergyMaster { address = Address.Null };
        }

        public bool IsMaster(Address address)
        {
            return !GetMaster(address).address.IsNull;
        }

        public BigInteger GetMasterCount()
        {
            return _mastersList.Count();
        }

        public Address[] GetMasterAddresses()
        {
            return _mastersList.All<EnergyMaster>().Select(x => x.address).ToArray();
        }

        //verifies how many valid masters are in the condition to claim the reward for a specific master claim date, assuming no changes in their master status in the meantime
        public BigInteger GetClaimMasterCount(Timestamp claimDate)
        {
            var count = GetMasterCount();
            var result = count;
            DateTime requestedClaimDate = new DateTime(((DateTime)claimDate).Year, ((DateTime)claimDate).Month, 1);

            for (int i = 0; i < count; i++)
            {
                DateTime currentMasterClaimDate = _mastersList.Get<EnergyMaster>(i).claimDate;
                if (currentMasterClaimDate > requestedClaimDate)
                    result--;
            }

            return result;
        }

        public Timestamp GetMasterClaimDate(BigInteger claimDistance)
        {
            return GetMasterClaimDateFromReference(claimDistance, default(Timestamp));
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
                Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                var referenceBlock = Runtime.GetBlockByHeight(1);
                referenceDate = referenceBlock.Timestamp;
            }
            else
            {
                referenceDate = _lastMasterClaim;
            }

            var nextMasterClaim = (Timestamp)(new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0)).AddMonths((int)claimDistance);
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

            var thisClaimDate = GetMaster(from).claimDate;
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
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(to.IsUser, "destination must be user address");

            var targetStake = _stakes.Get<Address, EnergyAction>(to);
            Runtime.Expect(targetStake.totalAmount == 0, "Tried to migrate to an account that's already staking");

            //migrate stake
            var sourceStake = _stakes.Get<Address, EnergyAction>(from);

            _stakes.Set(to, sourceStake);
            _stakes.Remove(from);

            //migrate master claim
            var masterAccountThreshold = GetMasterThreshold();
            if (sourceStake.totalAmount >= masterAccountThreshold)
            {
                var count = _mastersList.Count();
                var index = -1;
                Timestamp claimDate = Runtime.Time;
                for (int i = 0; i < count; i++)
                {
                    var master = _mastersList.Get<EnergyMaster>(i);
                    if (master.address == from)
                    {

                        index = i;
                        claimDate = master.claimDate;
                        break;
                    }
                }

                Runtime.Expect(index >= 0, "Expected this address to be a master");

                _mastersList.RemoveAt<EnergyMaster>(index);
                _mastersList.Add(new EnergyMaster() { address = to, claimDate = claimDate });
            }

            //migrate voting power
            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);
            votingLogbook.Add(to);
            votingLogbook.Remove(from);
        }

        public void MasterClaim(Address from)
        {
            Runtime.Expect(_masterClaimCount < 12 * 4, "no more claims available"); // 4 years

            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            var thisClaimDate = GetMaster(from).claimDate;
            Runtime.Expect(Runtime.Time >= thisClaimDate, "not enough time waited");

            var symbol = DomainSettings.StakingTokenSymbol;
            var token = Runtime.GetToken(symbol);

            var totalAmount = MasterClaimGlobalAmount;
            Runtime.Expect(Runtime.MintTokens(token.Symbol, this.Address, totalAmount), "mint failed");

            var listSize = _mastersList.Count();

            var validMasterCount = GetClaimMasterCount(thisClaimDate);

            var individualAmount = totalAmount / validMasterCount;
            var leftovers = totalAmount % validMasterCount;

            for (int i = 0; i < listSize; i++)
            {
                var targetMaster = _mastersList.Get<EnergyMaster>(i);

                if (targetMaster.claimDate != thisClaimDate)
                    continue;

                var transferAmount = individualAmount;
                if (targetMaster.address == from)
                {
                    transferAmount += leftovers;
                }

                Runtime.Expect(Runtime.TransferTokens(token.Symbol, this.Address, targetMaster.address, transferAmount), "transfer failed");

                totalAmount -= transferAmount;

                Runtime.Notify(EventKind.TokenMint, targetMaster.address, new TokenEventData() { symbol = token.Symbol, value = transferAmount, chainAddress = this.Address });

                var nextClaim = GetMasterClaimDateFromReference(1, thisClaimDate);

                _mastersList.Replace(i, new EnergyMaster() { address = from, claimDate = nextClaim });
            }
            Runtime.Expect(totalAmount == 0, "something failed");

            _lastMasterClaim = Runtime.Time;
            _masterClaimCount++;
        }

        public void Stake(Address from, BigInteger stakeAmount)
        {
            Runtime.Expect(stakeAmount >= MinimumValidStake, "invalid amount");
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var balance = Runtime.GetBalance(DomainSettings.StakingTokenSymbol, from);

            var currentStake = _stakes.Get<Address, EnergyAction>(from);

            var newStake = stakeAmount + currentStake.totalAmount;

            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, from, this.Address, stakeAmount), "stake transfer failed");

            var entry = new EnergyAction()
            {
                unclaimedPartials = stakeAmount + GetLastAction(from).unclaimedPartials,
                totalAmount = newStake,
                timestamp = this.Runtime.Time,
            };
            _stakes.Set(from, entry);

            var logEntry = new VotingLogEntry()
            {
                timestamp = this.Runtime.Time,
                amount = stakeAmount
            };

            var votingLogbook = _voteHistory.Get<Address, StorageList>(from);
            votingLogbook.Add(logEntry);

            var masterAccountThreshold = GetMasterThreshold();

            if (Runtime.Nexus.GenesisAddress != from && newStake >= masterAccountThreshold && !IsMaster(from))
            {
                var nextClaim = GetMasterClaimDate(2);

                _mastersList.Add(new EnergyMaster() { address = from, claimDate = nextClaim });
                Runtime.Notify(EventKind.RolePromote, from, new RoleEventData() { role = "master", date = nextClaim });
            }

            Runtime.Notify(EventKind.TokenStake, from, new TokenEventData() { chainAddress = this.Address, symbol = DomainSettings.StakingTokenSymbol, value = stakeAmount });
        }

        public BigInteger Unstake(Address from, BigInteger unstakeAmount)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");
            Runtime.Expect(unstakeAmount >= MinimumValidStake, "invalid amount");

            if (!_stakes.ContainsKey<Address>(from))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(from);

            if (stake.timestamp.Value == 0) // failsafe, should never happen
            {
                return 0;
            }

            Runtime.Expect(Runtime.Time >= stake.timestamp, "Negative time diff");

            var diff = Runtime.Time - stake.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 1, "waiting period required");

            var token = Runtime.GetToken(DomainSettings.StakingTokenSymbol);
            var balance = Runtime.GetBalance(token.Symbol, this.Address);
            Runtime.Expect(balance >= unstakeAmount, "not enough balance");

            var availableStake = stake.totalAmount;
            availableStake -= GetStorageStake(from);
            Runtime.Expect(availableStake >= unstakeAmount, "tried to unstake more than what was staked");

            //if this is a partial unstake
            if (availableStake - unstakeAmount > 0)
                Runtime.Expect(availableStake - unstakeAmount >= MinimumValidStake, "leftover stake would be below minimum staking amount");

            Runtime.Expect(Runtime.TransferTokens(DomainSettings.StakingTokenSymbol, this.Address, from, unstakeAmount), "stake transfer failed");

            stake.totalAmount -= unstakeAmount;

            var unclaimedPartials = GetLastAction(from).unclaimedPartials;

            if (stake.totalAmount == 0 && unclaimedPartials == 0)
            {
                _stakes.Remove(from);
                _voteHistory.Remove(from);
            }
            else
            {
                var entry = new EnergyAction()
                {
                    unclaimedPartials = unclaimedPartials,
                    totalAmount = stake.totalAmount,
                    timestamp = this.Runtime.Time,
                };

                _stakes.Set(from, entry);

                RemoveVotingPower(from, unstakeAmount);
            }

            var masterAccountThreshold = GetMasterThreshold();

            if (stake.totalAmount < masterAccountThreshold)
            {
                var count = _mastersList.Count();
                var index = -1;
                for (int i = 0; i < count; i++)
                {
                    var master = _mastersList.Get<EnergyMaster>(i);
                    if (master.address == from)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    var penalizationDate = GetMasterClaimDateFromReference(1, _mastersList.Get<EnergyMaster>(index).claimDate);
                    _mastersList.RemoveAt<EnergyMaster>(index);

                    Runtime.Notify(EventKind.RoleDemote, from, new RoleEventData() { role = "master", date = penalizationDate });
                }
            }

            Runtime.Notify(EventKind.TokenUnstake, from, new TokenEventData() { chainAddress = this.Address, symbol = token.Symbol, value = unstakeAmount });

            return unstakeAmount;
        }

        public BigInteger GetTimeBeforeUnstake(Address from)
        {
            if (!_stakes.ContainsKey<Address>(from))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(from);
            return 86400 - (Runtime.Time - stake.timestamp);

        }

        public Timestamp GetStakeTimestamp(Address from)
        {
            if (!_stakes.ContainsKey(from))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(from);
            return stake.timestamp;
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
                    votingLogbook.RemoveAt<VotingLogEntry>(i);
                }
            }
        }

        public BigInteger GetUnclaimed(Address stakeAddress)
        {
            if (!_stakes.ContainsKey<Address>(stakeAddress))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(stakeAddress);

            var currentStake = stake.totalAmount;

            var lastClaim = _claims.Get<Address, EnergyAction>(stakeAddress);



            if (lastClaim.timestamp.Value == 0)
                lastClaim.timestamp = stake.timestamp;

            Runtime.Expect(Runtime.Time >= stake.timestamp, "Negative time diff");

            var diff = Runtime.Time - lastClaim.timestamp;

            var days = diff / 86400; // convert seconds to days

            // if not enough time has passed, deduct the last claim from the available amount
            if (days <= 0)
            {
                currentStake -= lastClaim.totalAmount;
            }

            // clamp to avoid negative values
            if (currentStake < 0)
            {
                currentStake = 0;
            }

            return CalculateRewardsWithHalving(currentStake, GetLastAction(stakeAddress).unclaimedPartials, lastClaim.timestamp, Runtime.Time);
        }

        public void Claim(Address from, Address stakeAddress)
        {
            Runtime.Expect(Runtime.IsWitness(from), "witness failed");

            var unclaimedAmount = GetUnclaimed(stakeAddress);

            Runtime.Expect(unclaimedAmount > 0, "nothing unclaimed");

            var fuelAmount = unclaimedAmount;

            // distribute to proxy list
            var list = _proxyStakersMap.Get<Address, StorageList>(stakeAddress);
            var count = list.Count();

            // if the transaction comes from someone other than the stake owner, must be registred in proxy list
            if (from != stakeAddress)
            {
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    var proxy = list.Get<EnergyProxy>(i);
                    if (proxy.address == from)
                    {
                        found = true;
                        break;
                    }
                }
                Runtime.Expect(found, "invalid permissions");
            }

            BigInteger sum = 0;
            BigInteger availableAmount = fuelAmount;

            for (int i = 0; i < count; i++)
            {
                var proxy = list.Get<EnergyProxy>(i);
                sum += proxy.percentage;

                var proxyAmount = (fuelAmount * proxy.percentage) / 100;
                if (proxyAmount > 0)
                {
                    Runtime.Expect(availableAmount >= proxyAmount, "unsuficient amount for proxy distribution");
                    Runtime.Expect(Runtime.MintTokens(DomainSettings.FuelTokenSymbol, proxy.address, proxyAmount), "proxy fuel minting failed");
                    availableAmount -= proxyAmount;
                }
            }

            Runtime.Expect(availableAmount >= 0, "unsuficient leftovers");
            Runtime.Expect(Runtime.MintTokens(DomainSettings.FuelTokenSymbol, stakeAddress, availableAmount), "fuel minting failed");

            // NOTE here we set the full staked amount instead of claimed amount, to avoid infinite claims loophole
            var stake = _stakes.Get<Address, EnergyAction>(stakeAddress);

            if (stake.totalAmount == 0 && GetLastAction(stakeAddress).unclaimedPartials == 0)
                _stakes.Remove(from);

            var action = new EnergyAction()
            {
                unclaimedPartials = 0,
                totalAmount = stake.totalAmount,
                timestamp = Runtime.Time
            };

            _claims.Set<Address, EnergyAction>(stakeAddress, action);

            Runtime.Notify(EventKind.TokenClaim, from, new TokenEventData() { chainAddress = this.Address, symbol = DomainSettings.StakingTokenSymbol, value = unclaimedAmount });
            Runtime.Notify(EventKind.TokenMint, stakeAddress, new TokenEventData() { chainAddress = this.Address, symbol = DomainSettings.FuelTokenSymbol, value = fuelAmount });
        }

        public BigInteger GetStake(Address address)
        {
            BigInteger stake = 0;

            if (_stakes.ContainsKey(address))
            {
                stake = _stakes.Get<Address, EnergyAction>(address).totalAmount;
            }

            return stake;
        }

        public BigInteger GetStorageStake(Address address)
        {
            var usedStorageSize = Runtime.CallContext("storage", "GetUsedSpace", address).AsNumber();
            var usedStake = usedStorageSize * UnitConversion.ToBigInteger(1, DomainSettings.StakingTokenDecimals);
            usedStake = usedStake / (StorageContract.KilobytesPerStake * 1024);

            return usedStake;
        }

        public EnergyProxy[] GetProxies(Address address)
        {
            var list = _proxyStakersMap.Get<Address, StorageList>(address);
            return list.All<EnergyProxy>();
        }

        //returns the list of staking addresses that give a share of their rewards to the specified address
        public Address[] GetProxyStakers(Address address)
        {
            var receiversList = _proxyReceiversMap.Get<Address, StorageList>(address);
            return receiversList.All<Address>();
        }

        public void ClearProxies(Address from)
        {
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            var stakersList = _proxyStakersMap.Get<Address, StorageList>(from);
            var count = stakersList.Count();
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var proxy = stakersList.Get<EnergyProxy>(i);
                    Runtime.Notify(EventKind.AddressUnlink, from, proxy.address);

                    var receiversList = _proxyReceiversMap.Get<Address, StorageList>(proxy.address);
                    receiversList.Remove(from);
                }
                stakersList.Clear();
            }
        }

        public void AddProxy(Address from, Address to, BigInteger percentage)
        {
            Runtime.Expect(percentage > 0, "invalid percentage");
            Runtime.Expect(percentage <= 100, "invalid percentage");
            Runtime.Expect(from != to, "invalid proxy address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(!to.IsNull, "destination cannot be null address");
            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            var stakersList = _proxyStakersMap.Get<Address, StorageList>(from);
            var receiversList = _proxyReceiversMap.Get<Address, StorageList>(to);

            BigInteger sum = percentage;
            int index = -1;
            var count = stakersList.Count();
            for (int i = 0; i < count; i++)
            {
                var proxy = stakersList.Get<EnergyProxy>(i);

                Runtime.Expect(proxy.address != to, "repeated proxy address");

                /*if (proxy.address == to)
                {
                    sum += percentage;
                    index = i;
                }
                else
                {*/
                sum += proxy.percentage;
                //}
            }

            Runtime.Expect(sum <= 100, "invalid sum");

            var entry = new EnergyProxy() { percentage = (byte)percentage, address = to };
            //if (index < 0)
            //{
            stakersList.Add<EnergyProxy>(entry);
            receiversList.Add<Address>(from);
            /*}
            else
            {
                stakersList.Replace<EnergyProxy>(index, entry);
            }*/

            Runtime.Notify(EventKind.AddressLink, from, to);
        }

        public void RemoveProxy(Address from, Address to)
        {
            Runtime.Expect(from != to, "invalid proxy address");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");

            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            var stakersList = _proxyStakersMap.Get<Address, StorageList>(from);
            var receiversList = _proxyReceiversMap.Get<Address, StorageList>(to);

            int index = -1;
            var count = stakersList.Count();
            for (int i = 0; i < count; i++)
            {
                var proxy = stakersList.Get<EnergyProxy>(i);
                if (proxy.address == to)
                {
                    index = i;
                    break;
                }
            }

            stakersList.RemoveAt<EnergyProxy>(index);
            receiversList.Remove<Address>(from);
            Runtime.Notify(EventKind.AddressUnlink, from, to);
        }

        public static BigInteger FuelToStake(BigInteger fuelAmount)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * BaseEnergyRatioDivisor, DomainSettings.FuelTokenDecimals, DomainSettings.StakingTokenDecimals);
        }

        public static BigInteger StakeToFuel(BigInteger stakeAmount)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, DomainSettings.StakingTokenDecimals, DomainSettings.FuelTokenDecimals) / BaseEnergyRatioDivisor;
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

        private BigInteger CalculateRewardsWithHalving(BigInteger totalStake, BigInteger unclaimedPartials, Timestamp startTime, Timestamp endTime)
        {
            if (genesisTimestamp == 0)
            {
                Runtime.Expect(Runtime.IsRootChain(), "must be root chain");
                var genesisBlock = Runtime.GetBlockByHeight(1);
                if (genesisBlock == null)   //special case for genesis block's creation
                    return StakeToFuel(totalStake);

                genesisTimestamp = genesisBlock.Timestamp;
            }

            if (StakeToFuel(totalStake + unclaimedPartials) <= 0)
                return 0;

            DateTime genesisDate = genesisTimestamp;
            DateTime startDate = startTime;
            DateTime endDate = endTime;

            BigInteger reward = 0;
            uint halvingAmount = 1;
            var currentDate = startDate;
            var nextHalvingDate = genesisDate.AddYears(2);
            var partialRewardsFlag = true;

            while (currentDate <= endDate)
            {
                if (startDate < nextHalvingDate)
                {
                    var daysInCurrentHalving = 0;

                    if (partialRewardsFlag)
                    {
                        partialRewardsFlag = false;
                        reward += StakeToFuel(unclaimedPartials) / halvingAmount;
                    }

                    if (endDate > nextHalvingDate)
                    {
                        daysInCurrentHalving = (nextHalvingDate - currentDate).Days;
                        currentDate = nextHalvingDate;
                    }
                    else
                    {
                        daysInCurrentHalving = (endDate - currentDate).Days;

                        currentDate = endDate.AddDays(1);   //to force the while to break on next condition evaluation
                    }

                    reward += StakeToFuel(totalStake) * daysInCurrentHalving / halvingAmount;
                }

                nextHalvingDate = nextHalvingDate.AddYears(2);
                halvingAmount *= 2;
            }

            return reward;
        }

        private EnergyAction GetLastAction(Address address)
        {
            var lastClaim = _claims.Get<Address, EnergyAction>(address);
            var lastStake = _stakes.Get<Address, EnergyAction>(address);

            return lastClaim.timestamp >= lastStake.timestamp ? lastClaim : lastStake;
        }
    }
}
