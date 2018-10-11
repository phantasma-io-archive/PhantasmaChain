using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class PeerJoinMessage : Message
    {
        public PeerJoinMessage(Nexus nexus, Address address) : base(nexus, Opcode.PEER_Join, address)
        {
        }

        internal static PeerJoinMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            return new PeerJoinMessage(nexus, address);
        }
    }
}