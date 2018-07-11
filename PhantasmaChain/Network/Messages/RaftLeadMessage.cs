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

        public RaftLeadMessage(byte[] pubKey, IEnumerable<PeerVote> votes) : base(Opcode.RAFT_Lead, pubKey)
        {
            this.votes = votes.ToArray();
        }

        internal static RaftLeadMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var sigCount = reader.ReadUInt32();
            var sigs = new PeerVote[sigCount];
            for (int i = 0; i < sigCount; i++) {
                sigs[i] = PeerVote.Unserialize(reader);
            }
            return new RaftLeadMessage(pubKey, sigs);
        }
    }
}