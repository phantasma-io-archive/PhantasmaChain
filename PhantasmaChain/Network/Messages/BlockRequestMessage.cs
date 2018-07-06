using System.IO;
using System.Numerics;
using Phantasma.Utils;

namespace Phantasma.Network
{
    internal class ChainGetMessage : Message
    {
        // block range
        private readonly BigInteger Min;
        private readonly BigInteger Max;

        public ChainGetMessage(BigInteger min, BigInteger max)
        {
            this.Min = min;
            this.Max = max;
        }

        internal static Message FromReader(BinaryReader reader)
        {
            var min = reader.ReadBigInteger();
            var max = reader.ReadBigInteger();
            return new ChainGetMessage(min, max);
        }

    }
}