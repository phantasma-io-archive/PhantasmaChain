namespace Phantasma.Blockchain
{
    public abstract class IChainPlugin
    {
        public abstract void OnTransaction(Chain chain, Block block, Transaction transaction);
    }
}
