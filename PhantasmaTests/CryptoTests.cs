using System.Text;
using Phantasma.Cryptography;
using Phantasma.Utils;
using Phantasma.VM.Types;
using System;
using System.Linq;
using Phantasma.Mathematics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PhantasmaTests
{
    [TestClass]
    public class CryptoTests
    {
        [TestMethod]
        public void HashTests()
        {
            var bytes = new byte[32];
            var rnd = new Random();
            rnd.NextBytes(bytes);

            var hash = new Hash(bytes);

            Assert.IsTrue(hash.ToByteArray().Length == 32);
            Assert.IsTrue(hash.ToByteArray().SequenceEqual(bytes));

            bytes = new byte[10];
            BigInteger number;

            do
            {
                rnd.NextBytes(bytes);
                number = new BigInteger(bytes);
            } while (number <= 0);
             
            hash = number;
            Assert.IsTrue(hash.ToByteArray().Length == 32);

            BigInteger other = hash;
            Assert.IsTrue(number == other);

            Assert.IsTrue(Hash.TryParse("FFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsTrue(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("FFFFAAAXFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBB", out hash));
            Assert.IsFalse(Hash.TryParse("0xFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBBBFFFFAAAAFFFFBBB", out hash));
        }

        [TestMethod]
        public void KeyPairSign()
        {
            var keys = KeyPair.Generate();
            Assert.IsTrue(keys.PrivateKey.Length == KeyPair.PrivateKeyLength);
            Assert.IsTrue(keys.Address.PublicKey.Length == Address.PublicKeyLength);

            var msg = "Hello world";

            var msgBytes = Encoding.ASCII.GetBytes(msg);
            var signature = keys.Sign(msgBytes);

            var verified = signature.Verify(msgBytes, keys.Address);
            Assert.IsTrue(verified);

            // make sure that Verify fails for other addresses
            var otherKeys = KeyPair.Generate();
            Assert.IsFalse(otherKeys.Address == keys.Address);
            verified = signature.Verify(msgBytes, otherKeys.Address);
            Assert.IsFalse(verified);
        }
    }
}
