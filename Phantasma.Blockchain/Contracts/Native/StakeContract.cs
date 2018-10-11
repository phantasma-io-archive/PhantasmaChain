using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class StakeContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Stake;

        public StakeContract() : base()
        {
        }

        public void Stake(BigInteger amount)
        {
        }

        public void Unstake(BigInteger amount)
        {
        }

        public BigInteger GetStake(Address address)
        {
            throw new System.NotImplementedException();
        }

    }
}
