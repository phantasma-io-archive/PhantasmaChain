using Phantasma.Utils;
using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftCommitMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftCommitMessage(byte[] pubKey, byte[] hash) : base(Opcode.RAFT_Commit, pubKey)
        {
            this.BlockHash = hash;
        }

        internal static RaftCommitMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftCommitMessage(pubKey, hash);
        }
    }
}