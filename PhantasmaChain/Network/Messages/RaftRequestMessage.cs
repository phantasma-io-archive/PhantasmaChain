using System.IO;

namespace Phantasma.Network.Messages
{
    internal class RaftRequestMessage : Message
    {
        // random value. voters should sign a message with this number
        public readonly uint nonce;

        public RaftRequestMessage(byte[] pubKey, uint nonce) :base (Opcode.RAFT_Request, pubKey)
        {
            this.nonce = nonce;
        }

        internal static RaftRequestMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var nonce = reader.ReadUInt32();
            return new RaftRequestMessage(pubKey, nonce);
        }
    }
}