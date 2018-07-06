using System.IO;

namespace Phantasma.Network
{
    internal class RaftConfirmMessage : Message
    {
        private BinaryReader reader;

        public RaftConfirmMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}