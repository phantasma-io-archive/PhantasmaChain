using Phantasma.Core;
using Phantasma.Utils;
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

        public bool IsSigned()
        {
            throw new System.NotImplementedException();
        }

        public static Message Unserialize(BinaryReader reader)
        {
            var opcode = (Opcode)reader.ReadByte();
            var pubKey = reader.ReadByteArray();

            if (pubKey != null && pubKey.Length != KeyPair.PublicKeyLength)
            {
                pubKey = null;
            }

            Message msg;

            switch (opcode)
            {
                case Opcode.PEER_Join:
                    {
                        msg = PeerJoinMessage.FromReader(reader);
                        break;
                    }

                case Opcode.PEER_Leave:
                    {
                        msg = PeerLeaveMessage.FromReader(reader);
                        break;
                    }

                case Opcode.PEER_List:
                    {
                        msg = PeerListMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Request:
                    {
                        msg = RaftRequestMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Vote:
                    {
                        msg = RaftVoteMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Lead:
                    {
                        msg = RaftLeadMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Replicate:
                    {
                        msg = RaftReplicateMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Confirm:
                    {
                        msg = RaftConfirmMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Commit:
                    {
                        msg = RaftCommitMessage.FromReader(reader);
                        break;
                    }

                case Opcode.RAFT_Beat:
                    {
                        msg = RaftBeatMessage.FromReader(reader);
                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        msg = MempoolAddMessage.FromReader(reader);
                        break;
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        msg = MempoolGetMessage.FromReader(reader);
                        break;
                    }

                case Opcode.BLOCKS_Request:
                    {
                        msg = ChainRequestMessage.FromReader(reader);
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        msg = ChainRequestMessage.FromReader(reader);
                        break;
                    }

                case Opcode.CHAIN_Request:
                    {
                        msg = BlockRequestMessage.FromReader(reader);
                        break;
                    }

                case Opcode.CHAIN_Values:
                    {
                        msg = ChainValuesMessage.FromReader(reader);
                        break;
                    }

                case Opcode.SHARD_Submit:
                    {
                        msg = ShardSubmitMessage.FromReader(reader);
                        break;
                    }

                case Opcode.ERROR:
                    {
                        msg = ErrorMessage.FromReader(reader);
                        break;
                    }

                default: return null;
            }

            if (pubKey != null)
            {
                msg.Signature = reader.ReadByteArray();
            }

            return msg;
        }
    }

    public struct DeliveredMessage
    {
        public Message message;
        public Endpoint source;
    }
}
