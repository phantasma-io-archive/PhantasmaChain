using Phantasma.Blockchain.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class EpochContract : SmartContract
    {
        public override string Name => "epoch";

        public static readonly uint EpochDurationInSeconds = 60;
        public static readonly uint EpochSlashLimitInSeconds = 5;

        private Timestamp _epochStart;
        private Address _currentLeader;
        private Collection<Address> _epochParticipants;
        private BigInteger _unclaimedFees;

        public EpochContract() : base()
        {
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

            if (_currentLeader != Address.Null)
            {
                Runtime.Expect(address == _currentLeader, "not epoch address");

                var currentTime = Timestamp.Now;
                var diff = currentTime - _epochStart;
                Runtime.Expect(diff < EpochDurationInSeconds, "too late");
            }

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
            _currentLeader = Address.Null; // TODO select proper next address
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
