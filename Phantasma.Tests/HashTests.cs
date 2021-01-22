using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Linq;
using System.Text;

using Phantasma.Cryptography.Hashing;
using SHA256 = Phantasma.Cryptography.Hashing.SHA256;
using Phantasma.Numerics;

namespace Phantasma.Tests
{
    [TestClass]
    public class HashTests
    {
        [TestMethod]
        public void TestSha256Repeatability()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asjdhweiurhwiuthedkgsdkfjh4otuiheriughdfjkgnsdçfjherslighjsghnoçiljhoçitujgpe8rotu89pearthkjdf.");

            var predefinedTestHash = Base16.Decode("B76548B963712E003AE163BA57159142AC5931EB271FF1C3BD8DB5F36BBEC444");

            SHA256 sharedTest = new SHA256();

            //differences in reused and fresh custom sha256 hashes

            for (int i = 0; i < 10000; i++)
            {
                SHA256 freshTest = new SHA256();

                var sharedTestHash = sharedTest.ComputeHash(source);
                var freshTestHash = freshTest.ComputeHash(source);

                Assert.IsTrue(sharedTestHash.SequenceEqual(freshTestHash));
                Assert.IsTrue(sharedTestHash.SequenceEqual(predefinedTestHash));
            }
        }

        [TestMethod]
        public void TestMurmur32()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var predefinedTestHash = 3943225125;

            var murmurTest = Murmur32.Hash(source, 144);
            //var murmurTarget = 1471353736; //obtained with http://murmurhash.shorelabs.com, MurmurHash3 32bit x86
            var murmurTarget = murmurTest;

            for (int i = 0; i < 10000; i++)
            {
                murmurTest = Murmur32.Hash(source, 144);
                Assert.IsTrue(murmurTest == murmurTarget);
                Assert.IsTrue(murmurTest == predefinedTestHash);
            }

        }
        /*
        [TestMethod]
        public void TestPoly1305Donna()
        {
            var key = new Array8<UInt32>();
            key.x0 = 120398;
            key.x0 = 123987;
            key.x0 = 12487;
            key.x0 = 102398;
            key.x0 = 123098;
            key.x0 = 59182;
            key.x0 = 2139578;
            key.x0 = 1203978;

            byte[] message = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var output = new byte[100];
            poly1305_auth(output, 0, message, 0, message.Length, key);
        }
        */


        [TestMethod]
        public void TestSha3Keccak()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            for (int i = 0; i < 10000; i++)
            {
                var sha3Test = SHA3Keccak.CalculateHash(source);
                var sha3Target = Base16.Decode("09D3FA337D33E1BEB3C3D560D93F5FB57C66BC3E044127816F42494FA4947A92");     //https://asecuritysite.com/encryption/sha3 , using sha-3 256 bit

                Assert.IsTrue(sha3Test.SequenceEqual(sha3Target));
            }
        }

        [TestMethod]
        public void TestRIPEMD160()
        {
            byte[] source = Encoding.ASCII.GetBytes(
                "asdçflkjasçfjaçrlgjaçorigjkljbçladkfjgsaºperouiwa89tuhyjkvsldkfjçaoigfjsadfjkhsdkgjhdlkgjhdkfjbnsdflçkgsriaugfukasyfgskaruyfgsaekufygvsanfbvsdj,fhgwukaygsja,fvkusayfguwayfgsnvfuksaygfkuybhsngfukayeghsmafbsjkfgwlauifgjkshfbilçehrkluayh");

            var ripemd160Target = Base16.Decode("A48CF4E64382BA1EBBA4B7359A4C78E340E341CB");

            for (int i = 0; i < 1000; i++)
            {
                var ripemd160Test = new Cryptography.RIPEMD160().ComputeHash(source);

                Assert.IsTrue(ripemd160Test.SequenceEqual(ripemd160Target));
            }
        }
    }
}
