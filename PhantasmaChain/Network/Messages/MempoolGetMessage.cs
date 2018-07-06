using System.IO;

namespace Phantasma.Network
{
    internal class MempoolGetMessage : Message
    {
        private BinaryReader reader;

        public MempoolGetMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}