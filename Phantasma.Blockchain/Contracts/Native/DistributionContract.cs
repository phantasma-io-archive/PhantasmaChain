namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class DistributionContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Distribution; 

        public DistributionContract() : base()
        {
        }
    }
}
