using Phantasma.Domain;

namespace Phantasma.Contracts.Native
{
    public sealed class SaleContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Sale;

        public SaleContract() : base()
        {
        }
    }
}
