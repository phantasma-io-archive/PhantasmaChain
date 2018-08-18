using System.Numerics;
using Phantasma.VM;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class StakeContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private byte[] _publicKey;
        public override byte[] PublicKey => _publicKey;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public StakeContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._publicKey = publicKey;
        }

        public void Stake(BigInteger amount)
        {
        }

        public void Unstake(BigInteger amount)
        {
        }

        public BigInteger GetStake(byte[] publicAddress)
        {
            throw new System.NotImplementedException();
        }

    }
}
