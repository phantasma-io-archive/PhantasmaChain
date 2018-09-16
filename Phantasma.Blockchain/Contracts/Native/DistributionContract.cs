namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class DistributionContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Distribution; 

        public DistributionContract() : base()
        {
        }
    }
}
