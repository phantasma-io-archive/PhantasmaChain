using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class RaftConfirmMessage : Message
    {
        public readonly byte[] BlockHash;

        public RaftConfirmMessage(byte[] hash)
        {
            Throw.IfNull(hash, nameof(hash));
            this.BlockHash = hash;
        }

        internal static RaftConfirmMessage FromReader(BinaryReader reader)
        {
            var hash = reader.ReadByteArray();

            return new RaftConfirmMessage(hash);
        }
    }
}