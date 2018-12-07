namespace Phantasma.Network.P2P
{
    public enum Opcode
    {
        ERROR,
        PEER_Join,
        PEER_Leave,
        PEER_List,
        MEMPOOL_Add,
        MEMPOOL_Get,
        BLOCKS_Request,
        BLOCKS_List,
        CHAIN_Request,
        CHAIN_List,
        EPOCH_Request,
        EPOCH_List,
        EPOCH_Propose,
        EPOCH_Submit,
        EPOCH_Vote,
    }
}
