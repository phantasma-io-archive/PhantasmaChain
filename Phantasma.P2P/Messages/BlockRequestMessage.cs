using System.IO;
using Phantasma.Numerics;
using Phantasma.Cryptography;
using Phantasma.IO;
using Phantasma.Blockchain;

namespace Phantasma.Network.P2P.Messages
{
    internal class BlockRequestMessage : Message
    {
        // block range
        private readonly BigInteger Min;
        private readonly BigInteger Max;

        public BlockRequestMessage(Nexus nexus, Address address, BigInteger min, BigInteger max) :base(nexus, Opcode.BLOCKS_Request, address)
        {
            this.Min = min;
            this.Max = max;
        }

        internal static BlockRequestMessage FromReader(Nexus nexus, Address address, BinaryReader reader)
        {
            var min = reader.ReadBigInteger();
            var max = reader.ReadBigInteger();
            return new BlockRequestMessage(nexus, address, min, max);
        }

    }
}