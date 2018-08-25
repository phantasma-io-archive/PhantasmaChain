using System.IO;
using Phantasma.Cryptography;
using Phantasma.Core;
using Phantasma.Core.Utils;

namespace Phantasma.Network.Messages
{
    internal class RaftVoteMessage : Message
    {
        public readonly Address Vote;
        public readonly uint Nonce;

        public RaftVoteMessage(Address address, Address vote, uint nonce) : base(Opcode.RAFT_Vote, address)
        {
            Throw.If(vote == Address.Null, nameof(vote));

            this.Vote = vote;
            this.Nonce = nonce;
        }

        internal static RaftVoteMessage FromReader(Address address, BinaryReader reader)
        {
            var vote = reader.ReadAddress();
            var nonce = reader.ReadUInt32();
            return new RaftVoteMessage(address, vote, nonce);
        }
    }
}