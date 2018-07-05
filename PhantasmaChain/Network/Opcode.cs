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
        CHAIN_Height,
        CHAIN_Get,
        SHARD_Submit,
        DHT_GET,
        DHT_SET,
    }
}
