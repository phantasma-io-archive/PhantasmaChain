using System.IO;
using Phantasma.Blockchain;
using Phantasma.Core;
using Phantasma.Cryptography;
    
namespace Phantasma.Network.Messages
{
    internal class RaftReplicateMessage : Message
    {
        public readonly Block block;

        public RaftReplicateMessage(Address address, Block block) : base(Opcode.RAFT_Replicate, address)
        {
            Throw.IfNull(block, nameof(block));
            this.block = block;
        }

        internal static RaftReplicateMessage FromReader(Address address, BinaryReader reader)
        {
            var block = Block.Unserialize(reader);
            return new RaftReplicateMessage(address, block);
        }
    }
}