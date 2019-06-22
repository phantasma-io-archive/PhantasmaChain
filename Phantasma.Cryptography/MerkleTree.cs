using System;
using System.IO;
using System.Linq;
using Phantasma.Core;
using Phantasma.Core.Utils;
using Phantasma.Storage;
using Phantasma.Storage.Utils;

namespace Phantasma.Cryptography
{
    // Merkle tree implemented as a binary heap
    public class MerkleTree: ISerializable
    {
        private Hash[] _tree;

        public Hash Root => _tree[_tree.Length - 1];

        private MerkleTree()
        {

        }

        // TODO move this to a bette place, check if not duplicated
        //https://stackoverflow.com/questions/1322510/given-an-integer-how-do-i-find-the-next-largest-power-of-two-using-bit-twiddlin/1322548#1322548
        public static int NextPowerOf2(int n)
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
        internal MerkleTree(byte[] content, uint chunkSize)
        {
            Throw.If(content == null || content.Length < chunkSize, "invalid content");

            var chunkCount = (int)(content.Length / chunkSize);
            if (chunkCount * chunkSize < content.Length)
            {
                chunkCount++;
            }

            chunkCount = NextPowerOf2(chunkCount);

            int maxLevel = 1;
            var temp = chunkCount;
            var nodeCount = 0;

            while (temp > 0)
            {
                nodeCount += temp;
                temp /= 2;
                maxLevel++;
            }

            _tree = new Hash[nodeCount];

            for (int i=0; i< chunkCount; i++)
            {
                Hash hash;

                var ofs = (uint)(i * chunkSize);
                if (ofs < content.Length)
                {
                    var length = chunkSize;
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

            int prevOffset = 0;
            int prevRows = chunkCount;

            while (true)
            {
                int rows = prevRows / 2;
                if (rows <= 0)
                {
                    break;
                }

                int offset = prevOffset + prevRows;

                for (int i=0; i<rows; i++)
                {
                    int childIndex = prevOffset + (i * 2);
                    var left = _tree[childIndex];
                    var right = _tree[childIndex + 1];
                    _tree[offset + i] = Hash.MerkleCombine(left, right);
                }

                prevOffset = offset;
                prevRows = rows;
            }
        }

        public void SerializeData(BinaryWriter writer)
        {
            writer.WriteVarInt(_tree.Length);
            for (int i = 0; i < _tree.Length; i++)
            {
                writer.WriteHash(_tree[i]);
            }
        }

        public void UnserializeData(BinaryReader reader)
        {
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
    }
}
