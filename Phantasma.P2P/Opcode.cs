namespace Phantasma.Network.P2P
{
    public enum Opcode
    {
        ERROR,
        REQUEST,
        LIST,
        MEMPOOL_Add,
        BLOCKS_List,
        EPOCH_List,
        EPOCH_Propose,
        EPOCH_Submit,
        EPOCH_Vote,
    }
}
