using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System;
using System.IO;

namespace Phantasma.Blockchain
{
    [Flags]
    public enum ArchiveFlags
    {
        None = 0x0,
        Compressed = 0x1,
        Encrypted = 0x2,
    }

    public class Archive: ISerializable
    {
        public MerkleTree Hashes { get; private set; }
        public int Size { get; private set; }
        public int References { get; private set; }
        public ArchiveFlags Flags { get; private set; }
        public byte[] Key { get; private set; }

        public void SerializeData(BinaryWriter writer)
        {
            Hashes.SerializeData(writer);
            writer.Write(Size);
            writer.Write((byte)Flags);
            writer.WriteByteArray(Key);
            writer.WriteVarInt(References);
        }

        public void UnserializeData(BinaryReader reader)
        {
            Hashes = MerkleTree.Unserialize(reader);
            Size = reader.ReadInt32();
            Flags = (ArchiveFlags) reader.ReadByte();

            Key = reader.ReadByteArray();
            Key = Key ?? new byte[0];

            References = (int)reader.ReadVarInt();
        }
    }
}
