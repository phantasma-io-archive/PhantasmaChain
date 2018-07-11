using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class RaftConfirmMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftConfirmMessage(byte[] pubKey, byte[] hash) : base(Opcode.RAFT_Confirm, pubKey)
        {
            Throw.IfNull(hash, nameof(hash));
            this.BlockHash = hash;
        }

        internal static RaftConfirmMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftConfirmMessage(pubKey, hash);
        }
    }
}