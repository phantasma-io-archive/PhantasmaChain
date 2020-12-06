using System.IO;
using System.Numerics;
using Phantasma.Core;
using Phantasma.Domain;
using Phantasma.Storage;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Storage.Utils;
using System.Collections.Generic;

namespace Phantasma.Blockchain.Storage
{
    public class Archive: IArchive, ISerializable
    {
        public static readonly uint BlockSize = MerkleTree.ChunkSize;

        public string Name { get; private set; }

        public Hash Hash => MerkleTree.Root;

        public MerkleTree MerkleTree { get; private set; }
        public BigInteger Size { get; private set; }
        public Timestamp Time { get; private set; }

        public IArchiveEncryption Encryption { get; private set; }

        public BigInteger BlockCount => this.GetBlockCount();


        private List<Address> _owners = new List<Address>();
        public IEnumerable<Address> Owners => _owners;
        public int OwnerCount => _owners.Count;

        private List<int> _missingBlocks = new List<int>();
        public IEnumerable<int> MissingBlockIndices => _missingBlocks;
        public int MissingBlockCount => _missingBlocks.Count;

        public IEnumerable<Hash> BlockHashes
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

        public Archive(MerkleTree tree, string name, BigInteger size, Timestamp time, IArchiveEncryption encryption, List<int> missingBlocks)
        {
            this.MerkleTree = tree;
            this.Name = name;
            this.Size = size;
            this.Time = time;
            this.Encryption = encryption;
            this._missingBlocks = missingBlocks;
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
            writer.WriteArchiveEncryption(Encryption);
            writer.WriteVarInt(_owners.Count);
            for (int i = 0; i < _owners.Count; i++)
            {
                writer.WriteAddress(_owners[i]);
            }
            writer.WriteVarInt(_missingBlocks.Count);
            for (int i = 0; i < _missingBlocks.Count; i++)
            {
                writer.Write(_missingBlocks[i]);
            }
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
            Encryption = reader.ReadArchiveEncryption();

            var ownerCount = (int)reader.ReadVarInt();
            _owners.Clear();
            for (int i = 0; i < ownerCount; i++)
            {
                var addr = reader.ReadAddress();
                _owners.Add(addr);
            }

            var missingBlockCount = (int)reader.ReadVarInt();
            _missingBlocks.Clear();
            for (int i = 0; i < missingBlockCount; i++)
            {
                var blockIndex = reader.ReadInt32();
                _missingBlocks.Add(blockIndex);
            }
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

        public void AddOwner(Address address)
        {
            Throw.If(IsOwner(address), "already an owner of the archive");
            _owners.Add(address);
        }

        public void RemoveOwner(Address address)
        {
            Throw.If(!IsOwner(address), "not an owner of the archive");
            _owners.Remove(address);
        }

        public bool IsOwner(Address address)
        {
            return _owners.Contains(address);
        }

        public void AddMissingBlock(int blockIndex)
        {
            Throw.If(blockIndex < 0 || blockIndex >= BlockCount, "invalid block index");
            Throw.If(!_missingBlocks.Contains(blockIndex), "block index wasnt missing");

            _missingBlocks.Remove(blockIndex);
        }
    }
}
