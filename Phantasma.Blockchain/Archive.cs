using System.Collections.Generic;
using System.IO;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Storage.Utils;
using Phantasma.Numerics;
using Phantasma.Domain;
using Phantasma.Core.Types;

namespace Phantasma.Blockchain
{
    public class Archive: IArchive, ISerializable
    {
        public static readonly uint BlockSize = MerkleTree.ChunkSize;

        public Hash Hash => MerkleTree.Root;

        public MerkleTree MerkleTree { get; private set; }
        public BigInteger Size { get; private set; }
        public Timestamp Time { get; private set; }

        public ArchiveFlags Flags { get; private set; }
        public byte[] Key { get; private set; }

        public BigInteger BlockCount => this.GetBlockCount();

        public string Name { get; private set; }

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

        public Archive(MerkleTree tree, string name, BigInteger size, Timestamp time, ArchiveFlags flags, byte[] key)
        {
            this.MerkleTree = tree;
            this.Name = name;
            this.Size = size;
            this.Time = time;
            this.Flags = flags;
            this.Key = key;
        }

        public Archive()
        {

        }

        public void SerializeData(BinaryWriter writer)
        {
            MerkleTree.SerializeData(writer);
            writer.WriteVarString(Name);
            writer.WriteBigInteger(Size);
            writer.Write(Time.Value);
            writer.Write((byte)Flags);
            writer.WriteByteArray(Key);
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    SerializeData(writer);
                }
                return stream.ToArray();
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            MerkleTree = MerkleTree.Unserialize(reader);
            Name = reader.ReadVarString();
            Size = reader.ReadBigInteger();
            Time = new Timestamp(reader.ReadUInt32());
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

        public static Archive Unserialize(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return Unserialize(reader);
                }
            }
        }

    }
}
