using System.IO;
using Phantasma.Cryptography;
using Phantasma.Network.Messages;
using Phantasma.Utils;
using Phantasma.VM.Types;

namespace Phantasma.Network
{
    /// <summary>
    /// Represents a generic network message
    /// </summary>
    public abstract class Message
    {
        public Opcode Opcode { get; private set; }
        public Address Address { get; private set; }
        public byte[] Signature { get; private set; }

        public bool IsSigned => Address != Address.Null && Signature != null;

        public Message(Opcode opcode, Address address) {
            this.Opcode = opcode;
            this.Address = address;
        }

        public static Message Unserialize(BinaryReader reader)
        {
            var opcode = (Opcode)reader.ReadByte();
            var address = reader.ReadAddress();

            Message msg;

            switch (opcode)
            {
                case Opcode.PEER_Join:
                    {
                        msg = PeerJoinMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.PEER_Leave:
                    {
                        msg = PeerLeaveMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.PEER_List:
                    {
                        msg = PeerListMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Request:
                    {
                        msg = RaftRequestMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Vote:
                    {
                        msg = RaftVoteMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Lead:
                    {
                        msg = RaftLeadMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Replicate:
                    {
                        msg = RaftReplicateMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Confirm:
                    {
                        msg = RaftConfirmMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Commit:
                    {
                        msg = RaftCommitMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.RAFT_Beat:
                    {
                        msg = RaftBeatMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        msg = MempoolAddMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        msg = MempoolGetMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.BLOCKS_Request:
                    {
                        msg = ChainRequestMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        msg = ChainRequestMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.CHAIN_Request:
                    {
                        msg = BlockRequestMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.CHAIN_Values:
                    {
                        msg = ChainValuesMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.SHARD_Submit:
                    {
                        msg = ShardSubmitMessage.FromReader(address, reader);
                        break;
                    }

                case Opcode.ERROR:
                    {
                        msg = ErrorMessage.FromReader(address, reader);
                        break;
                    }

                default: return null;
            }

            if (address != null)
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
