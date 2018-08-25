using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.Messages
{
    internal class RaftRequestMessage : Message
    {
        // random value. voters should sign a message with this number
        public readonly uint nonce;

        public RaftRequestMessage(Address address, uint nonce) :base (Opcode.RAFT_Request, address)
        {
            this.nonce = nonce;
        }

        internal static RaftRequestMessage FromReader(Address address, BinaryReader reader)
        {
            var nonce = reader.ReadUInt32();
            return new RaftRequestMessage(address, nonce);
        }
    }
}