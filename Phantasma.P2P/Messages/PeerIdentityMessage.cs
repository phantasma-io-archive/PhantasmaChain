using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    public class PeerIdentityMessage : Message
    {
        public PeerIdentityMessage(Address address) : base(Opcode.PEER_Identity, address)
        {
        }

        internal static PeerIdentityMessage FromReader(Address address, BinaryReader reader)
        {
            return new PeerIdentityMessage(address);
        }

        protected override void OnSerialize(BinaryWriter writer)
        {
            // do nothing
        }

        public override string ToString()
        {
            return "";
        }
    }
}