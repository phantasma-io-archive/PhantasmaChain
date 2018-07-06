using System.IO;
using System.Numerics;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class BlockRequestMessage : Message
    {
        // block range
        private readonly BigInteger Min;
        private readonly BigInteger Max;

        public BlockRequestMessage(BigInteger min, BigInteger max)
        {
            this.Min = min;
            this.Max = max;
        }

        internal static BlockRequestMessage FromReader(BinaryReader reader)
        {
            var min = reader.ReadBigInteger();
            var max = reader.ReadBigInteger();
            return new BlockRequestMessage(min, max);
        }

    }
}