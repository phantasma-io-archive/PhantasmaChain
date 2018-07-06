using System.IO;

namespace Phantasma.Network
{
    internal class MempoolAddMessage : Message
    {
        private BinaryReader reader;

        public MempoolAddMessage(BinaryReader reader)
        {
            this.reader = reader;
        }
    }
}