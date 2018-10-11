using System.IO;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftCommitMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftCommitMessage(Nexus nexus, Address address, byte[] hash) : base(nexus, Opcode.RAFT_Commit, address)
        {
            this.BlockHash = hash;
        }

        internal static RaftCommitMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftCommitMessage(nexus, address, hash);
        }
    }
}