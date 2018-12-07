namespace Phantasma.Network.P2P
{
    public enum Opcode
    {
        ERROR,
        PEER_Identity,
        PEER_List,
        MEMPOOL_List,
        MEMPOOL_Add,
        BLOCKS_List,
        CHAIN_List,
        EPOCH_List,
        EPOCH_Propose,
        EPOCH_Submit,
        EPOCH_Vote,
    }
}
