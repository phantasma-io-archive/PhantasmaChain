using System.IO;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftCommitMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftCommitMessage(Address address, byte[] hash) : base(Opcode.RAFT_Commit, address)
        {
            this.BlockHash = hash;
        }

        internal static RaftCommitMessage FromReader(Address address, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftCommitMessage(address, hash);
        }
    }
}