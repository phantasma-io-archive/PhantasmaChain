using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Numerics;
using Phantasma.Cryptography.EdDSA;
using System.Text;

// Testing methods:
// bool Verify(byte[] signature, byte[] message, byte[] publicKey)

namespace Phantasma.Tests
{
    [TestClass]
    public class CryptoEd25519Tests
    {
        [TestMethod]
        public void VerifyPredefinedTest()
        {
            var keyHex = "A1CF654B4FE1C83597D165C6C8B6089FFF6D53423E6FE93D41E687A2C12942F7";
            var keyIncorrectHex = "A1CF654B4FE1C83597D165C6C8B6189FFF6D53423E6FE93D41E687A2C12942F7";

            var signatureHex = "7E4054CE755B555F405A4CC911D5C88FBE77630C63FC43DA7C7D92235AA79887032AD4C42C16D380AB05BBD1AB6A60C6CF8FBC12758B2A1C96CC9734B1C35801";
            var signatureIncorrectHex = "7E4054CE755B555F405A4CC911D5C88FBE77630C63FC43DA7C7D92235AA79887032AD4C42C26D380AB05BBD1AB6A60C6CF8FBC12758B2A1C96CC9734B1C35801";

            var msg = "Hello Phantasma!";
            var msgBytes = Encoding.ASCII.GetBytes(msg);

            var msgIncorrect = "Hello Fhantasma!";
            var msgIncorrectBytes = Encoding.ASCII.GetBytes(msgIncorrect);

            var signatureBytes = Base16.Decode(signatureHex);
            var signatureIncorrectBytes = Base16.Decode(signatureIncorrectHex);

            var keys = new Cryptography.PhantasmaKeys(Base16.Decode(keyHex));
            Assert.IsTrue(keys.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            var keysIncorrect = new Cryptography.PhantasmaKeys(Base16.Decode(keyIncorrectHex));
            Assert.IsTrue(keysIncorrect.PrivateKey.Length == Cryptography.PhantasmaKeys.PrivateKeyLength);

            // Check correct signature, message and public key
            Assert.IsTrue(Ed25519.Verify(signatureBytes, msgBytes, keys.PublicKey));

            // Check incorrect signature
            Assert.IsFalse(Ed25519.Verify(signatureIncorrectBytes, msgBytes, keys.PublicKey));

            // Check incorrect message
            Assert.IsFalse(Ed25519.Verify(signatureBytes, msgIncorrectBytes, keys.PublicKey));

            // Check incorrect public key
            Assert.IsFalse(Ed25519.Verify(signatureBytes, msgBytes, keysIncorrect.PublicKey));

            // Check incorrect signature and message
            Assert.IsFalse(Ed25519.Verify(signatureIncorrectBytes, msgIncorrectBytes, keys.PublicKey));

            // Check incorrect signature and public key
            Assert.IsFalse(Ed25519.Verify(signatureIncorrectBytes, msgBytes, keysIncorrect.PublicKey));

            // Check incorrect message and public key
            Assert.IsFalse(Ed25519.Verify(signatureBytes, msgIncorrectBytes, keysIncorrect.PublicKey));

            // Check incorrect everything
            Assert.IsFalse(Ed25519.Verify(signatureIncorrectBytes, msgIncorrectBytes, keysIncorrect.PublicKey));
        }
    }
}