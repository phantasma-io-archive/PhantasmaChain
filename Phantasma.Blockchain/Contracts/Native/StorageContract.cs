namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class StorageContract : NativeContract
    {
        public override ContractKind Kind => ContractKind.Storage;

        public StorageContract() : base()
        {
        }
    }
}
