using System.IO;

namespace Phantasma.Network.Messages
{
    internal class PeerLeaveMessage : Message
    {
        public PeerLeaveMessage(byte[] pubKey) : base( Opcode.PEER_Leave, pubKey)
        {
        }

        internal static PeerLeaveMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            return new PeerLeaveMessage(pubKey);
        }
    }
}