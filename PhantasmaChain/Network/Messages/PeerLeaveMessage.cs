using System;
using System.IO;

namespace Phantasma.Network
{
    internal class PeerLeaveMessage : Message
    {
        public PeerLeaveMessage()
        {
        }

        internal static PeerLeaveMessage FromReader(BinaryReader reader)
        {
            return new PeerLeaveMessage();
        }
    }
}