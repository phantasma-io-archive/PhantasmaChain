using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public enum GovernanceSubject
    {
        NetworkProtocol,    // controls version of network protocol
        FeeMultiplier,      // controls multiplier used for fee calculation
        FeeAllocation,      // controls percentage of distribution of block fees
        GovernanceContract, // TODO control system contract migration
        DistributionContract, // TODO 
        StakeContract,
        StakeLimit, // minimum stakable amount
        BlockLimit,
        TransactionLimit,
        ShardLimit
    }

    public class GovernanceContract : SmartContract
    {
        public override string Name => "governance";

        public BigInteger FeeMultiplier = 1;

        public GovernanceContract() : base()
        {
        }

        public bool InitVotingRound(GovernanceSubject subject, byte[] value)
        {
            throw new System.NotImplementedException();
        }

        public bool InitVotingRound(GovernanceSubject subject, BigInteger value)
        {
            return InitVotingRound(subject, value.ToSignedByteArray());
        }

        public byte[] GetGovernanceBytes(GovernanceSubject subject)
        {
            throw new System.NotImplementedException();
        }

        public BigInteger GetGovernanceValue(GovernanceSubject subject)
        {
            return new BigInteger(GetGovernanceBytes(subject));
        }

        public void Vote(GovernanceSubject subject, bool vote)
        {
        }

    }
}
