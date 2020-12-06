using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Numerics;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Cryptography
{
    // Merkle tree implemented as a binary heap
    public class MerkleTree: ISerializable
    {
        private Hash[] _tree;
        private uint _maxDepthLeafCount;
        public uint MaxDepthLeafCount => _maxDepthLeafCount;

        public Hash Root => _tree[_tree.Length - 1];

        public static readonly uint ChunkSize = 256 * 1024;

        private MerkleTree()
        {

        }

        // TODO move this to a bette place, check if not duplicated
        //https://stackoverflow.com/questions/1322510/given-an-integer-how-do-i-find-the-next-largest-power-of-two-using-bit-twiddlin/1322548#1322548
        public static uint NextPowerOf2(uint n)
        {
            n--;
            n |= n >> 1;   // Divide by 2^k for consecutive doublings of k up to 32,
            n |= n >> 2;   // and then or the results.
            n |= n >> 4;
            n |= n >> 8;
            n |= n >> 16;
            n++;           // The result is a number of 1 bits equal to the number of bits in the original number, plus 1
            return n;
        }

        // chunkSize must be a power of 2
        public MerkleTree(byte[] content)
        {
            //Throw.If(content == null || content.Length < ChunkSize, "invalid content");
            Throw.If(content == null, "invalid content");

            var chunkCount = (uint)(content.Length / ChunkSize);
            if (chunkCount * ChunkSize < content.Length)
            {
                chunkCount++;
            }

            _maxDepthLeafCount = NextPowerOf2(chunkCount);

            //int maxLevel = 1;
            var temp = MaxDepthLeafCount;
            uint nodeCount = 0;

            while (temp > 0)
            {
                nodeCount += temp;
                temp /= 2;
                //maxLevel++;
            }

            _tree = new Hash[nodeCount];

            //hash the maximum depth leaves of the tree
            for (int i=0; i< MaxDepthLeafCount; i++)
            {
                Hash hash;

                var ofs = (uint)(i * ChunkSize);
                if (ofs < content.Length)
                {
                    var length = ChunkSize;
                    if (ofs+length > content.Length)
                    {
                        length = (uint)(content.Length - ofs);
                    }
                    hash = new Hash(CryptoExtensions.Sha256(content, ofs, length));
                }
                else
                {
                    hash = Hash.Null;
                }

                _tree[i] = hash;
            }

            //and how combine the leaf hashes in the branches
            uint prevOffset = 0;
            uint prevRows = MaxDepthLeafCount;

            while (true)
            {
                uint rows = prevRows / 2;
                if (rows <= 0)
                {
                    break;
                }

                uint offset = prevOffset + prevRows;

                for (uint i=0; i<rows; i++)
                {
                    uint childIndex = prevOffset + (i * 2);
                    var left = _tree[childIndex];
                    var right = _tree[childIndex + 1];
                    _tree[offset + i] = Hash.MerkleCombine(left, right);
                }

                prevOffset = offset;
                prevRows = rows;
            }
        }

        public static Hash CalculateBlockHash(byte[] content)
        {
            var hash = new Hash(CryptoExtensions.Sha256(content, 0, (uint)content.Length));
            return hash;
        }

        public bool VerifyContent(byte[] content, int blockIndex)
        {
            var hash = CalculateBlockHash(content);
            return VerifyContent(hash, blockIndex);
        }

        public bool VerifyContent(Hash hash, int blockIndex)
        {
            Throw.If(blockIndex < 0, "Invalid index");
            Throw.If(blockIndex >= MaxDepthLeafCount, "Index does not correspond to maximum depth leaf");
            var expectedHash = GetHash(blockIndex);
            return hash == expectedHash;
        }

        public Hash GetHash(int index)
        {
            return _tree[index];
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarInt(_maxDepthLeafCount);
            writer.WriteVarInt(_tree.Length);
            for (int i = 0; i < _tree.Length; i++)
            {
                writer.WriteHash(_tree[i]);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    this.SerializeData(writer);
                }
                return stream.ToArray();
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
            _maxDepthLeafCount = (uint)reader.ReadVarInt();
            var len = (int)reader.ReadVarInt();
            _tree = new Hash[len];
            for (int i = 0; i < len; i++)
            {
                _tree[i] = reader.ReadHash();
            }
        }

        public static MerkleTree FromBytes(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    var tree = new MerkleTree();
                    tree.UnserializeData(reader);
                    return tree;
                }
            }
        }
        
        public static MerkleTree Unserialize(BinaryReader reader)
        {
            var tree = new MerkleTree();
            tree.UnserializeData(reader);
            return tree;
        }

        public static uint GetChunkCountForSize(uint size)
        {
            var result = size / ChunkSize;
            if (size % ChunkSize > 0)
            {
                result++;
            }

            return result;
        }

        public static uint GetChunkCountForSize(BigInteger size)
        {
            var result = size / ChunkSize;
            if (size % ChunkSize > 0)
            {
                result++;
            }

            return (uint)result;
        }

    }
}
