using System.Collections.Generic;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using Phantasma.Numerics;
using Phantasma.Domain;

namespace Phantasma.Blockchain
{
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

    public class Archive: IArchive, ISerializable
    {
        public static readonly uint BlockSize = MerkleTree.ChunkSize;

        public Hash Hash => MerkleTree.Root;

        public MerkleTree MerkleTree { get; private set; }
        public BigInteger Size { get; private set; }
        public ArchiveFlags Flags { get; private set; }
        public byte[] Key { get; private set; }

        public BigInteger BlockCount => this.GetBlockCount();

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

        public Archive()
        {

        }

        public void SerializeData(BinaryWriter writer)
        {
            MerkleTree.SerializeData(writer);
            writer.WriteBigInteger(Size);
            writer.Write((byte)Flags);
            writer.WriteByteArray(Key);
        }

        public void UnserializeData(BinaryReader reader)
        {
            MerkleTree = MerkleTree.Unserialize(reader);
            Size = reader.ReadBigInteger();
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
