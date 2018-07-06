using System.IO;

namespace Phantasma.Network
{
	/// <summary>
	/// Represents a generic network message
	/// </summary>
	public abstract class Message
	{
        public Opcode Opcode { get; private set; }
        public byte[] PublicKey { get; private set; }
        public byte[] Signature { get; private set; }

        public bool IsSigned() {
            throw new System.NotImplementedException();
        }

        public static Message Unserialize(BinaryReader reader) {
            var opcode = (Opcode)reader.ReadByte();

            switch (opcode) {
                case Opcode.PEER_Join:
                    {
                        return PeerJoinMessage.FromReader(reader);
                    }

                case Opcode.PEER_Leave:
                    {
                        return PeerLeaveMessage.FromReader(reader);
                    }

                case Opcode.PEER_List:
                    {
                        return PeerListMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Request:
                    {
                        return RaftRequestMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Vote:
                    {
                        return RaftVoteMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Lead:
                    {
                        return RaftLeadMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Replicate:
                    {
                        return RaftReplicateMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Confirm:
                    {
                        return RaftConfirmMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Commit:
                    {
                        return RaftCommitMessage.FromReader(reader);
                    }

                case Opcode.RAFT_Beat:
                    {
                        return RaftBeatMessage.FromReader(reader);
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        return MempoolAddMessage.FromReader(reader);
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        return MempoolGetMessage.FromReader(reader);
                    }

                case Opcode.BLOCKS_Request:
                    {
                        return ChainRequestMessage.FromReader(reader);
                    }

                case Opcode.BLOCKS_List:
                    {
                        return ChainRequestMessage.FromReader(reader);
                    }

                case Opcode.CHAIN_Request:
                    {
                        return BlockRequestMessage.FromReader(reader);
                    }

                case Opcode.CHAIN_Values:
                    {
                        return new ChainValuesMessage.FromReader(reader);
                    }

                case Opcode.SHARD_Submit:
                    {
                        return ShardSubmitMessage.FromReader(reader);
                    }

                case Opcode.ERROR:
                    {
                        return ErrorMessage.FromReader(reader);
                    }

                default: return null;
            }
        }
    }

    public struct DeliveredMessage {
        public Message message;
        public Endpoint source;
    }
}
