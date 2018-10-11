using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftRequestMessage : Message
    {
        // random value. voters should sign a message with this number
        public readonly uint nonce;

        public RaftRequestMessage(Nexus nexus, Address address, uint nonce) : base (nexus, Opcode.RAFT_Request, address)
        {
            this.nonce = nonce;
        }

        internal static RaftRequestMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var nonce = reader.ReadUInt32();
            return new RaftRequestMessage(nexus, address, nonce);
        }
    }
}