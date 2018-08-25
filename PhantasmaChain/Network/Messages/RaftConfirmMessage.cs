using System.IO;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Cryptography;

namespace Phantasma.Network.Messages
{
    internal class RaftConfirmMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftConfirmMessage(Address address, byte[] hash) : base(Opcode.RAFT_Confirm, address)
        {
            Throw.IfNull(hash, nameof(hash));
            this.BlockHash = hash;
        }

        internal static RaftConfirmMessage FromReader(Address address, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftConfirmMessage(address, hash);
        }
    }
}