using System;
using Phantasma.Storage.Context;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.Blockchain.Tokens;

namespace Phantasma.Blockchain.Contracts.Native
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

    public sealed class EnergyContract : SmartContract
    {
        public override string Name => "energy";

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

        public readonly static BigInteger MasterAccountThreshold = UnitConversion.ToBigInteger(50000, Nexus.StakingTokenDecimals);
        public readonly static BigInteger MasterClaimGlobalAmount = UnitConversion.ToBigInteger(125000, Nexus.StakingTokenDecimals);

        public readonly static BigInteger BaseEnergyRatioDivisor = 500; // used as 1/500, will generate 0.002 per staked token
        public static BigInteger MinimumValidStake => UnitConversion.GetUnitValue(Nexus.StakingTokenDecimals);

        public readonly static BigInteger MaxVotingPowerBonus = 1000;
        public readonly static BigInteger DailyVotingBonus = 1;

        public EnergyContract() : base()
        {
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

            return new EnergyMaster{address=Address.Null};
        }

        public bool IsMaster(Address address)
        {
            return GetMaster(address).address != Address.Null;
        }

        public BigInteger GetCurrentMasterCount()
        {
            return _mastersList.Count();
        }

        //verifies how many valid masters are in the condition to claim the reward for a specific master claim date, assuming no changes in their master status in the meantime
        public BigInteger GetClaimMasterCount(Timestamp claimDate)
        {
            var count = GetCurrentMasterCount();
            var result = count;
            DateTime requestedClaimDate = new DateTime(((DateTime) claimDate).Year, ((DateTime)claimDate).Month, 1);

            for (int i = 0; i < count; i++)
            {
                DateTime currentMasterClaimDate = _mastersList.Get<EnergyMaster>(i).claimDate;
                if ( currentMasterClaimDate > requestedClaimDate)
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
                referenceDate = referenceTime;
            else if (_lastMasterClaim.Value == 0)
                referenceDate = Runtime.Nexus.RootChain.FindBlockByHeight(1).Timestamp;
            else
                referenceDate = _lastMasterClaim;

            var nextMasterClaim = (Timestamp)(new DateTime(referenceDate.Year, referenceDate.Month, 1, 0, 0, 0)).AddMonths((int) claimDistance);
            var dateTimeClaim = (DateTime) nextMasterClaim;

            if (dateTimeClaim.Hour == 23)
                nextMasterClaim = dateTimeClaim.AddHours(1);
            if (dateTimeClaim.Hour == 1)
                nextMasterClaim = dateTimeClaim.AddHours(-1);

            //Allow a claim once per month starting on the 1st day of each month
            return nextMasterClaim;
        }

        public BigInteger GetMasterRewards(Address from)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
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
        public void Migrate(Address source, Address target)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(!to.IsInterop, "destination cannot be interop address");

            var targetStake = _stakes.Get<Address, EnergyAction>(target);
            Runtime.Expect(targetStake.totalAmount == 0, "Tried to migrate to an account that's already staking");

            //migrate stake
            var sourceStake = _stakes.Get<Address, EnergyAction>(source);
            
            _stakes.Set(target, sourceStake);
            _stakes.Remove(source);

            //migrate master claim
            if (sourceStake.totalAmount >= MasterAccountThreshold)
            {
                var count = _mastersList.Count();
                var index = -1;
                Timestamp claimDate;
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

                Runtime.Expect(index >= 0,"Expected this address to be a master");

                _mastersList.RemoveAt<EnergyMaster>(index);
                _mastersList.Add(new EnergyMaster() { address = target, claimDate = claimDate });
            }

            //migrate voting power
            var votingLogbook = _voteHistory.Get<Address, StorageList>(source);
            votingLogbook.Add(target);
            votingLogbook.Remove(source);
        }

        public void MasterClaim(Address from)
        {
            Runtime.Expect(_masterClaimCount < 12 * 4, "no more claims available"); // 4 years

            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            var thisClaimDate = GetMaster(from).claimDate;
            Runtime.Expect(Runtime.Time >= thisClaimDate, "not enough time waited");

            var symbol = Nexus.StakingTokenSymbol;
            var token = Runtime.Nexus.GetTokenInfo(symbol);

            var totalAmount = MasterClaimGlobalAmount;
            Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, token.Symbol, Runtime.Chain.Address, totalAmount), "mint failed");

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

                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, token.Symbol, Runtime.Chain.Address, targetMaster.address, transferAmount), "transfer failed");

                totalAmount -= transferAmount;

                Runtime.Notify(EventKind.TokenMint, targetMaster.address, new TokenEventData() { symbol = token.Symbol, value = transferAmount, chainAddress = Runtime.Chain.Address });

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
            Runtime.Expect(IsWitness(from), "witness failed");

            var stakeBalances = new BalanceSheet(Nexus.StakingTokenSymbol);
            var balance = stakeBalances.Get(this.Storage, from);

            var currentStake = _stakes.Get<Address, EnergyAction>(from);

            var newStake = stakeAmount + currentStake.totalAmount;

            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(stakeBalances.Subtract(this.Storage, from, stakeAmount), "balance subtract failed");
            Runtime.Expect(stakeBalances.Add(this.Storage, Runtime.Chain.Address, stakeAmount), "balance add failed");

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

            if (Runtime.Nexus.GenesisAddress != from && newStake >= MasterAccountThreshold && !IsMaster(from))
            {
                var nextClaim = GetMasterClaimDate(2);

                _mastersList.Add(new EnergyMaster() { address = from, claimDate = nextClaim });
                Runtime.Notify(EventKind.RolePromote, from, new RoleEventData() { role = "master", date = nextClaim });
            }

            Runtime.Notify(EventKind.TokenStake, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Nexus.StakingTokenSymbol, value = stakeAmount});
        }

        public BigInteger Unstake(Address from, BigInteger unstakeAmount)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
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

            var diff = Runtime.Time - stake.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 1, "waiting period required");

            var token = Runtime.Nexus.GetTokenInfo(Nexus.StakingTokenSymbol);
            var balances = new BalanceSheet(token.Symbol);
            var balance = balances.Get(this.Storage, Runtime.Chain.Address);
            Runtime.Expect(balance >= unstakeAmount, "not enough balance");

            var availableStake = stake.totalAmount;
            availableStake -= GetStorageStake(from);
            Runtime.Expect(availableStake >= unstakeAmount, "tried to unstake more than what was staked");

            //if this is a partial unstake
            if(availableStake - unstakeAmount > 0)
                Runtime.Expect(availableStake - unstakeAmount >= MinimumValidStake, "leftover stake would be below minimum staking amount" );

            Runtime.Expect(balances.Subtract(this.Storage, Runtime.Chain.Address, unstakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(this.Storage, from, unstakeAmount), "balance add failed");

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

            if (stake.totalAmount < MasterAccountThreshold)
            {
                var count = _mastersList.Count();
                var index = -1;
                for (int i=0; i<count; i++)
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

            Runtime.Notify(EventKind.TokenUnstake, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = token.Symbol, value = unstakeAmount });

            return unstakeAmount;
        }

        public BigInteger GetTimeBeforeUnstake(Address from)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            if (!_stakes.ContainsKey<Address>(from))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(from);
            return 86400 - (Runtime.Time - stake.timestamp);

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

            var currentTime = Runtime.Time;

            if (lastClaim.timestamp.Value == 0)
                lastClaim.timestamp = stake.timestamp;

            var diff = currentTime - lastClaim.timestamp;

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

            return CalculateRewardsWithHalving(currentStake, GetLastAction(stakeAddress).unclaimedPartials, lastClaim.timestamp, currentTime); ;
        }

        public void Claim(Address from, Address stakeAddress)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

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
                    Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, Nexus.FuelTokenSymbol, proxy.address, proxyAmount), "proxy fuel minting failed");
                    availableAmount -= proxyAmount;
                }
            }

            Runtime.Expect(availableAmount >= 0, "unsuficient leftovers");
            Runtime.Expect(Runtime.Nexus.MintTokens(Runtime, Nexus.FuelTokenSymbol, stakeAddress, availableAmount), "fuel minting failed");

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

            Runtime.Notify(EventKind.TokenClaim, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Nexus.StakingTokenSymbol, value = unclaimedAmount});
            Runtime.Notify(EventKind.TokenMint, stakeAddress, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Nexus.FuelTokenSymbol, value = fuelAmount });
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
            var temp = Runtime.CallContext("storage", "GetUsedSpace", address);
            var usedStorageSize = (BigInteger)temp;
            var usedStake = usedStorageSize * UnitConversion.ToBigInteger(1, Nexus.StakingTokenDecimals);
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
            Runtime.Expect(IsWitness(from), "invalid witness");

            var stakersList = _proxyStakersMap.Get<Address, StorageList>(from);
            var count = stakersList.Count();
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var proxy = stakersList.Get<EnergyProxy>(i);
                    Runtime.Notify(EventKind.AddressRemove, from, proxy.address);

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
            Runtime.Expect(IsWitness(from), "invalid witness");

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

            Runtime.Notify(EventKind.AddressAdd, from, to);
        }

        public void RemoveProxy(Address from, Address to)
        {
            Runtime.Expect(from != to, "invalid proxy address");
            Runtime.Expect(IsWitness(from), "invalid witness");

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
            Runtime.Notify(EventKind.AddressRemove, from, to);
        }

        public static BigInteger FuelToStake(BigInteger fuelAmount)
        {
            return UnitConversion.ConvertDecimals(fuelAmount * BaseEnergyRatioDivisor, Nexus.FuelTokenDecimals, Nexus.StakingTokenDecimals);
        }

        public static BigInteger StakeToFuel(BigInteger stakeAmount)
        {
            return UnitConversion.ConvertDecimals(stakeAmount, Nexus.StakingTokenDecimals, Nexus.FuelTokenDecimals) / BaseEnergyRatioDivisor;
        }

        public BigInteger GetAddressVotingPower(Address address)
        {
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
                var genesisBlock = Runtime.Nexus.RootChain.FindBlockByHeight(1);
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

            return lastClaim.timestamp > lastStake.timestamp ? lastClaim : lastStake;
        }
    }
}
