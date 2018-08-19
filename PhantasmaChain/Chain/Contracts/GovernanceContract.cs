using System.Numerics;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public class GovernanceContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public BigInteger FeeMultiplier = 1;

        public GovernanceContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._address = new Address(publicKey);
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
