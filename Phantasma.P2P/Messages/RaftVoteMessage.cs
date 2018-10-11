using System.IO;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.IO;
using Phantasma.Blockchain;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftVoteMessage : Message
    {
        public readonly Address Vote;
        public readonly uint Nonce;

        public RaftVoteMessage(Nexus nexus, Address address, Address vote, uint nonce) : base(nexus, Opcode.RAFT_Vote, address)
        {
            Throw.If(vote == Address.Null, nameof(vote));

            this.Vote = vote;
            this.Nonce = nonce;
        }

        internal static RaftVoteMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var vote = reader.ReadAddress();
            var nonce = reader.ReadUInt32();
            return new RaftVoteMessage(nexus, address, vote, nonce);
        }
    }
}