using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Phantasma.Numerics;

namespace Phantasma.Blockchain
{
    [Flags]
    public enum ArchiveFlags
    {
        None = 0x0,
        Compressed = 0x1,
        Encrypted = 0x2,
    }

    // TODO support this
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
        public static readonly int MaxSize = 104857600; //100mb
        public static readonly int BlockSize = 256 * 1024;

        public Hash Hash => MerkleTree.Root;

        public MerkleTree MerkleTree { get; private set; }
        public BigInteger Size { get; private set; }
        public ArchiveFlags Flags { get; private set; }
        public byte[] Key { get; private set; }

        public BigInteger BlockCount => Size / BlockSize;

        public IEnumerable<Hash> Blocks
        {
            get
            {
                for (int i=0; i<BlockCount; i++)
                {
                    yield return MerkleTree.GetHash(i);
                }

                yield break;
            }
        }

        public Archive(MerkleTree tree, BigInteger size, ArchiveFlags flags, byte[] key)
        {
            this.MerkleTree = tree;
            this.Size = size;
            this.Flags = flags;
            this.Key = key;
        }

        private Archive()
        {

        }

        public void SerializeData(BinaryWriter writer)
        {
            MerkleTree.SerializeData(writer);
            writer.Write((long)Size);
            writer.Write((byte)Flags);
            writer.WriteByteArray(Key);
        }

        public void UnserializeData(BinaryReader reader)
        {
            MerkleTree = MerkleTree.Unserialize(reader);
            Size = reader.ReadInt32();
            Flags = (ArchiveFlags) reader.ReadByte();

            Key = reader.ReadByteArray();
            Key = Key ?? new byte[0];
        }

        public static Archive Unserialize(BinaryReader reader)
        {
            var archive = new Archive();
            archive.UnserializeData(reader);
            return archive;
        }
    }
}
