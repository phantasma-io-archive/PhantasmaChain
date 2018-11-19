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
        public void BigIntAddNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) + new LargeInteger(y)).ToString() == (new BigInteger(-x) + new BigInteger(y)).ToString());
            Assert.IsTrue((new LargeInteger(x) + new LargeInteger(-y)).ToString() == (new BigInteger(x) + new BigInteger(-y)).ToString());
            Assert.IsTrue((new LargeInteger(-x) + new LargeInteger(-y)).ToString() == (new BigInteger(-x) + new BigInteger(-y)).ToString());

            Assert.IsTrue((new LargeInteger(-y) + new LargeInteger(x)).ToString() == (new BigInteger(-y) + new BigInteger(x)).ToString());
            Assert.IsTrue((new LargeInteger(y) + new LargeInteger(-x)).ToString() == (new BigInteger(y) + new BigInteger(-x)).ToString());
            Assert.IsTrue((new LargeInteger(-y) + new LargeInteger(-x)).ToString() == (new BigInteger(-y) + new BigInteger(-x)).ToString());
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
        public void BigIntSubNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) - new LargeInteger(y)).ToString() == (new BigInteger(-x) - new BigInteger(y)).ToString());
            Assert.IsTrue((new LargeInteger(x) - new LargeInteger(-y)).ToString() == (new BigInteger(x) - new BigInteger(-y)).ToString());
            Assert.IsTrue((new LargeInteger(-x) - new LargeInteger(-y)).ToString() == (new BigInteger(-x) - new BigInteger(-y)).ToString());

            Assert.IsTrue((new LargeInteger(-y) - new LargeInteger(x)).ToString() == (new BigInteger(-y) - new BigInteger(x)).ToString());
            Assert.IsTrue((new LargeInteger(y) - new LargeInteger(-x)).ToString() == (new BigInteger(y) - new BigInteger(-x)).ToString());
            Assert.IsTrue((new LargeInteger(-y) - new LargeInteger(-x)).ToString() == (new BigInteger(-y) - new BigInteger(-x)).ToString());
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
        public void BigIntMultNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) * new LargeInteger(y)).ToString() == (new BigInteger(-x) * new BigInteger(y)).ToString());
            Assert.IsTrue((new LargeInteger(x) * new LargeInteger(-y)).ToString() == (new BigInteger(x) * new BigInteger(-y)).ToString());
            Assert.IsTrue((new LargeInteger(-x) * new LargeInteger(-y)).ToString() == (new BigInteger(-x) * new BigInteger(-y)).ToString());

            Assert.IsTrue((new LargeInteger(-y) * new LargeInteger(x)).ToString() == (new BigInteger(-y) * new BigInteger(x)).ToString());
            Assert.IsTrue((new LargeInteger(y) * new LargeInteger(-x)).ToString() == (new BigInteger(y) * new BigInteger(-x)).ToString());
            Assert.IsTrue((new LargeInteger(-y) * new LargeInteger(-x)).ToString() == (new BigInteger(-y) * new BigInteger(-x)).ToString());
        }

        [TestMethod]
        public void BigIntDiv()
        {
            long x = 4311810559;
            uint y = 1048575;

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

        [TestMethod]
        public void BigIntDivNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) / new LargeInteger(y)).ToString() == (new BigInteger(-x) / new BigInteger(y)).ToString());
            Assert.IsTrue((new LargeInteger(x) / new LargeInteger(-y)).ToString() == (new BigInteger(x) / new BigInteger(-y)).ToString());
            Assert.IsTrue((new LargeInteger(-x) / new LargeInteger(-y)).ToString() == (new BigInteger(-x) / new BigInteger(-y)).ToString());

            Assert.IsTrue((new LargeInteger(-y) / new LargeInteger(x)).ToString() == (new BigInteger(-y) / new BigInteger(x)).ToString());
            Assert.IsTrue((new LargeInteger(y) / new LargeInteger(-x)).ToString() == (new BigInteger(y) / new BigInteger(-x)).ToString());
            Assert.IsTrue((new LargeInteger(-y) / new LargeInteger(-x)).ToString() == (new BigInteger(-y) / new BigInteger(-x)).ToString());
        }

        [TestMethod]
        public void TestSubtractionBorrowing()
        {
            var x1 = new LargeInteger(new uint[] {0x01000001});
            var y1 = new LargeInteger(new uint[] {0xfefeff});

            var x2 = new BigInteger(new byte[] { 0x01, 0x00, 0x00, 0x01 });
            var y2 = new BigInteger(new byte[] { 0xff, 0xfe, 0xfe });

            var groundTruth1 = new LargeInteger(new uint[] { 0x010102 });
            var groundTruth2 = new BigInteger(new byte[] { 0x02, 0x01, 0x01 });

            var z1 = x1 - y1;
            var z2 = x2 - y2;

            Assert.IsTrue(z1 == groundTruth1);
            Assert.IsTrue(z2 == groundTruth2);
        }

        [TestMethod]
        public void TestToString()
        {
            var x = new LargeInteger(new uint[] {0x1010});
            Assert.IsTrue(x.ToString() == "4112");
        }

        [TestMethod]
        public void TestComparison()
        {
            var x1 = new LargeInteger(new uint[] { 0x01000000});
            var y1 = new LargeInteger(new uint[] { 0xffffff });

            var x2 = new BigInteger(new byte[] { 0x00, 0x00, 0x00, 0x01 });
            var y2 = new BigInteger(new byte[] { 0xff, 0xff, 0xff });

            var z1 = x1 / y1;
            var z2 = x2 / y2;

            Assert.IsTrue(z1.ToString() == "1");
            Assert.IsTrue(z2.ToString() == "1");

            var test1 = new LargeInteger(new uint[]{0x0100});
            var test2 = new BigInteger(new byte[]{0x00, 0x01});

            Assert.IsTrue(test1 > z1);
            Assert.IsTrue(test2 > z2);
        }
        #endregion
    }
}
