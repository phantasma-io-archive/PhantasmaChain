using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class PeerLeaveMessage : Message
    {
        public PeerLeaveMessage(Nexus nexus, Address address) : base(nexus, Opcode.PEER_Leave, address)
        {
        }

        internal static PeerLeaveMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            return new PeerLeaveMessage(nexus, address);
        }
    }
}