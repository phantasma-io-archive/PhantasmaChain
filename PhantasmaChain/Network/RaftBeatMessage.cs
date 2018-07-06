using System.IO;

namespace Phantasma.Network
{
    internal class RaftBeatMessage : Message
    {
        private BinaryReader reader;

        public RaftBeatMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}