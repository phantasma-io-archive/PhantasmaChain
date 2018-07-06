using System.IO;

namespace Phantasma.Network
{
    internal class PeerListMessage : Message
    {
        private BinaryReader reader;

        public PeerListMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}