using System.IO;

namespace Phantasma.Network
{
    internal class RaftLeadMessage : Message
    {
        private BinaryReader reader;

        public RaftLeadMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}