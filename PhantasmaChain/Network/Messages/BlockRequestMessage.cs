using System.IO;
using System.Numerics;
using Phantasma.Utils;

namespace Phantasma.Network.Messages
{
    internal class BlockRequestMessage : Message
    {
        // block range
        private readonly BigInteger Min;
        private readonly BigInteger Max;

        public BlockRequestMessage(byte[] pubKey, BigInteger min, BigInteger max) :base(Opcode.BLOCKS_Request, pubKey)
        {
            this.Min = min;
            this.Max = max;
        }

        internal static BlockRequestMessage FromReader(byte[] pubKey, BinaryReader reader)
        {
            var min = reader.ReadBigInteger();
            var max = reader.ReadBigInteger();
            return new BlockRequestMessage(pubKey, min, max);
        }

    }
}