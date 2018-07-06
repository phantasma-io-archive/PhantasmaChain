using Phantasma.Core;
using System;
using System.IO;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class RaftReplicateMessage : Message
    {
        public readonly Block block;

        public RaftReplicateMessage(Block block)
        {
            Throw.IfNull(block, nameof(block));
            this.block = block;
        }

        internal static RaftReplicateMessage FromReader(BinaryReader reader)
        {
            var block = Block.Unserialize(reader);
            return new RaftReplicateMessage(block);
        }
    }
}