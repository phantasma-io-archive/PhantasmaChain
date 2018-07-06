using System.IO;

namespace Phantasma.Network
{
    internal class RaftReplicateMessage : Message
    {
        private BinaryReader reader;

        public RaftReplicateMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}