using Phantasma.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network.Messages
{
    internal class RaftLeadMessage : Message
    {
        private PeerVote[] votes;
        public IEnumerable<PeerVote> Votes => votes;

        public RaftLeadMessage(Address address, IEnumerable<PeerVote> votes) : base(Opcode.RAFT_Lead, address)
        {
            this.votes = votes.ToArray();
        }

        internal static RaftLeadMessage FromReader(Address address, BinaryReader reader)
        {
            var sigCount = reader.ReadUInt32();
            var sigs = new PeerVote[sigCount];
            for (int i = 0; i < sigCount; i++) {
                sigs[i] = PeerVote.Unserialize(reader);
            }
            return new RaftLeadMessage(address, sigs);
        }
    }
}