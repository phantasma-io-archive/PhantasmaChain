using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Phantasma.Mathematics;
using Phantasma.Cryptography;

namespace Phantasma.Tests
{
    [TestClass]
    public class MathTests
    {
        [TestMethod]
        public void Base16Tests()
        {
            var bytes = new byte[Address.PublicKeyLength];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var base16 = Base16.Encode(bytes);

            Assert.IsTrue(base16.Length == bytes.Length * 2);

            var output = Base16.Decode(base16);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }

        [TestMethod]
        public void Base58Tests()
        {
            var bytes = new byte[Address.PublicKeyLength];
            var rnd = new Random(39545);
            rnd.NextBytes(bytes);

            var base58 = Base58.Encode(bytes);

            var output = Base58.Decode(base58);
            Assert.IsTrue(output.Length == bytes.Length);
            Assert.IsTrue(output.SequenceEqual(bytes));
        }
    }
}
