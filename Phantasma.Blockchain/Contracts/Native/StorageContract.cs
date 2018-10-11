namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class StorageContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Storage;

        public StorageContract() : base()
        {
        }
    }
}
