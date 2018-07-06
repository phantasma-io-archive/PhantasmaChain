using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftBeatMessage : Message
    {
        private readonly uint Term;

        public RaftBeatMessage(uint term)
        {
            this.Term = term;
        }

        internal static RaftBeatMessage FromReader(BinaryReader reader)
        {
            var term = reader.ReadUInt32();
            return new RaftBeatMessage(term);
        }
    }
}