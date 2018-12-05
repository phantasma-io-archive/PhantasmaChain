using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public struct ValidatorInfo
    {
        public Address address;
        public BigInteger stake;
        public Timestamp timestamp;
        public int slashes;
    }

    public sealed class StakeContract : SmartContract
    {
        public override string Name => "stake";

        public static readonly uint EpochDurationInSeconds = 60;
        public static readonly uint EpochSlashLimitInSeconds = 5;

        private Timestamp _epochStart;
        private Address _currentLeader;
        private Collection<Address> _epochParticipants;
        private BigInteger _unclaimedFees;

        private Collection<Address> _entryList;
        private Map<Address, ValidatorInfo> _entryMap;

        public StakeContract() : base()
        {
        }

        public int GetMaxValidators()
        {
            return 3; // TODO this should be dynamic
        }

        public BigInteger GetRequiredStake()
        {
            return TokenUtils.ToBigInteger(50000, Nexus.NativeTokenDecimals); // TODO this should be dynamic
        }

        public Address[] GetValidators()
        {
            return _entryList.All();
        }

        public bool IsValidator(Address address)
        {
            return _entryMap.ContainsKey(address);
        }

        public void Stake(Address address, BigInteger amount)
        {
            Runtime.Expect(IsWitness(address), "witness failed");

            var count = _entryList.Count();
            var max = GetMaxValidators();
            Runtime.Expect(count < max, "no open validators spots");

            var stakeAmount = GetRequiredStake();

            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(Runtime.Chain.Address, stakeAmount), "balance add failed");

            _entryList.Add(address);

            var entry = new ValidatorInfo()
            {
                address = address,
                stake = stakeAmount,
                timestamp = Timestamp.Now,
                slashes = 0
            };
            _entryMap.Set(address, entry);

            // if there is no leader yet, create a new epoch with this as leader
            if (_entryList.Count() == 1)
            {
                _currentLeader = address;
                _epochStart = Timestamp.Now;
                // TODO more required?
            }
        }

        public void Unstake(Address address)
        {
            Runtime.Expect(IsValidator(address), "validator failed");
            Runtime.Expect(IsWitness(address), "witness failed");

            var entry = _entryMap.Get(address);

            var diff = Timestamp.Now - entry.timestamp;
            var days = diff / 86400; // convert seconds to days

            Runtime.Expect(days >= 30, "waiting period required");

            var stakeAmount = entry.stake;
            var balances = Runtime.Chain.GetTokenBalances(Runtime.Nexus.NativeToken);
            var balance = balances.Get(Runtime.Chain.Address);
            Runtime.Expect(balance >= stakeAmount, "not enough balance");

            Runtime.Expect(balances.Subtract(Runtime.Chain.Address, stakeAmount), "balance subtract failed");
            Runtime.Expect(balances.Add(address, stakeAmount), "balance add failed");

            _entryMap.Remove(address);

            _entryList.Remove(address);
        }

        public BigInteger GetStake(Address address)
        {
            Runtime.Expect(_entryMap.ContainsKey(address), "not a validator address");
            var entry = _entryMap.Get(address);
            return entry.stake;
        }

        private void AccumulateFees()
        {
            BigInteger txFee = 0; // Runtime.Chain.GetBlockReward(Runtime.Block); TODO fixme
            _unclaimedFees += txFee;

            var token = this.Runtime.Nexus.NativeToken;
            var balances = this.Runtime.Chain.GetTokenBalances(token);
            //Runtime.Expect(token.Burn(balances, from, amount), "burn failed");
        }

        public void ContinueEpoch(Address address)
        {
            Runtime.Expect(IsValidator(address), "validator failed");
            Runtime.Expect(IsWitness(address), "witness failed");

            Runtime.Expect(address == _currentLeader, "not epoch address");

            var currentTime = Timestamp.Now;
            var diff = currentTime - _epochStart;
            Runtime.Expect(diff < EpochDurationInSeconds, "too late");

            if (!_epochParticipants.Contains(address))
            {
                _epochParticipants.Add(address);
            }

            AccumulateFees();
        }

        private void DistributeEpoch(bool slashed)
        {
            var count = _epochParticipants.Count();
            var distributionAmount = _unclaimedFees / count;
            var leftovers = _unclaimedFees - (distributionAmount * count);

            var token = Runtime.Nexus.NativeToken;
            var balances = Runtime.Chain.GetTokenBalances(token);
            
            for (int i = 0; i < count; i++)
            {
                var participant = _epochParticipants.Get(i);
                BigInteger amountToReceive = 0;

                if (participant == _currentLeader)
                {
                    amountToReceive += leftovers;

                    if (!slashed)
                    {
                        amountToReceive = distributionAmount;
                    }
                }
                else
                {
                    amountToReceive = distributionAmount;
                }

                Runtime.Expect(token.Mint(balances, participant, amountToReceive), "mint failed");
            }

            _unclaimedFees = 0;
            // _currentLeader = _nextLeader; TODO
        }

        public void CloseEpoch(Address address)
        {
            Runtime.Expect(IsValidator(address), "validator failed");
            Runtime.Expect(IsWitness(address), "witness failed");

            Runtime.Expect(_unclaimedFees > 0, "epoch not active");
            Runtime.Expect(address == _currentLeader, "not epoch address");

            var currentTime = Timestamp.Now;
            var diff = currentTime - _epochStart;
            Runtime.Expect(diff >= EpochDurationInSeconds, "too soon");

            diff -= EpochDurationInSeconds;

            if (address == _currentLeader)
            {
                Runtime.Expect(diff < EpochSlashLimitInSeconds, "too late, slashed");
            }
            else
            {
                Runtime.Expect(diff >= EpochSlashLimitInSeconds, "too soon for slash");
            }

            AccumulateFees();

            var slashed = address != _currentLeader;
            DistributeEpoch(slashed);
        }

    }
}
