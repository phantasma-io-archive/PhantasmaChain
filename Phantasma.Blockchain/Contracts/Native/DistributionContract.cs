namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class DistributionContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Distribution; 

        public DistributionContract() : base()
        {
        }
    }
}
