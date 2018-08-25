namespace Phantasma.Blockchain.Contracts
{
    public sealed class StorageContract : NativeContract
    {
        internal override NativeContractKind Kind => NativeContractKind.Storage;

        public StorageContract() : base()
        {
        }
    }
}
