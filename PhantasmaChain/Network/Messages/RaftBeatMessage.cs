using Phantasma.VM.Types;
using System.IO;

namespace Phantasma.Network.Messages
{
    internal class RaftBeatMessage : Message
    {
        private readonly uint Term;

        public RaftBeatMessage(Address address, uint term) :base(Opcode.RAFT_Beat, address)
        {
            this.Term = term;
        }

        internal static RaftBeatMessage FromReader(Address address, BinaryReader reader)
        {
            var term = reader.ReadUInt32();
            return new RaftBeatMessage(address, term);
        }
    }
}