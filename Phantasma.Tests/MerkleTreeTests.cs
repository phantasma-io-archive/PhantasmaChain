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
        public void TestMerkleSuccess()
        {
            uint fileSize = 1000;
            uint chunkSize = fileSize / 10;

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file, chunkSize);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }

            var chunk = new byte[chunkSize];
            for (int i = 0; i < chunkCount; i++)
            {
                Array.Copy(file, i * chunkSize, chunk, 0, chunkSize);
                Assert.IsTrue(tree.VerifyContent(chunk, i));
            }
        }

        [TestMethod]
        public void TestMerkleFailure()
        {
            uint fileSize = 1000;
            uint chunkSize = fileSize / 10;

            byte[] file = new byte[fileSize];

            Random r = new Random();
            r.NextBytes(file);

            var tree = new MerkleTree(file, chunkSize);

            var chunkCount = file.Length / chunkSize;
            if (chunkCount * chunkSize < file.Length)
            {
                chunkCount++;
            }

            var originalChunk = new byte[chunkSize];

            var fakeChunk = new byte[chunkSize];
            r.NextBytes(fakeChunk);

            for (int i = 0; i < chunkCount; i++)
            {
                Array.Copy(file, i * chunkSize, originalChunk, 0, chunkSize);

                while (fakeChunk.SequenceEqual(originalChunk))
                {
                    r.NextBytes(fakeChunk);
                }

                Assert.IsFalse(tree.VerifyContent(fakeChunk, i));
            }
        }
    }
}
