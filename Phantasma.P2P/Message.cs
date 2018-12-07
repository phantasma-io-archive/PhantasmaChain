using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Network.P2P.Messages;

namespace Phantasma.Network.P2P
{
    /// <summary>
    /// Represents a generic network message
    /// </summary>
    public abstract class Message
    {
        public Opcode Opcode { get; private set; }
        public Address Address { get; private set; }
        public byte[] Signature { get; private set; }

        public Nexus Nexus { get; private set; }

        public bool IsSigned => Address != Address.Null && Signature != null;

        public Message(Nexus nexus, Opcode opcode, Address address) {
            this.Nexus = nexus;
            this.Opcode = opcode;
            this.Address = address;
        }

        public static Message Unserialize(Nexus nexus, BinaryReader reader)
        {
            var opcode = (Opcode)reader.ReadByte();
            var address = reader.ReadAddress();

            Message msg;

            switch (opcode)
            {
                case Opcode.PEER_Join:
                    {
                        msg = PeerJoinMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.PEER_Leave:
                    {
                        msg = PeerLeaveMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.PEER_List:
                    {
                        msg = PeerListMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Add:
                    {
                        msg = MempoolAddMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.MEMPOOL_Get:
                    {
                        msg = MempoolGetMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.BLOCKS_Request:
                    {
                        msg = ChainRequestMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.BLOCKS_List:
                    {
                        msg = ChainRequestMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.CHAIN_Request:
                    {
                        msg = BlockRequestMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.CHAIN_List:
                    {
                        msg = ChainListMessage.FromReader(nexus, address, reader);
                        break;
                    }

                case Opcode.ERROR:
                    {
                        msg = ErrorMessage.FromReader(nexus, address, reader);
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
