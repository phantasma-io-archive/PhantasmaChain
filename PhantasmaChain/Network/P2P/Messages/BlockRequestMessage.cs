using System.IO;
using Phantasma.Mathematics;
using Phantasma.Cryptography;
using Phantasma.Core.Utils;

namespace Phantasma.Network.P2P.Messages
{
    internal class BlockRequestMessage : Message
    {
        // block range
        private readonly BigInteger Min;
        private readonly BigInteger Max;

        public BlockRequestMessage(Address address, BigInteger min, BigInteger max) :base(Opcode.BLOCKS_Request, address)
        {
            this.Min = min;
            this.Max = max;
        }

        internal static BlockRequestMessage FromReader(Address address, BinaryReader reader)
        {
            var min = reader.ReadBigInteger();
            var max = reader.ReadBigInteger();
            return new BlockRequestMessage(address, min, max);
        }

    }
}