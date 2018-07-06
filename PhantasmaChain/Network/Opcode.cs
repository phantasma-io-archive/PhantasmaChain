namespace Phantasma.Network
{
    public enum Opcode
    {
        ERROR,
        PEER_Join,
        PEER_Leave,
        PEER_List,
        RAFT_Request,
        RAFT_Vote,
        RAFT_Lead,
        RAFT_Replicate,
        RAFT_Confirm,
        RAFT_Commit,
        RAFT_Beat,
        MEMPOOL_Add,
        MEMPOOL_Get,
        BLOCKS_Request,
        BLOCKS_List,
        CHAIN_Request,
        CHAIN_Values,
        SHARD_Submit,
        DHT_GET,
        DHT_SET,
    }
}
