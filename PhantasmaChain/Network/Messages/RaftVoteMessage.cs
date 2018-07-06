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

        public RaftVoteMessage(byte[] votePublicKey, uint nonce)
        {
            Throw.IfNull(votePublicKey, nameof(votePublicKey));

            this.VoteKey = votePublicKey;
            this.Nonce = nonce;
        }

        internal static RaftVoteMessage FromReader(BinaryReader reader)
        {
            var pubKey = reader.ReadBytes(KeyPair.PublicKeyLength);
            var nonce = reader.ReadUInt32();
            return new RaftVoteMessage(pubKey, nonce);
        }
    }
}