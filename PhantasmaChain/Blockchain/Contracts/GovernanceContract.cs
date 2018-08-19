using Phantasma.Mathematics;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public class GovernanceContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Governance;

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
            return InitVotingRound(subject, value.ToByteArray());
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
