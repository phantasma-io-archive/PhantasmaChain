using System.IO;

namespace Phantasma.Network
{
    internal class PeerJoinMessage : Message
    {
        private BinaryReader reader;

        public PeerJoinMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}