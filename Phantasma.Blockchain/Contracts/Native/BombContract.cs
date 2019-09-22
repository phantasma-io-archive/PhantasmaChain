namespace Phantasma.Blockchain.Contracts.Native
{
    public sealed class BombContract : SmartContract
    {
        public override string Name => Nexus.BombContractName;

        public BombContract() : base()
        {
        }
    }
}
