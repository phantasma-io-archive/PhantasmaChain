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

    public struct ArchiveMetadata
    {
        public readonly string Key;
        public readonly string Value;

        public ArchiveMetadata(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public class Archive: ISerializable
    {
        public static readonly int MinSize = 1024; //1kb
        public static readonly int MaxSize = 10485760; //100mb

        public MerkleTree Hashes { get; private set; }
        public int Size { get; private set; }
        public int References { get; private set; }
        public ArchiveFlags Flags { get; private set; }
        public byte[] Key { get; private set; }
        public ArchiveMetadata[] Metadata { get; private set; }

        public void SerializeData(BinaryWriter writer)
        {
            Hashes.SerializeData(writer);
            writer.Write(Size);
            writer.Write((byte)Flags);
            writer.WriteByteArray(Key);
            writer.WriteVarInt(References);
            writer.WriteVarInt(Metadata.Length);
            for (int i = 0; i < Metadata.Length; i++)
            {
                writer.WriteVarString(Metadata[i].Key);
                writer.WriteVarString(Metadata[i].Value);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            Hashes = MerkleTree.Unserialize(reader);
            Size = reader.ReadInt32();
            Flags = (ArchiveFlags) reader.ReadByte();

            Key = reader.ReadByteArray();
            Key = Key ?? new byte[0];

            References = (int)reader.ReadVarInt();

            int metaCount = (int)reader.ReadVarInt();
            Metadata = new ArchiveMetadata[metaCount];
            for (int i = 0; i < Metadata.Length; i++)
            {
                var key = reader.ReadVarString();
                var val = reader.ReadVarString();
                Metadata[i] = new ArchiveMetadata(key, val);
            }
        }
    }
}
