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
            string x = "1ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
            string y = "1ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            string z = "3fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe";

            //var a1 = new BigInteger(x);
            //var b1 = new BigInteger(y);

            var a2 = new LargeInteger(x, 16);
            var b2 = new LargeInteger(y, 16);

            var r = new LargeInteger(z, 16);

            //var c1 = a1 + b1;
            var c2 = a2 + b2;

            //Assert.IsTrue(a1.ToString() == a2.ToString());
            //Assert.IsTrue(b1.ToString() == b2.ToString());
            //Assert.IsTrue(c1.ToString() == c2.ToString());
            Assert.IsTrue(c2 == r);
        }

        [TestMethod]
        public void BigIntAddNegatives()
        {
            string x = "1000";
            string y = "2000";

            Assert.IsTrue((new LargeInteger("-" + x, 10) + new LargeInteger(y, 10)).ToDecimal() == "1000");
            Assert.IsTrue((new LargeInteger(x, 10) + new LargeInteger("-" + y, 10)).ToDecimal() == "-1000");
            Assert.IsTrue((new LargeInteger("-" + x, 10) + new LargeInteger("-" + y, 10)).ToDecimal() == "-3000");

            Assert.IsTrue((new LargeInteger("-" + y, 10) + new LargeInteger(x, 10)).ToDecimal() == "-1000");
            Assert.IsTrue((new LargeInteger(y, 10) + new LargeInteger("-" + x, 10)).ToDecimal() == "1000");
            Assert.IsTrue((new LargeInteger("-" + y, 10) + new LargeInteger("-" + x, 10)).ToDecimal() == "-3000");
        }

        [TestMethod]
        public void BigIntSub()
        {
            string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f318533333333129873102938abfe298102967238291029850912890345297864812798570912830";
            string by = "18273910598278301928412039581203918927840501928391029237623187492834729018034982903478091248398457129387129837198237918274985791875";
            string bz = "1b07ff8cd9acc0011a06f77df9d725a1a1a611d52e2da690a22c9f4dd101abe9ea6400801135627b994c8a8d5fefef8bd272758b766b0e0ff62cefa523beb180fbb";

            var a = new LargeInteger(bx, 16);
            var b = new LargeInteger(by, 16);

            var c = a - b;
            var target = new LargeInteger(bz, 16);

            Assert.IsTrue(c == target);
        }

        [TestMethod]
        public void BigIntSubNegatives()
        {
            int x = 1000;
            int y = 2000;

            Assert.IsTrue((new LargeInteger(-x) - new LargeInteger(y)).ToDecimal() == "-3000");
            Assert.IsTrue((new LargeInteger(x) - new LargeInteger(-y)).ToDecimal() == "3000");
            Assert.IsTrue((new LargeInteger(-x) - new LargeInteger(-y)).ToDecimal() == "1000");

            Assert.IsTrue((new LargeInteger(-y) - new LargeInteger(x)).ToDecimal() == "-3000");
            Assert.IsTrue((new LargeInteger(y) - new LargeInteger(-x)).ToDecimal() == "3000");
            Assert.IsTrue((new LargeInteger(-y) - new LargeInteger(-x)).ToDecimal() == "-1000");
        }

        [TestMethod]
        public void BigIntMult()
        {
            string x = "120938109581209348298572913834710238901381847238471902348083410abefbaebf112987319828738192387509856";
            string y = "671035689173764871689727651290298572184350928350918429057384756781347915789013472348957aebfdc";
            string z = "742d9e573c9dd18b1ff173c47c69b9d6eb11462370e26720d642f3a430268ddd8e1996c9ccba94a880345a7d623b9fd5da50354f5ab846da8758b16eea574942abe7b7b01b1013d70423cdc160ac301b4cdef1be51e32664f7877102f5f13e8";

            var a = new LargeInteger(x, 16);
            var b = new LargeInteger(y, 16);
            var target = new LargeInteger(z, 16);

            var c = a * b;
            var tmp = (new BigInteger(x, 16) * new BigInteger(y, 16)).ToString();
            var tmp2 = target.ToDecimal();
            Assert.IsTrue(c == target);
        }

        [TestMethod]
        public void BigIntMultNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) * new LargeInteger(y)).ToDecimal() == "-100000000");
            Assert.IsTrue((new LargeInteger(x) * new LargeInteger(-y)).ToDecimal() == "-100000000");
            Assert.IsTrue((new LargeInteger(-x) * new LargeInteger(-y)).ToDecimal() == "100000000");

            Assert.IsTrue((new LargeInteger(-y) * new LargeInteger(x)).ToDecimal() == "-100000000");
            Assert.IsTrue((new LargeInteger(y) * new LargeInteger(-x)).ToDecimal() == "-100000000");
            Assert.IsTrue((new LargeInteger(-y) * new LargeInteger(-x)).ToDecimal() == "100000000");
        }

        [TestMethod]
        public void BigIntMultiDigitDiv()
        {
            bool bigIntFlag = false;

            string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f318533333333129873102938abfe298102967238291029850912890345297864812798570912830";
            string by = "18273910598278301928412039581203918927840501928391029237623187492834729018034982903478091248398457";
            string bq = "21e81159046f0d4c1fc54daf52b4638c36";
            string br = "16473496497eb5be6dcf5ac855637242609f07345a89d8c2c4fe00aa3d2cccd1dc504bad02805b41b5ffe15401666aa9d6";


            if (bigIntFlag == false)
            {
                var numerator = new LargeInteger(bx, 16);
                var denominator = new LargeInteger(by, 16);

                var target_quot = new LargeInteger(bq, 16);
                var target_rem = new LargeInteger(br, 16);

                LargeInteger quot;
                LargeInteger rem;
                LargeInteger.DivideAndModulus(numerator, denominator, out quot, out rem);

                Assert.IsTrue(quot == target_quot);
                Assert.IsTrue(rem == target_rem);
            }
            else
            {
                var numerator = new BigInteger(bx, 16);
                var denominator = new BigInteger(by, 16);

                var target_quot = new BigInteger(bq, 16);
                var target_rem = new BigInteger(br, 16);

                var quot = numerator / denominator;
                var rem = numerator % denominator;

                Assert.IsTrue(quot == target_quot);
                Assert.IsTrue(rem == target_rem);
            }
        }

        [TestMethod]
        public void BigIntSingleDigitDiv()
        {
            bool bigIntFlag = false;

            string bx = "332f389d332f3831332f389e332f37a5332f3959332f3914332f318533333333129873102938abfe29810296723829";
            string by = "A";
            string bq = "51e52761eb7ec04eb84b8dc9eb7ebf6eb84b8ef51eb1f4ed1eb1e8d51eb851eb50f3eb4d0ec1133042680423e9f37";
            string br = "3";

            if (bigIntFlag == false)
            {
                var numerator = new LargeInteger(bx, 16);
                var denominator = new LargeInteger(by, 16);

                var target_quot = new LargeInteger(bq, 16);
                var target_rem = new LargeInteger(br, 16);

                LargeInteger quot;
                LargeInteger rem;
                LargeInteger.DivideAndModulus(numerator, denominator, out quot, out rem);

                Assert.IsTrue(quot == target_quot);
                Assert.IsTrue(rem == target_rem);
            }
            else
            {
                var numerator = new BigInteger(bx, 16);
                var denominator = new BigInteger(by, 16);

                var target_quot = new BigInteger(bq, 16);
                var target_rem = new BigInteger(br, 16);

                var quot = numerator / denominator;
                var rem = numerator % denominator;

                Assert.IsTrue(quot == target_quot);
                Assert.IsTrue(rem == target_rem);
            }
        }

        [TestMethod]
        public void BigIntDivNegatives()
        {
            int x = 100000;
            int y = 1000;

            Assert.IsTrue((new LargeInteger(-x) / new LargeInteger(y)).ToDecimal() == "-100");
            Assert.IsTrue((new LargeInteger(x) / new LargeInteger(-y)).ToDecimal() == "-100");
            Assert.IsTrue((new LargeInteger(-x) / new LargeInteger(-y)).ToDecimal() == "100");

            Assert.IsTrue((new LargeInteger(-y) / new LargeInteger(x)).ToDecimal() == "0");
            Assert.IsTrue((new LargeInteger(y) / new LargeInteger(-x)).ToDecimal() == "0");
            Assert.IsTrue((new LargeInteger(-y) / new LargeInteger(-x)).ToDecimal() == "0");
        }

        [TestMethod]
        public void TestSubtractionBorrowing()
        {
            var x1 = new LargeInteger(new uint[] { 0x01000001 });
            var y1 = new LargeInteger(new uint[] { 0xfefeff });


            var groundTruth1 = new LargeInteger(new uint[] { 0x010102 });

            var z1 = x1 - y1;

            Assert.IsTrue(z1 == groundTruth1);
        }

        [TestMethod]
        public void TestToString()
        {
            uint[] xu = new uint[] { 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, 0x1 };
            var x = new LargeInteger(xu);
            Assert.IsTrue(x.ToDecimal() == "231584178474632390847141970017375815706539969331281128078915168015826259279871");
        }

        [TestMethod]
        public void TestComparison()
        {
            var x = new LargeInteger("1000000", 16);
            var y = new LargeInteger("ffffff", 16);

            var z = x / y;

            Assert.IsTrue(z.ToDecimal() == "1");

            var test1 = new LargeInteger("0100", 16);

            Assert.IsTrue(test1 > z);
        }
        #endregion
    }
}
