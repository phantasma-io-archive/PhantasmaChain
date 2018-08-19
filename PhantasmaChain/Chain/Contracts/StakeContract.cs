using System.Numerics;
using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class StakeContract : Contract
    {
        public static readonly byte[] DefaultScript = new byte[] { (byte)Opcode.RET };
        public static readonly byte[] DefaultABI = new byte[] { };

        private Address _address;
        public override Address Address => _address;

        public override byte[] Script => DefaultScript;
        public override byte[] ABI => DefaultABI;

        public StakeContract(Chain chain, byte[] publicKey) : base(chain)
        {
            this._address = new Address(publicKey);
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
