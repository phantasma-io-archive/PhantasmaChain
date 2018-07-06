using System.IO;

namespace Phantasma.Network
{
    internal class PeerLeaveMessage : Message
    {
        private BinaryReader reader;

        public PeerLeaveMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}