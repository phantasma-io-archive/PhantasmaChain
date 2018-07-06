using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftRequestMessage : Message
    {
        // random value. voters should sign a message with this number
        public readonly uint nonce;

        public RaftRequestMessage(uint nonce)
        {
            this.nonce = nonce;
        }

        internal static RaftRequestMessage FromReader(BinaryReader reader)
        {
            var nonce = reader.ReadUInt32();
            return new RaftRequestMessage(nonce);
        }
    }
}