using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Cryptography;

namespace Phantasma.Tests
{
    [TestClass]
    public class MerkleTreeTests
    {
        [TestMethod]
        public void TestSingleNodeMerkleSuccess()
        {
            uint fileSize = 1000;
            uint chunkSize = MerkleTree.ChunkSize;

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }

            var actualChunkSize = MerkleTree.ChunkSize < fileSize ? MerkleTree.ChunkSize : fileSize;

            var chunk = new byte[actualChunkSize];
            for (int i = 0; i < chunkCount; i++)
            {
                Array.Copy(file, i * actualChunkSize, chunk, 0, actualChunkSize);
                Assert.IsTrue(tree.VerifyContent(chunk, i));
            }
        }

        [TestMethod]
        public void TestSingleNodeMerkleFailure()
        {
            uint fileSize = 1000;
            uint chunkSize = MerkleTree.ChunkSize;

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }

            var actualChunkSize = MerkleTree.ChunkSize < fileSize ? MerkleTree.ChunkSize : fileSize;
            var originalChunk = new byte[actualChunkSize];

            var fakeChunk = new byte[actualChunkSize];
            r.NextBytes(fakeChunk);

            for (int i = 0; i < chunkCount; i++)
            {
                Array.Copy(file, i * actualChunkSize, originalChunk, 0, actualChunkSize);

                while (fakeChunk.SequenceEqual(originalChunk))
                {
                    r.NextBytes(fakeChunk);
                }

                Assert.IsFalse(tree.VerifyContent(fakeChunk, i));
            }
        }

        [TestMethod]
        public void TestMultipleNodeMerkleSuccess()
        {
            uint chunkSize = MerkleTree.ChunkSize;
            uint fileSize = (uint) (chunkSize * 20.9);

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }

            for (int i = 0; i < chunkCount; i++)
            {
                var leftoverFile = fileSize - (MerkleTree.ChunkSize * i);
                var actualChunkSize = MerkleTree.ChunkSize < leftoverFile ? MerkleTree.ChunkSize : leftoverFile;
                var chunk = new byte[actualChunkSize];

                Array.Copy(file, i * MerkleTree.ChunkSize, chunk, 0, actualChunkSize);
                Assert.IsTrue(tree.VerifyContent(chunk, i));
            }
        }

        [TestMethod]
        public void TestMultipleNodeMerkleFailure()
        {
            uint chunkSize = MerkleTree.ChunkSize;
            uint fileSize = (uint)(chunkSize * 20.9);

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }
            
            for (int i = 0; i < chunkCount; i++)
            {
                var leftoverFile = fileSize - (MerkleTree.ChunkSize * i);
                var actualChunkSize = MerkleTree.ChunkSize < leftoverFile ? MerkleTree.ChunkSize : leftoverFile;

                var originalChunk = new byte[actualChunkSize];
                Array.Copy(file, i * MerkleTree.ChunkSize, originalChunk, 0, actualChunkSize);

                var fakeChunk = new byte[actualChunkSize];
                do
                {
                    r.NextBytes(fakeChunk);
                } while (fakeChunk.SequenceEqual(originalChunk));

                Assert.IsFalse(tree.VerifyContent(fakeChunk, i));
            }
        }
    }
}
