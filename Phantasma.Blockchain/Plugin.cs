namespace Phantasma.Blockchain
{
    public abstract class IChainPlugin
    {
        public abstract void OnBlock(Chain chain, Block block);
        public abstract void OnTransaction(Chain chain, Block block, Transaction transaction);
    }
}
