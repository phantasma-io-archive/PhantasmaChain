using Phantasma.VM;
using System.Numerics;

namespace Phantasma.Core
{
    public class FeeGovernanceContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public BigInteger FeeMultiplier = 1;

        public FeeGovernanceContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._publicKey = publicKey;
        }

        public void InitVotingRound()
        {
        }

        public void Vote(BigInteger value)
        {
        }

    }
}
