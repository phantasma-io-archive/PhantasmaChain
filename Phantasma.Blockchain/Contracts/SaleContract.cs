using Phantasma.Domain;

namespace Phantasma.Blockchain.Contracts
{
    public sealed class SaleContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Sale;

        public SaleContract() : base()
        {
        }
    }
}
