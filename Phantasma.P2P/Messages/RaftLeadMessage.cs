using Phantasma.Blockchain;
using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftLeadMessage : Message
    {
        private PeerVote[] votes;
        public IEnumerable<PeerVote> Votes => votes;

        public RaftLeadMessage(Nexus nexus, Address address, IEnumerable<PeerVote> votes) : base(nexus, Opcode.RAFT_Lead, address)
        {
            this.votes = votes.ToArray();
        }

        internal static RaftLeadMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var sigCount = reader.ReadUInt32();
            var sigs = new PeerVote[sigCount];
            for (int i = 0; i < sigCount; i++) {
                sigs[i] = PeerVote.Unserialize(reader);
            }
            return new RaftLeadMessage(nexus, address, sigs);
        }
    }
}