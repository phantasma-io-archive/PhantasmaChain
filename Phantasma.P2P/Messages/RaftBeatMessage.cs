using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftBeatMessage : Message
    {
        private readonly uint Term;

        public RaftBeatMessage(Nexus nexus, Address address, uint term) :base(nexus, Opcode.RAFT_Beat, address)
        {
            this.Term = term;
        }

        internal static RaftBeatMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var term = reader.ReadUInt32();
            return new RaftBeatMessage(nexus, address, term);
        }
    }
}