using Phantasma.VM;
using Phantasma.VM.Types;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class DistributionContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Distribution; 

        public DistributionContract(Chain chain) : base(chain)
        {
        }
    }
}
