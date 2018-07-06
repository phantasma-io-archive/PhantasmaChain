using System.IO;

namespace Phantasma.Network
{
    internal class RaftRequestMessage : Message
    {
        private BinaryReader reader;

        public RaftRequestMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}