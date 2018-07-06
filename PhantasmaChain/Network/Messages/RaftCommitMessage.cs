using Phantasma.Utils;
using System;
using System.IO;

namespace Phantasma.Network
{
    internal class RaftCommitMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftCommitMessage(byte[] hash)
        {
            this.BlockHash = hash;
        }

        internal static RaftCommitMessage FromReader(BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftCommitMessage(hash);
        }
    }
}