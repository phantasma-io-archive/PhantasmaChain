using System.IO;

namespace Phantasma.IO
{
    public static class StreamExtensions
    {
        public static uint ReadUInt24(this BinaryReader reader)
        {
            var b1 = reader.ReadByte();
            var b2 = reader.ReadByte();
            var b3 = reader.ReadByte();
            return
                (((uint)b1) << 16) |
                (((uint)b2) << 8) |
                ((uint)b3);
        }

        public static void WriteUInt24(this BinaryWriter writer, uint val)
        {
            writer.Write((byte)((val >> 16) & 0xFF));
            writer.Write((byte)((val >> 8) & 0xFF));
            writer.Write((byte)(val & 0xFF));
        }
    }
}
