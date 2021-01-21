using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Cryptography.ECC;
using Phantasma.Numerics;
using System;
using System.Linq;
using System.Text;

// Testing covers following methods:
// ECPoint DecodePoint(byte[] encoded, ECCurve curve)

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoECPointTests
    {
        [TestMethod]
        public void DecodePointPredefinedTest()
        {
            var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";

            Assert.IsTrue(keyHex.Length == 64);

            var privateKey = Base16.Decode(keyHex);
            var pKey = ECCurve.Secp256k1.G * privateKey;

            var publicKey = pKey.EncodePoint(true).ToArray();

            var bytes = Phantasma.Cryptography.ECC.ECPoint.DecodePoint(publicKey, ECCurve.Secp256r1).EncodePoint(false);
            Console.WriteLine("bytes: " + Base16.Encode(bytes));

            Assert.IsTrue(Base16.Encode(bytes) == "042F5AE314AB517F917CECCFFD889DE6928A0770E005B67DE1A08CBAA732121EE9C6DC7F68B38DB2D52393397D1A9DBCEB6511CF238E32C4ABFF001581FF2EAF71");
        }
    }
}