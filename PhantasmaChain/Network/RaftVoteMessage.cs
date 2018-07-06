using System.IO;

namespace Phantasma.Network
{
    internal class RaftVoteMessage : Message
    {
        private BinaryReader reader;

        public RaftVoteMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}