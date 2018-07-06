using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phantasma.Network
{
    internal class RaftLeadMessage : Message
    {
        private PeerVote[] votes;
        public IEnumerable<PeerVote> Votes => votes;

        public RaftLeadMessage(IEnumerable<PeerVote> votes)
        {
            this.votes = votes.ToArray();
        }

        internal static RaftLeadMessage FromReader(BinaryReader reader)
        {
            var sigCount = reader.ReadUInt32();
            var sigs = new PeerVote[sigCount];
            for (int i = 0; i < sigCount; i++) {
                sigs[i] = PeerVote.Unserialize(reader);
            }
            return new RaftLeadMessage(sigs);
        }
    }
}