using System.IO;

namespace Phantasma.Network
{
    internal class ChainRequestMessage : Message
    {
        public readonly uint Flags;

        public ChainRequestMessage(uint flags = 0)
        {
            this.Flags = 0;
        }

        internal static ChainRequestMessage FromReader(BinaryReader reader)
        {
            var flags = reader.ReadUInt32();
            return new ChainRequestMessage(flags);
        }
    }
}