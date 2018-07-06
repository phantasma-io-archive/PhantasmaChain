using System.IO;

namespace Phantasma.Network
{
    internal class ChainStatsMessage : Message
    {
        public readonly uint Flags;

        public ChainStatsMessage(uint flags = 0)
        {
            this.Flags = 0;
        }

        internal static Message FromReader(BinaryReader reader)
        {
            var flags = reader.ReadUInt32();
            return new ChainStatsMessage(flags);
        }
    }
}