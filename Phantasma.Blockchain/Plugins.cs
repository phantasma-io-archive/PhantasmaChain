namespace Phantasma.Blockchain
{
    public interface INexusPlugin
    {
        void OnNewChain(Chain chain);
        void OnNewBlock(Chain chain, Block block);
        void OnNewTransaction(Chain chain, Block block, Transaction transaction);
    }
}
