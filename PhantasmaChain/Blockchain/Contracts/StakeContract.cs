using Phantasma.Mathematics;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class StakeContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Stake;

        public StakeContract() : base()
        {
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
