using System.IO;
using Phantasma.Blockchain;
using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.IO;

namespace Phantasma.Network.P2P.Messages
{
    internal class RaftConfirmMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftConfirmMessage(Nexus nexus, Address address, byte[] hash) : base(nexus, Opcode.RAFT_Confirm, address)
        {
            Throw.IfNull(hash, nameof(hash));
            this.BlockHash = hash;
        }

        internal static RaftConfirmMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftConfirmMessage(nexus, address, hash);
        }
    }
}