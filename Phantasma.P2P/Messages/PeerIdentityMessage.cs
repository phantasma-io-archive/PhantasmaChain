using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    public class PeerIdentityMessage : Message
    {
        public PeerIdentityMessage(Nexus nexus, Address address) : base(nexus, Opcode.PEER_Identity, address)
        {
        }

        internal static PeerIdentityMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            return new PeerIdentityMessage(nexus, address);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            // do nothing
        }
    }
}