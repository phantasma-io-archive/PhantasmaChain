using System.IO;
using Phantasma.Core;
using Phantasma.Network.Messages;
using Phantasma.Utils;

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

        public bool IsSigned => PublicKey != null && Signature != null;

        public Message(Opcode opcode, byte[] publicKey) {
            this.Opcode = opcode;
            this.PublicKey = publicKey;
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
                        msg = PeerJoinMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.PEER_Leave:
                    {
                        msg = PeerLeaveMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.PEER_List:
                    {
                        msg = PeerListMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Request:
                    {
                        msg = RaftRequestMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Vote:
                    {
                        msg = RaftVoteMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Lead:
                    {
                        msg = RaftLeadMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Replicate:
                    {
                        msg = RaftReplicateMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Confirm:
                    {
                        msg = RaftConfirmMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Commit:
                    {
                        msg = RaftCommitMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.RAFT_Beat:
                    {
                        msg = RaftBeatMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        msg = MempoolAddMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        msg = MempoolGetMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.BLOCKS_Request:
                    {
                        msg = ChainRequestMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        msg = ChainRequestMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.CHAIN_Request:
                    {
                        msg = BlockRequestMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.CHAIN_Values:
                    {
                        msg = ChainValuesMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.SHARD_Submit:
                    {
                        msg = ShardSubmitMessage.FromReader(pubKey, reader);
                        break;
                    }

                case Opcode.ERROR:
                    {
                        msg = ErrorMessage.FromReader(pubKey, reader);
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
