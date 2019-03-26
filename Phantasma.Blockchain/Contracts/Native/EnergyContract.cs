using System;
using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct EnergyAction
    {
        public BigInteger amount;
        public Timestamp timestamp;
    }

    public struct EnergyProxy
    {
        public Address address;
        public byte percentage;
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
        private StorageMap _proxyMap; // <Address, List<EnergyProxy>>
        private StorageMap _claims; // <Address, EnergyAction>
        private StorageList _mastersList; // <Address>
        private Timestamp _lastMasterClaim;
        private StorageMap _masterMemory; // <Address, Timestamp>
        private uint _masterClaimCount;

        private Timestamp genesisTimestamp = 0;

        public readonly static BigInteger MasterAccountThreshold = UnitConversion.ToBigInteger(50000, Nexus.StakingTokenDecimals);
        public readonly static BigInteger MasterClaimGlobalAmount = UnitConversion.ToBigInteger(125000, Nexus.StakingTokenDecimals);

        public readonly static BigInteger BaseEnergyRatioDivisor = 500; // used as 1/500, will generate 0.002 per staked token
 
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

        public void MasterClaim(Address from)
        {
            Runtime.Expect(_masterClaimCount < 12 * 4, "no more claims available"); // 4 years

            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(IsMaster(from), "invalid master");

            var thisClaimDate = GetMaster(from).claimDate;
            Runtime.Expect(Runtime.Time >= thisClaimDate, "not enough time waited");

            var token = Runtime.Nexus.StakingToken;
            var stakeBalances = Runtime.Chain.GetTokenBalances(token);
            var stakeSupplies = Runtime.Chain.GetTokenSupplies(token);

            var totalAmount = MasterClaimGlobalAmount;
            Runtime.Expect(token.Mint(Runtime.ChangeSet, stakeBalances, stakeSupplies, Runtime.Chain.Address, totalAmount), "mint failed");

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

                Runtime.Expect(token.Transfer(Runtime.ChangeSet, stakeBalances, Runtime.Chain.Address, targetMaster.address, transferAmount), "transfer failed");

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
            Runtime.Expect(StakeToFuel(stakeAmount) >= 1, "invalid amount");
            Runtime.Expect(IsWitness(from), "witness failed");

            var stakeToken = Runtime.Nexus.StakingToken;
            var stakeBalances = Runtime.Chain.GetTokenBalances(stakeToken);
            var balance = stakeBalances.Get(this.Storage, from);

            var currentStake = _stakes.Get<Address, EnergyAction>(from).amount;
            var newStake = stakeAmount + currentStake;

            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(stakeBalances.Subtract(this.Storage, from, stakeAmount), "balance subtract failed");
            Runtime.Expect(stakeBalances.Add(this.Storage, Runtime.Chain.Address, stakeAmount), "balance add failed");

            var entry = new EnergyAction()
            {
                amount = newStake,
                timestamp = this.Runtime.Time,
            };
            _stakes.Set(from, entry);

            if (Runtime.Nexus.GenesisAddress != from && newStake >= MasterAccountThreshold && !IsMaster(from))
            {
                var nextClaim = GetMasterClaimDate(2);

                /*
                // check for unstaking penalization
                if (_masterMemory.ContainsKey<Address>(from))
                {
                    var lastClaim = _masterMemory.Get<Address, Timestamp>(from);
                    if (lastClaim > nextClaim)
                    {
                        nextClaim = lastClaim;
                    }
                }*/

                _mastersList.Add(new EnergyMaster() { address = from, claimDate = nextClaim});
                Runtime.Notify(EventKind.MasterPromote, from, nextClaim);
            }

            Runtime.Notify(EventKind.TokenStake, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = stakeToken.Symbol, value = newStake });
        }

        public BigInteger Unstake(Address from, BigInteger amount)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

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

            var token = Runtime.Nexus.StakingToken;
            var balances = Runtime.Chain.GetTokenBalances(token);
            var balance = balances.Get(this.Storage, Runtime.Chain.Address);
            Runtime.Expect(balance >= amount, "not enough balance");

            Runtime.Expect(stake.amount >= amount, "tried to unstake more than what was staked");

            Runtime.Expect(balances.Subtract(this.Storage, Runtime.Chain.Address, amount), "balance subtract failed");
            Runtime.Expect(balances.Add(this.Storage, from, amount), "balance add failed");

            stake.amount -= amount;
            
            if (stake.amount == 0)
                _stakes.Remove(from);
            else
            {
                var entry = new EnergyAction()
                {
                    amount = stake.amount,
                    timestamp = this.Runtime.Time,
                };

                _stakes.Set(from, entry);
            }

            if (stake.amount < MasterAccountThreshold)
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

                    Runtime.Notify(EventKind.MasterDemote, from, penalizationDate);
                }
            }

            Runtime.Notify(EventKind.TokenUnstake, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = token.Symbol, value = amount });

            return amount;
        }

        public BigInteger GetUnclaimed(Address stakeAddress)
        {
            if (!_stakes.ContainsKey<Address>(stakeAddress))
            {
                return 0;
            }

            var stake = _stakes.Get<Address, EnergyAction>(stakeAddress);

            if (stake.timestamp.Value == 0) // failsafe, should never happen
            {
                return 0;
            }

            var currentStake = stake.amount;

            var lastClaim = _claims.Get<Address, EnergyAction>(stakeAddress);

            var currentTime = Runtime.Time;

            if (lastClaim.timestamp.Value == 0)
                lastClaim.timestamp = currentTime;

            var diff = currentTime - lastClaim.timestamp;

            var days = diff / 86400; // convert seconds to days

            // if not enough time has passed, deduct the last claim from the available amount
            if (days <= 0)
            {
                currentStake -= lastClaim.amount;
            }
            
            // clamp to avoid negative values
            if (currentStake < 0)
            {
                currentStake = 0;
            }

            return CalculateRewardsWithHalving(currentStake, lastClaim.timestamp, currentTime); ;
        }

        public void Claim(Address from, Address stakeAddress)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var unclaimedAmount = GetUnclaimed(stakeAddress);

            Runtime.Expect(unclaimedAmount > 0, "nothing unclaimed");

            var stakeToken = Runtime.Nexus.StakingToken;
            
            var fuelToken = Runtime.Nexus.FuelToken;
            var fuelBalances = Runtime.Chain.GetTokenBalances(fuelToken);
            var fuelAmount = unclaimedAmount;

            // distribute to proxy list
            var list = _proxyMap.Get<Address, StorageList>(stakeAddress);
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
            var fuelSupplies = Runtime.Chain.GetTokenSupplies(fuelToken);
            for (int i = 0; i < count; i++)
            {
                var proxy = list.Get<EnergyProxy>(i);
                sum += proxy.percentage;

                var proxyAmount = (fuelAmount * proxy.percentage) / 100;
                if (proxyAmount > 0)
                {
                    Runtime.Expect(availableAmount >= proxyAmount, "unsuficient amount for proxy distribution");
                    Runtime.Expect(fuelToken.Mint(this.Storage, fuelBalances, fuelSupplies, proxy.address, proxyAmount), "proxy fuel minting failed");
                    availableAmount -= proxyAmount;
                }
            }

            Runtime.Expect(availableAmount >= 0, "unsuficient leftovers");
            Runtime.Expect(fuelToken.Mint(this.Storage, fuelBalances, fuelSupplies, stakeAddress, availableAmount), "fuel minting failed");

            // NOTE here we set the full staked amount instead of claimed amount, to avoid infinite claims loophole
            var stake = _stakes.Get<Address, EnergyAction>(stakeAddress);
            Runtime.Expect(stake.amount > 0, "stake missing"); // failsafe, should never happen
            var action = new EnergyAction() { amount = stake.amount, timestamp = Runtime.Time };
            _claims.Set<Address, EnergyAction>(stakeAddress, action);

            Runtime.Notify(EventKind.TokenClaim, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = stakeToken.Symbol, value = unclaimedAmount});
            Runtime.Notify(EventKind.TokenMint, stakeAddress, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = fuelToken.Symbol, value = fuelAmount });
        }

        public BigInteger GetStake(Address address)
        {
            BigInteger stake = 0;

            if (_stakes.ContainsKey(address))
                stake = _stakes.Get<Address, EnergyAction>(address).amount;

            return stake;
        }

        public EnergyProxy[] GetProxies(Address address)
        {
            var list = _proxyMap.Get<Address, StorageList>(address);
            return list.All<EnergyProxy>();
        }

        public void ClearProxies(Address from)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var list = _proxyMap.Get<Address, StorageList>(from);
            var count = list.Count();
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    var proxy = list.Get<EnergyProxy>(i);
                    Runtime.Notify(EventKind.AddressRemove, from, proxy.address);

                }
                list.Clear();
            }
        }

        public void AddProxy(Address from, Address to, BigInteger percentage)
        {
            Runtime.Expect(percentage > 0, "invalid percentage");
            Runtime.Expect(percentage <= 100, "invalid percentage");
            Runtime.Expect(from != to, "invalid proxy address");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var list = _proxyMap.Get<Address, StorageList>(from);

            BigInteger sum = percentage;
            int index = -1;
            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var proxy = list.Get<EnergyProxy>(i);

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
            if (index < 0)
            {
                list.Add<EnergyProxy>(entry);
            }
            else
            {
                list.Replace<EnergyProxy>(index, entry);
            }

            Runtime.Notify(EventKind.AddressAdd, from, to);
        }

        public void RemoveProxy(Address from, Address to)
        {
            Runtime.Expect(from != to, "invalid proxy address");
            Runtime.Expect(IsWitness(from), "invalid witness");

            var list = _proxyMap.Get<Address, StorageList>(from);

            int index = -1;
            var count = list.Count();
            for (int i = 0; i < count; i++)
            {
                var proxy = list.Get<EnergyProxy>(i);
                if (proxy.address == to)
                {
                    index = i;
                    break;
                }
            }
           
            Runtime.Expect(index>=0, "proxy not found");

            list.RemoveAt<EnergyProxy>(index);
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

        private BigInteger CalculateRewardsWithHalving(BigInteger stake, Timestamp startTime, Timestamp endTime)
        {
            if (genesisTimestamp == 0)
            {
                var genesisBlock = Runtime.Nexus.RootChain.FindBlockByHeight(1);
                if (genesisBlock == null)   //special case for genesis block's creation
                    return StakeToFuel(stake);

                genesisTimestamp = genesisBlock.Timestamp;
            }
           
            if (StakeToFuel(stake) <= 0)
                return 0;

            DateTime genesisDate = genesisTimestamp;
            DateTime startDate = startTime;
            DateTime endDate = endTime;

            BigInteger reward = 0;
            uint halvingAmount = 1;
            var currentDate = startDate;
            var nextHalvingDate = genesisDate.AddYears(2);

            while (currentDate <= endDate)
            {
                if (startDate < nextHalvingDate)
                {
                    var daysInCurrentHalving = 0;
                    if (endDate > nextHalvingDate)
                    {
                        daysInCurrentHalving = (nextHalvingDate - currentDate).Days;
                        currentDate = nextHalvingDate;
                    }
                    else
                    {
                        daysInCurrentHalving = (endDate - currentDate).Days;

                        if (currentDate == startDate && daysInCurrentHalving == 0)
                            daysInCurrentHalving = 1;

                        currentDate = endDate.AddDays(1);   //to force the while to break
                    }

                    reward += (StakeToFuel(stake) / halvingAmount) * daysInCurrentHalving;
                }

                nextHalvingDate = nextHalvingDate.AddYears(2);
                halvingAmount *= 2;
            }

            return reward;
        }

    }
}
