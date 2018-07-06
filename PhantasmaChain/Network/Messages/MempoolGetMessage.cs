using System;
using System.IO;

namespace Phantasma.Network
{
    internal class MempoolGetMessage : Message
    {
        public MempoolGetMessage()
        {
        }

        internal static MempoolGetMessage FromReader(BinaryReader reader)
        {
            return new MempoolGetMessage();
        }
    }
}