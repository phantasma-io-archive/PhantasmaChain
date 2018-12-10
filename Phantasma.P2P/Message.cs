using System.IO;
using Phantasma.Blockchain;
using Phantasma.Core;
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
        public Signature Signature { get; private set; }

        public Nexus Nexus { get; private set; }

        public bool IsSigned => Address != Address.Null && Signature != null;

        public Message(Nexus nexus, Opcode opcode, Address address) {
            this.Nexus = nexus;
            this.Opcode = opcode;
            this.Address = address;
        }

        public void Sign(KeyPair keyPair)
        {
            Throw.If(keyPair.Address != this.Address, "unexpected keypair");

            var msg = this.ToByteArray(false);

            this.Signature = keyPair.Sign(msg);
        }

        public static Message Unserialize(Nexus nexus, BinaryReader reader)
        {
            var opcode = (Opcode)reader.ReadByte();
            var address = reader.ReadAddress();

            Message msg;

            switch (opcode)
            {
                case Opcode.PEER_Identity:
                    {
                        msg = PeerIdentityMessage.FromReader(nexus, address, reader);
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

                case Opcode.MEMPOOL_List:
                    {
                        msg = MempoolGetMessage.FromReader(nexus, address, reader);
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
                msg.Signature = reader.ReadSignature();
            }

            return msg;
        }

        public byte[] ToByteArray(bool withSignature)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialize(writer, withSignature);
                }

                return stream.ToArray();
            }
        }

        public void Serialize(BinaryWriter writer, bool withSignature)
        {
            writer.Write((byte)Opcode);
            writer.WriteAddress(Address);

            OnSerialize(writer);

            if (withSignature)
            {
                Throw.IfNull(Signature, nameof(Signature));

                writer.WriteSignature(Signature);
            }
        }

        protected abstract void OnSerialize(BinaryWriter writer);
    }
}
