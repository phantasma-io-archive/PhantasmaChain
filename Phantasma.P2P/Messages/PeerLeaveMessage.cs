using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class PeerLeaveMessage : Message
    {
        public PeerLeaveMessage(Address address) : base( Opcode.PEER_Leave, address)
        {
        }

        internal static PeerLeaveMessage FromReader(Address address, BinaryReader reader)
        {
            return new PeerLeaveMessage(address);
        }
    }
}