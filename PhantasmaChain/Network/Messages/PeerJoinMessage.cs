using System;
using System.IO;

namespace Phantasma.Network
{
    internal class PeerJoinMessage : Message
    {
        public PeerJoinMessage()
        {
        }

        internal static PeerJoinMessage FromReader(BinaryReader reader)
        {
            return new PeerJoinMessage();
        }
    }
}