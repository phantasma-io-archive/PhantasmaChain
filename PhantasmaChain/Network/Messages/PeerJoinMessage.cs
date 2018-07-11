using System;
using System.IO;

namespace Phantasma.Network
{
    internal class PeerJoinMessage : Message
    {
        public PeerJoinMessage(byte[] pubKey) : base(Opcode.PEER_Join, pubKey)
        {
        }

        internal static PeerJoinMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            return new PeerJoinMessage(pubKey);
        }
    }
}