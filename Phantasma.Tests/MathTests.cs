using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using Phantasma.Numerics;
using Phantasma.Cryptography;

namespace Phantasma.Tests
{
    [TestClass]
    public class MathTests
    {
        #region BASE CONVERSIONS
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
        #endregion

        #region BIG INT
        [TestMethod]
        public void BigIntAdd()
        {
            uint x = 5432543;
            uint y = 1432543;

            var a1 = new BigInteger(x);
            var b1 = new BigInteger(y);

            var a2 = new LargeInteger(x);
            var b2 = new LargeInteger(y);

            var c1 = a1 + b1;
            var c2 = a2 + b2;

            Assert.IsTrue(a1.ToString() == a2.ToString());
            Assert.IsTrue(b1.ToString() == b2.ToString());
            Assert.IsTrue(c1.ToString() == c2.ToString());
        }

        [TestMethod]
        public void BigIntSub()
        {
            uint x = 5432543;
            uint y = 1432543;

            var a1 = new BigInteger(x);
            var b1 = new BigInteger(y);

            var a2 = new LargeInteger(x);
            var b2 = new LargeInteger(y);

            var c1 = a1 - b1;
            var c2 = a2 - b2;

            Assert.IsTrue(a1.ToString() == a2.ToString());
            Assert.IsTrue(b1.ToString() == b2.ToString());
            Assert.IsTrue(c1.ToString() == c2.ToString());
        }

        [TestMethod]
        public void BigIntMult()
        {
            uint x = 5432543;
            uint y = 1432543;

            var a1 = new BigInteger(x);
            var b1 = new BigInteger(y);

            var a2 = new LargeInteger(x);
            var b2 = new LargeInteger(y);

            var c1 = a1 * b1;
            var c2 = a2 * b2;

            Assert.IsTrue(a1.ToString() == a2.ToString());
            Assert.IsTrue(b1.ToString() == b2.ToString());
            Assert.IsTrue(c1.ToString() == c2.ToString());
        }

        [TestMethod]
        public void BigIntDiv()
        {
            uint x = 5432543;
            uint y = 1432543;

            var a1 = new BigInteger(x);
            var b1 = new BigInteger(y);

            var a2 = new LargeInteger(x);
            var b2 = new LargeInteger(y);

            var c1 = a1 / b1;
            var c2 = a2 / b2;

            Assert.IsTrue(a1.ToString() == a2.ToString());
            Assert.IsTrue(b1.ToString() == b2.ToString());
            Assert.IsTrue(c1.ToString() == c2.ToString());
        }
        #endregion
    }
}
