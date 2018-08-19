using Phantasma.VM.Types;
using System.IO;

namespace Phantasma.Network.Messages
{
    internal class PeerJoinMessage : Message
    {
        public PeerJoinMessage(Address address) : base(Opcode.PEER_Join, address)
        {
        }

        internal static PeerJoinMessage FromReader(Address address, BinaryReader reader)
        {
            return new PeerJoinMessage(address);
        }
    }
}