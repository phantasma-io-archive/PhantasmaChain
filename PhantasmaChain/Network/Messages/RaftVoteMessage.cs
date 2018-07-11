using Phantasma.Core;
using Phantasma.Utils;
using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftVoteMessage : Message
    {
        public readonly byte[] VoteKey;
        public readonly uint Nonce;

        public RaftVoteMessage(byte[] pubKey, byte[] votePublicKey, uint nonce) : base(Opcode.RAFT_Vote, pubKey)
        {
            Throw.IfNull(votePublicKey, nameof(votePublicKey));

            this.VoteKey = votePublicKey;
            this.Nonce = nonce;
        }

        internal static RaftVoteMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var votePubKey = reader.ReadBytes(KeyPair.PublicKeyLength);
            var nonce = reader.ReadUInt32();
            return new RaftVoteMessage(pubKey, votePubKey, nonce);
        }
    }
}