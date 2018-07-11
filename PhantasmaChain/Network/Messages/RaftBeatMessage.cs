using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftBeatMessage : Message
    {
        private readonly uint Term;

        public RaftBeatMessage(byte[] pubKey, uint term) :base(Opcode.RAFT_Beat, pubKey)
        {
            this.Term = term;
        }

        internal static RaftBeatMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var term = reader.ReadUInt32();
            return new RaftBeatMessage(pubKey, term);
        }
    }
}