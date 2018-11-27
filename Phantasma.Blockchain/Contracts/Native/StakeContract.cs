using Phantasma.Cryptography;
using Phantasma.Numerics;

namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class StakeContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Custom;

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
