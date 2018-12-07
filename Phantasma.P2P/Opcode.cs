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
        CHAIN_Values,
        CHAIN_Notify,
        SHARD_Submit,
        DHT_GET,
        DHT_SET,
    }
}
