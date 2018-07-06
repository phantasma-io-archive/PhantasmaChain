using System.IO;

namespace Phantasma.Network
{
    internal class RaftCommitMessage : Message
    {
        private BinaryReader reader;

        public RaftCommitMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}