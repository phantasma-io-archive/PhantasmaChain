namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class VaultContract : NativeContract
    {
        internal override ContractKind Kind => ContractKind.Vault;

        public VaultContract() : base()
        {
        }
    }
}
